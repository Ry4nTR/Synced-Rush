using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class LobbyDiscoveryService : MonoBehaviour
{
    public static LobbyDiscoveryService Instance { get; private set; }

    [Serializable]
    public class LobbyInfo
    {
        public string Ip;
        public string LobbyName;
        public int PlayerCount;
        public int MaxPlayers;
        public bool HasPassword;
        public bool InGame;

        [NonSerialized] public float lastSeen;
    }

    [SerializeField] private int broadcastPort = 47777;
    [SerializeField] private float lobbyTimeout = 5f;

    private UdpClient broadcaster;
    private UdpClient listener;
    private CancellationTokenSource listenCancellation;

    private readonly Dictionary<string, LobbyInfo> lobbies = new();

    private Func<int> getPlayerCount;
    private Func<int> getMaxPlayers;
    private Func<bool> getInGame;

    private readonly ConcurrentQueue<LobbyInfo> pending = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    private float NowSeconds() => (float)clock.Elapsed.TotalSeconds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // consume incoming packets on main thread
        while (pending.TryDequeue(out var info))
        {
            lock (lobbies) { lobbies[info.Ip] = info; }
        }

        // timeout pruning using the same clock
        float now = NowSeconds();
        List<string> toRemove = null;

        lock (lobbies)
        {
            foreach (var kvp in lobbies)
            {
                if (now - kvp.Value.lastSeen > lobbyTimeout)
                {
                    toRemove ??= new List<string>();
                    toRemove.Add(kvp.Key);
                }
            }
            if (toRemove != null)
                foreach (var ip in toRemove) lobbies.Remove(ip);
        }
    }

    // =========================
    // HOST: BROADCAST
    // =========================

    public void StartBroadcasting(
        string lobbyName,
        Func<int> getPlayerCountFunc,
        Func<int> getMaxPlayersFunc,
        Func<bool> getInGameFunc,
        bool hasPassword)
    {
        if (broadcaster != null) return;

        getPlayerCount = getPlayerCountFunc;
        getMaxPlayers = getMaxPlayersFunc;
        getInGame = getInGameFunc;

        try
        {
            broadcaster = new UdpClient();
            broadcaster.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LobbyDiscoveryService] Failed to start broadcaster: {ex.Message}");
            broadcaster = null;
            return;
        }

        var endpoint = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
        StartCoroutine(BroadcastCoroutine(lobbyName, hasPassword, endpoint));
    }

    // Convenience overload (if you don’t care about InGame yet)
    public void StartBroadcasting(string lobbyName, Func<int> getPlayerCountFunc, Func<int> getMaxPlayersFunc, bool hasPassword)
    {
        Debug.Log($"[LobbyDiscovery] START BROADCAST name='{lobbyName}' port={broadcastPort} hasPwd={hasPassword}");
        StartBroadcasting(lobbyName, getPlayerCountFunc, getMaxPlayersFunc, () => false, hasPassword);
    }

    public void StopBroadcasting()
    {
        if (broadcaster != null)
        {
            try { broadcaster.Close(); } catch { }
            broadcaster = null;
        }

        StopAllCoroutines();
    }

    private System.Collections.IEnumerator BroadcastCoroutine(string lobbyName, bool hasPassword, IPEndPoint endpoint)
    {
        var loopback = new IPEndPoint(IPAddress.Loopback, broadcastPort);

        while (broadcaster != null)
        {
            string ip = GetLocalIPAddress();

            int playerCount = getPlayerCount != null ? getPlayerCount() : 1;
            int maxPlayers = getMaxPlayers != null ? getMaxPlayers() : 2;
            bool inGame = getInGame != null && getInGame();

            string msg = $"{ip}|{lobbyName}|{playerCount}|{maxPlayers}|{(hasPassword ? 1 : 0)}|{(inGame ? 1 : 0)}";
            byte[] bytes = Encoding.UTF8.GetBytes(msg);

            try
            {
                // LAN
                broadcaster.Send(bytes, bytes.Length, endpoint);
                // Same-PC testing reliability
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                broadcaster.Send(bytes, bytes.Length, loopback);
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyDiscoveryService] Broadcast send failed: {ex.Message}");
            }

            Debug.Log($"[LobbyDiscovery] SEND '{msg}'");

            yield return new WaitForSeconds(1f);
        }
    }

    // =========================
    // CLIENT: LISTEN
    // =========================

    public void StartListening()
    {
        Debug.Log($"[LobbyDiscovery] StartListening() called. listener={(listener != null ? "NOT NULL" : "NULL")} port={broadcastPort}");

        if (listener != null) return;

        try
        {
            listener = new UdpClient();
            listener.EnableBroadcast = true;

            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.ExclusiveAddressUse = false;
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, broadcastPort));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LobbyDiscovery] LISTEN BIND FAILED port={broadcastPort} ex={ex}");
            listener = null;
            return;
        }

        listenCancellation = new CancellationTokenSource();
        Task.Run(() => ListenAsync(listenCancellation.Token));
    }

    public void StopListening()
    {
        if (listener != null)
        {
            try
            {
                listenCancellation?.Cancel();
                listener.Close();
            }
            catch { }

            listener = null;
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await listener.ReceiveAsync(); }
            catch (ObjectDisposedException) { break; }
            catch { if (ct.IsCancellationRequested) break; else continue; }

            string message;
            try { message = Encoding.UTF8.GetString(result.Buffer); }
            catch { continue; }

            var parts = message.Split('|');
            if (parts.Length < 4) continue;

            string remoteIp = result.RemoteEndPoint.Address.ToString();
            string advertisedIp = parts[0]; // this is the host’s LAN IP you put in the message

            // If the packet came from loopback, it's your same-PC test send.
            // Use the advertised LAN IP as the lobby key so it merges with the broadcast entry.
            string ipKey = (remoteIp == "127.0.0.1" || remoteIp == "::1") ? advertisedIp : remoteIp;
            string name = parts[1];

            int.TryParse(parts[2], out int count);

            int maxPlayers = 2;
            bool hasPwd = false;
            bool inGame = false;

            if (parts.Length >= 6)
            {
                int.TryParse(parts[3], out maxPlayers);
                hasPwd = parts[4] == "1";
                inGame = parts[5] == "1";
            }
            else
            {
                hasPwd = parts[3] == "1";
            }

            pending.Enqueue(new LobbyInfo
            {
                Ip = ipKey,
                LobbyName = name,
                PlayerCount = count,
                MaxPlayers = System.Math.Max(1, maxPlayers),
                HasPassword = hasPwd,
                InGame = inGame,
                lastSeen = NowSeconds()
            });
        }
    }

    public List<LobbyInfo> GetLobbies()
    {
        lock (lobbies)
        {
            return new List<LobbyInfo>(lobbies.Values);
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LobbyDiscoveryService] GetLocalIPAddress failed: {ex.Message}");
        }

        return "127.0.0.1";
    }
}