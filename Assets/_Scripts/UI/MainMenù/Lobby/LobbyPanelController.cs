using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LobbyPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private GameObject hostIpPanel;
    [SerializeField] private TextMeshProUGUI hostIpText;
    [SerializeField] private GameObject startMatchButton;

    [Header("Refs")]
    [SerializeField] private PanelManager uiManager;

    [Header("Scene Services (MainMenu)")]
    [SerializeField] private MatchmakingManager matchmakingManager;   // keep this on AppBootstrap

    [Header("Children")]
    [SerializeField] private AutoTeamToggleController autoTeamToggle;

    private NetworkLobbyState lobbyState;
    private LobbyManager lobbyManager;
    private RoundManager roundManager;

    private Coroutine bindRoutine;

    private void OnEnable()
    {
        Bind();
        SessionServices.OnReady += OnSessionReady;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnPreShutdown += OnNetworkShutdown;
        }

        if (bindRoutine != null) StopCoroutine(bindRoutine);
        bindRoutine = StartCoroutine(BindWhenReady());

        RefreshUI();
    }

    private void OnDisable()
    {
        SessionServices.OnReady -= OnSessionReady;

        if (bindRoutine != null) { StopCoroutine(bindRoutine); bindRoutine = null; }

        UnsubscribeLobbyState();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnPreShutdown -= OnNetworkShutdown;
        }
    }

    private void OnSessionReady(SessionServices s)
    {
        Bind();
        RefreshUI();
    }

    private void Bind()
    {
        var s = SessionServices.Current;
        if (s == null) return;

        lobbyState = s.LobbyState;
        lobbyManager = s.LobbyManager;
        roundManager = s.RoundManager;

        SubscribeLobbyState();
    }

    private IEnumerator BindWhenReady()
    {
        while (SessionServices.Current == null)
            yield return null;

        Bind();
        RefreshUI();
    }

    private void SubscribeLobbyState()
    {
        if (lobbyState == null) return;

        UnsubscribeLobbyState();

        lobbyState.LobbyName.OnValueChanged += OnLobbyNameChanged;
        lobbyState.Players.OnListChanged += OnPlayersChanged;
    }

    private void UnsubscribeLobbyState()
    {
        if (lobbyState == null) return;

        lobbyState.LobbyName.OnValueChanged -= OnLobbyNameChanged;
        if (lobbyState.Players != null)
            lobbyState.Players.OnListChanged -= OnPlayersChanged;
    }

    // =========================
    // UI EVENTS
    // =========================
    public void OnReadyPressed()
    {
        if (lobbyState == null || NetworkManager.Singleton == null) return;
        lobbyState.ToggleReadyServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void OnStartMatchPressed()
    {
        StartMatchIfPossible();
    }

    public void OnLeaveLobbyPressed()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

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

        // IMPORTANT: these MUST come from SessionServices (network spawned)
        if (lobbyState == null || lobbyManager == null || roundManager == null)
        {
            Debug.LogWarning("[LOBBY] Cannot start match: SessionServices not bound yet.");
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
            Debug.LogWarning("[LOBBY] Cannot start match yet.");
            return;
        }

        lobbyManager.LockLobby();

        if (lobbyManager.TeamAssignmentMode == TeamAssignmentMode.Random)
            lobbyManager.AssignTeamsAutomatically();

        roundManager.StartMatch(lobbyManager, lobbyManager.GetSelectedGamemode(), lobbyManager.GetSelectedMap());
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
            uiManager.ShowMainMenu();
    }

    private void OnNetworkShutdown()
    {
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
            lobbyNameText.text = lobbyState != null ? lobbyState.LobbyName.Value.ToString() : "";

        if (hostIpPanel != null)
            hostIpPanel.SetActive(isHost);

        if (hostIpText != null)
        {
            hostIpText.gameObject.SetActive(isHost);
            if (isHost && matchmakingManager != null)
                hostIpText.text = $"Host IP: {matchmakingManager.GetLocalIP()}";
        }

        if (autoTeamToggle != null)
            autoTeamToggle.RefreshUI();
    }
}