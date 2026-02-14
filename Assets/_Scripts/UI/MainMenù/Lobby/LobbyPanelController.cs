using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class LobbyPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private GameObject hostIpPanel;
    [SerializeField] private TextMeshProUGUI hostIpText;
    [SerializeField] private GameObject startMatchButton;

    [Header("Refs")]
    [SerializeField] private PanelManager uiManager;

    // Inject these via Inspector (preferred)
    [Header("Services (assign in Inspector if possible)")]
    [SerializeField] private NetworkLobbyState lobbyState;
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private MatchmakingManager matchmakingManager;
    [SerializeField] private RoundManager roundManager;
    [SerializeField] private AutoTeamToggleController autoTeamToggle;

    private Coroutine _bindRoutine;

    private void Awake()
    {
        // Fallbacks if not assigned in Inspector
        if (lobbyState == null) lobbyState = FindFirstObjectByType<NetworkLobbyState>();
        if (lobbyManager == null) lobbyManager = FindFirstObjectByType<LobbyManager>();
        if (matchmakingManager == null) matchmakingManager = FindFirstObjectByType<MatchmakingManager>();

        // RoundManager is DontDestroyOnLoad → should exist somewhere, cache it once
        if (roundManager == null) roundManager = FindFirstObjectByType<RoundManager>();
    }

    private void OnEnable()
    {
        RefreshUI();

        if (_bindRoutine != null) StopCoroutine(_bindRoutine);
        _bindRoutine = StartCoroutine(BindLobbyStateWhenReady());

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnPreShutdown += OnNetworkShutdown;
        }
    }

    private void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        UnsubscribeLobbyState();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnPreShutdown -= OnNetworkShutdown;
        }
    }

    private System.Collections.IEnumerator BindLobbyStateWhenReady()
    {
        // Ensure we have a lobbyState reference
        while (lobbyState == null)
        {
            lobbyState = FindFirstObjectByType<NetworkLobbyState>();
            yield return null;
        }

        // Wait until its NetworkList exists
        while (lobbyState.Players == null)
            yield return null;

        // Avoid double subscribe
        UnsubscribeLobbyState();

        lobbyState.LobbyName.OnValueChanged += OnLobbyNameChanged;
        lobbyState.Players.OnListChanged += OnPlayersChanged;

        // Now safe to refresh using Players
        RefreshUI();
    }

    private void UnsubscribeLobbyState()
    {
        if (lobbyState == null) return;

        if (lobbyState.LobbyName != null)
            lobbyState.LobbyName.OnValueChanged -= OnLobbyNameChanged;

        if (lobbyState.Players != null)
            lobbyState.Players.OnListChanged -= OnPlayersChanged;
    }

    // =========================
    // UI EVENTS
    // =========================
    public void OnReadyPressed()
    {
        if (lobbyState == null || NetworkManager.Singleton == null)
            return;

        lobbyState.ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void OnStartMatchPressed()
    {
        StartMatchIfPossible();
    }

    public void OnLeaveLobbyPressed()
    {
        if (NetworkManager.Singleton != null)
        {
            Debug.Log(NetworkManager.Singleton.IsHost
                ? "[LOBBY] Host stopped the lobby"
                : "[LOBBY] Client left the lobby");

            NetworkManager.Singleton.Shutdown();
        }
        uiManager.ShowMainMenu();

        RefreshUI();
    }

    // =========================
    // Match start helpers
    // =========================
    private bool AreAllPlayersReady()
    {
        if (lobbyState == null) return false;

        foreach (var p in lobbyState.Players)
            if (!p.isReady) return false;

        return true;
    }

    private void StartMatchIfPossible()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            return;

        if (lobbyState == null || lobbyManager == null)
        {
            Debug.LogWarning("[LOBBY] Cannot start match: missing lobby state/manager.");
            return;
        }

        if (!lobbyManager.AreTeamsValidForStart(out var reason))
        {
            Debug.LogWarning($"[LOBBY] Start blocked: {reason}");
            return;
        }

        bool allReady = AreAllPlayersReady();
        int playerCount = lobbyState.Players.Count;

        if (!lobbyManager.CanStartMatch(playerCount, allReady))
        {
            Debug.LogWarning("[LOBBY] Cannot start match yet. Ensure a gamemode, map, and enough ready players.");
            return;
        }

        lobbyManager.LockLobby();

        gameObject.SetActive(false);

        Debug.Log($"[LOBBY] StartMatch pressed. TeamAssignmentMode={lobbyManager.TeamAssignmentMode}");

        if (lobbyManager.TeamAssignmentMode == TeamAssignmentMode.Random)
            lobbyManager.AssignTeamsAutomatically();

        var gamemode = lobbyManager.GetSelectedGamemode();
        var map = lobbyManager.GetSelectedMap();

        if (roundManager == null) roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager == null)
        {
            Debug.LogError("[LOBBY] RoundManager not found; cannot start match.");
            return;
        }

        roundManager.StartMatch(lobbyManager, gamemode, map);
    }

    // =========================
    // NETWORK EVENTS
    // =========================
    private void OnLobbyNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue) => RefreshUI();
    private void OnPlayersChanged(NetworkListEvent<NetLobbyPlayer> _) => RefreshUI();

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;

        if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.ServerClientId)
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
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        if (startMatchButton != null)
            startMatchButton.SetActive(isHost);

        if (lobbyNameText != null)
            lobbyNameText.text = lobbyState != null ? lobbyState.LobbyName.Value.ToString() : string.Empty;

        if (hostIpText != null)
        {
            hostIpText.gameObject.SetActive(isHost);
            if (isHost)
                hostIpText.text = matchmakingManager != null ? $"Host IP: {matchmakingManager.GetLocalIP()}" : "Host IP: (missing)";
        }

        if (hostIpPanel != null)
        {
            if (isHost)
                hostIpPanel.SetActive(true);
            else
                hostIpPanel.SetActive(false);
        }

        if (autoTeamToggle != null)
            autoTeamToggle.RefreshUI();
    }
}
