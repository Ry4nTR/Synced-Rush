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
        // Ensure a single persistent instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Players = new NetworkList<NetLobbyPlayer>();

        // Persist across scene loads so lobby and game scenes share the same lobby state
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
        foreach (var p in Players)
        {
            if (p.clientId == clientId)
                return;
        }

        Debug.Log($"[SERVER] Client joined lobby: {clientId}");

        // Initialize the player entry with default team and alive status.
        // TeamId of -1 indicates unassigned; isAlive false until spawned in a round.
        Players.Add(new NetLobbyPlayer
        {
            clientId = clientId,
            name = PlayerProfile.PlayerName,
            isReady = false,
            isHost = clientId == NetworkManager.ServerClientId,
            teamId = -1,
            isAlive = false
        });
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetLobbyNameServerRpc(string name)
    {
        LobbyName.Value = name;
    }
}
