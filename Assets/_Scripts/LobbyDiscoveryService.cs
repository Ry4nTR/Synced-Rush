using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
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

    private List<IPEndPoint> _broadcastEndpoints;

    private Func<int> getPlayerCount;
    private Func<int> getMaxPlayers;
    private Func<bool> getInGame;

    private readonly ConcurrentQueue<LobbyInfo> pending = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    private float NowSeconds() => (float)clock.Elapsed.TotalSeconds;


#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public bool DebugIsListening => listener != null;
    public string DebugBoundEndpoint => _boundEndpoint;
    public int DebugPacketsReceived => _packetsReceived;
    public float DebugLastRecvAgoSeconds => _packetsReceived == 0 ? -1f : (NowSeconds() - _lastRecvTime);
    public int DebugLobbyCount { get { lock (lobbies) return lobbies.Count; } }

    private string _boundEndpoint = "n/a";
    private int _packetsReceived = 0;
    private float _lastRecvTime = 0f;
#endif

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

        _broadcastEndpoints = BuildBroadcastEndpoints();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[LobbyDiscovery] Broadcast endpoints:");
        foreach (var ep in _broadcastEndpoints)
            Debug.Log($"   -> {ep}");
#endif

        StartCoroutine(BroadcastCoroutine(lobbyName, hasPassword));
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

    private System.Collections.IEnumerator BroadcastCoroutine(string lobbyName, bool hasPassword)
    {
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
                foreach (var ep in _broadcastEndpoints)
                    broadcaster.Send(bytes, bytes.Length, ep);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyDiscoveryService] Broadcast send failed: {ex.Message}");
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"[LobbyDiscovery] SEND '{msg}'");
#endif

            yield return new WaitForSeconds(1f);
        }
    }

    private List<IPEndPoint> BuildBroadcastEndpoints()
    {
        var eps = new List<IPEndPoint>();

        // Global broadcast (may be ignored by some routers)
        eps.Add(new IPEndPoint(IPAddress.Broadcast, broadcastPort));

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var ipProps = ni.GetIPProperties();

            foreach (var ua in ipProps.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (ua.IPv4Mask == null)
                    continue;

                byte[] ipBytes = ua.Address.GetAddressBytes();
                byte[] maskBytes = ua.IPv4Mask.GetAddressBytes();
                byte[] broadcastBytes = new byte[4];

                for (int i = 0; i < 4; i++)
                    broadcastBytes[i] = (byte)(ipBytes[i] | (maskBytes[i] ^ 255));

                var broadcastAddress = new IPAddress(broadcastBytes);
                eps.Add(new IPEndPoint(broadcastAddress, broadcastPort));
            }
        }

        // Remove duplicates
        var unique = new Dictionary<string, IPEndPoint>();
        foreach (var ep in eps)
            unique[$"{ep.Address}:{ep.Port}"] = ep;

        return new List<IPEndPoint>(unique.Values);
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var lep = (IPEndPoint)listener.Client.LocalEndPoint;
            _boundEndpoint = $"{lep.Address}:{lep.Port}";
            Debug.Log($"[LobbyDiscovery] LISTENER BOUND {_boundEndpoint}");
#endif
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _packetsReceived++;
            _lastRecvTime = NowSeconds();
#endif

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