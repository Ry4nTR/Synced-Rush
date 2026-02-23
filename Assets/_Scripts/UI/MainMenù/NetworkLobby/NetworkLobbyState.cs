using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkLobbyState : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> LobbyName =
        new("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkList<NetLobbyPlayer> Players;

    public NetworkVariable<int> MaxPlayers =
        new(2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private string lobbyPassword = string.Empty;

    private void Awake()
    {
        Players ??= new NetworkList<NetLobbyPlayer>();
    }

    public override void OnNetworkSpawn()
    {
        if (Players == null)
            Players = new NetworkList<NetLobbyPlayer>();

        if (IsServer)
        {
            Players.Clear();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
                OnClientConnected(id);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // =====================================================
    // CONNECTION / FULL LOBBY GUARD
    // =====================================================

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        if (clientId != NetworkManager.ServerClientId)
        {
            int max = Mathf.Max(1, MaxPlayers.Value);
            if (Players.Count >= max)
            {
                Debug.Log($"[LobbyState] Reject {clientId}: lobby full ({Players.Count}/{max})");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }
        }

        for (int i = 0; i < Players.Count; i++)
            if (Players[i].clientId == clientId)
                return;

        Players.Add(new NetLobbyPlayer
        {
            clientId = clientId,
            name = "Connecting...",
            isReady = false,
            isHost = clientId == NetworkManager.ServerClientId,
            teamId = -1,
            isAlive = false
        });
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        for (int i = Players.Count - 1; i >= 0; i--)
        {
            if (Players[i].clientId == clientId)
            {
                Players.RemoveAt(i);
                break;
            }
        }
    }

    // =====================================================
    // SERVER SETTINGS
    // =====================================================

    // Called from LobbyManager
    public void SetLobbyPassword(string password)
    {
        if (!IsServer) return;
        lobbyPassword = (password ?? string.Empty).Trim();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetMaxPlayersServerRpc(int maxPlayers)
    {
        if (!IsServer) return;
        MaxPlayers.Value = Mathf.Clamp(maxPlayers, 1, 16);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetLobbyNameServerRpc(string name)
    {
        if (!IsServer) return;
        LobbyName.Value = (name ?? string.Empty);
    }

    // =====================================================
    // PLAYER UPDATES
    // =====================================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerNameServerRpc(string playerName, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

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

    // REQUIRED BY LobbyPanelController
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ToggleReadyServerRpc(ulong clientId, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong sender = rpcParams.Receive.SenderClientId;

        // Only host can toggle others
        if (sender != NetworkManager.ServerClientId && sender != clientId)
            return;

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

    // REQUIRED BY LobbyManager (team assignment)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetPlayerTeamServerRpc(ulong targetClientId, int teamId, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // Only host can assign teams
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
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

    // =====================================================
    // PASSWORD CHECK
    // =====================================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitLobbyPasswordServerRpc(string passwordAttempt, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (string.IsNullOrEmpty(lobbyPassword))
            return;

        string attempt = (passwordAttempt ?? string.Empty).Trim();
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (!string.Equals(attempt, lobbyPassword))
        {
            Debug.Log($"[LobbyState] Wrong password from {senderId}, disconnecting.");
            NetworkManager.Singleton.DisconnectClient(senderId);
        }
    }

    public void ServerSetLobbyPassword(string password)
    {
        if (!IsServer) return;
        lobbyPassword = (password ?? string.Empty).Trim();
    }
}