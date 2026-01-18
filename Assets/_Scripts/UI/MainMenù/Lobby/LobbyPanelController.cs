using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class LobbyPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI hostIpText;
    [SerializeField] private GameObject startMatchButton;

    [Header("Refs")]
    [SerializeField] private PanelManager uiManager;

    private void OnEnable()
    {
        RefreshUI();

        if (NetworkLobbyState.Instance != null)
        {
            NetworkLobbyState.Instance.LobbyName.OnValueChanged += OnLobbyNameChanged;
            NetworkLobbyState.Instance.Players.OnListChanged += OnPlayersChanged;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnPreShutdown += OnNetworkShutdown;
        }
    }

    private void OnDisable()
    {
        if (NetworkLobbyState.Instance != null)
        {
            NetworkLobbyState.Instance.LobbyName.OnValueChanged -= OnLobbyNameChanged;
            NetworkLobbyState.Instance.Players.OnListChanged -= OnPlayersChanged;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnPreShutdown -= OnNetworkShutdown;
        }
    }


    // =========================
    // UI EVENTS
    // =========================
    public void OnReadyPressed()
    {
        if (NetworkLobbyState.Instance == null)
            return;

        NetworkLobbyState.Instance.ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void OnStartMatchPressed()
    {
        StartMatchIfPossible();
    }

    public void OnLeaveLobbyPressed()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[LOBBY] Host stopped the lobby");
            NetworkManager.Singleton.Shutdown();
        }
        else
        {
            Debug.Log("[LOBBY] Client left the lobby");
            NetworkManager.Singleton.Shutdown();
        }

        uiManager.ShowMainMenu();
    }

    // =========================
    // Match start helpers
    // =========================
    private bool AreAllPlayersReady()
    {
        // Returns true if every player in the network lobby list has toggled ready
        foreach (var p in NetworkLobbyState.Instance.Players)
        {
            if (!p.isReady)
                return false;
        }
        return true;
    }

    private void StartMatchIfPossible()
    {
        // Only the host/server is allowed to start the match
        if (!NetworkManager.Singleton.IsHost)
            return;

        // Ensure required managers exist
        if (NetworkLobbyState.Instance == null || LobbyManager.Instance == null)
        {
            Debug.LogWarning("[LOBBY] Cannot start match: missing lobby state.");
            return;
        }

        bool allReady = AreAllPlayersReady();
        int playerCount = NetworkLobbyState.Instance.Players.Count;

        if (!LobbyManager.Instance.CanStartMatch(playerCount, allReady))
        {
            Debug.LogWarning("[LOBBY] Cannot start match yet. Ensure a gamemode, map, and enough ready players.");
            return;
        }

        // Lock lobby state to prevent changes while the match is running
        LobbyManager.Instance.LockLobby();

        // Show a loading screen and hide the lobby UI so the player doesn't
        // see both the lobby and in‑game UIs at the same time.  The loading
        // screen will remain visible until the map scene finishes loading
        // and RoundManager hides it.  Only do this on the host as clients
        // receive the scene load automatically.
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.Show();
        }
        // Optionally hide the lobby panel itself to avoid duplicated UI on
        // scene load.  The panel will be re‑shown when returning to the lobby.
        gameObject.SetActive(false);

        // Perform automatic team assignment if the lobby is set to Random.  Teams
        // will be assigned only once before the match starts.
        if (LobbyManager.Instance != null &&
            LobbyManager.Instance.TeamAssignmentMode == TeamAssignmentMode.Random)
        {
            LobbyManager.Instance.AssignTeamsAutomatically();
        }

        // Retrieve selected gamemode and map
        var gamemode = LobbyManager.Instance.GetSelectedGamemode();
        var map = LobbyManager.Instance.GetSelectedMap();

        // Acquire the RoundManager instance (it should persist across scenes)
        var roundManager = RoundManager.Instance != null ? RoundManager.Instance : FindAnyObjectByType<RoundManager>();
        if (roundManager == null)
        {
            Debug.LogError("[LOBBY] RoundManager not found; cannot start match.");
            return;
        }

        roundManager.StartMatch(LobbyManager.Instance, gamemode, map);
    }

    // =========================
    // NETWORK EVENTS
    // =========================
    private void OnLobbyNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        RefreshUI();
    }

    private void OnPlayersChanged(NetworkListEvent<NetLobbyPlayer> _)
    {
        RefreshUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Host left → client returns to menu
        if (!NetworkManager.Singleton.IsHost &&
            clientId == NetworkManager.ServerClientId)
        {
            Debug.Log("[NET] Host left lobby");
            uiManager.ShowMainMenu();
        }
    }

    private void OnNetworkShutdown()
    {
        Debug.Log("[NET] Network shutdown — returning to main menu");
        uiManager.ShowMainMenu();
    }


    // =========================
    // INTERNAL
    // =========================
    private void RefreshUI()
    {
        bool isHost = NetworkManager.Singleton != null &&
                      NetworkManager.Singleton.IsHost;

        startMatchButton.SetActive(isHost);

        if (NetworkLobbyState.Instance != null)
        {
            lobbyNameText.text =
                NetworkLobbyState.Instance.LobbyName.Value.ToString();
        }
        else
        {
            lobbyNameText.text = string.Empty;
        }

        if (isHost)
        {
            hostIpText.gameObject.SetActive(true);
            hostIpText.text =
                $"Host IP: {MatchmakingManager.Instance.GetLocalIP()}";
        }
        else
        {
            hostIpText.gameObject.SetActive(false);
        }
    }

}
