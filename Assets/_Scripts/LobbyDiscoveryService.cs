using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

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
        List<string> toRemove = null;
        float now = Time.unscaledTime;

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
            {
                foreach (var ip in toRemove)
                    lobbies.Remove(ip);
            }
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
                broadcaster.Send(bytes, bytes.Length, loopback);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyDiscoveryService] Broadcast send failed: {ex.Message}");
            }

            yield return new WaitForSeconds(1f);
        }
    }

    // =========================
    // CLIENT: LISTEN
    // =========================

    public void StartListening()
    {
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
            Debug.LogWarning($"[LobbyDiscoveryService] Failed to start listener on port {broadcastPort}: {ex.Message}");
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

            // Backward compat:
            // Old: <ip>|<lobbyName>|<playerCount>|<hasPassword>
            // New: <ip>|<lobbyName>|<playerCount>|<maxPlayers>|<hasPassword>|<inGame>

            string ip = parts[0];
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
                // old format
                hasPwd = parts[3] == "1";
            }

            var info = new LobbyInfo
            {
                Ip = ip,
                LobbyName = name,
                PlayerCount = count,
                MaxPlayers = Mathf.Max(1, maxPlayers),
                HasPassword = hasPwd,
                InGame = inGame,
                lastSeen = Time.unscaledTime
            };

            lock (lobbies)
            {
                lobbies[ip] = info;
            }
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