using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkLobbyState : NetworkBehaviour
{
    public static NetworkLobbyState Instance;

    public NetworkVariable<FixedString64Bytes> LobbyName =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkList<NetLobbyPlayer> Players;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Players = new NetworkList<NetLobbyPlayer>();

        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Players.Clear();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    // =========================
    // CONNECTION HANDLERS
    // =========================
    private void OnClientConnected(ulong clientId)
    {
        // Prevent duplicates
        for (int i = 0; i < Players.Count; i++)
            if (Players[i].clientId == clientId)
                return;

        string initialName = "Connecting...";

        // If this is the host (server client), we already know the local name on this machine
        if (clientId == NetworkManager.ServerClientId && IsHost)
            initialName = MatchmakingManager.Instance.LocalPlayerName;

        Players.Add(new NetLobbyPlayer
        {
            clientId = clientId,
            name = initialName,
            isReady = false,
            isHost = clientId == NetworkManager.ServerClientId,
            teamId = -1,
            isAlive = false
        });

        Debug.Log($"[SERVER] Client joined lobby: {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[SERVER] Client left lobby: {clientId}");

        for (int i = Players.Count - 1; i >= 0; i--)
        {
            if (Players[i].clientId == clientId)
            {
                Players.RemoveAt(i);
                break;
            }
        }
    }

    // =========================
    // RPCS
    // =========================
    // Toggle a player's ready status.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ToggleReadyServerRpc(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == clientId)
            {
                var p = Players[i];
                p.isReady = !p.isReady;
                Players[i] = p;
                break;
            }
        }
    }

    // Set the lobby name.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetLobbyNameServerRpc(string name)
    {
        LobbyName.Value = name;
    }

    // Set a player's display name.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerNameServerRpc(string playerName, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == senderId)
            {
                var p = Players[i];
                p.name = playerName;
                Players[i] = p;
                break;
            }
        }
    }

    // Set a player's team ID.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerTeamServerRpc(ulong targetClientId, int teamId, RpcParams rpcParams = default)
    {
        // Only the host can set teams; reject if the sender is not the host.
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != NetworkManager.ServerClientId)
            return;

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].clientId == targetClientId)
            {
                var p = Players[i];
                p.teamId = teamId;
                Players[i] = p;
                break;
            }
        }
    }
}
