using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class AutoTeamToggleController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Toggle toggle;

    [Tooltip("Optional: set this to the visual root to hide (e.g. the whole row). If null, we hide only the Toggle.")]
    [SerializeField] private GameObject visualRoot;

    [Header("Behavior")]
    [SerializeField] private bool hideForClients = true;

    // Bound from SessionServices (NO searching)
    private LobbyManager lobbyManager;
    private NetworkLobbyState lobbyState;

    private bool isWired;
    private Coroutine initRoutine;

    private void Awake()
    {
        if (toggle == null) toggle = GetComponent<Toggle>();
        if (visualRoot == null) visualRoot = gameObject;
    }

    private void OnEnable()
    {
        // If session already exists (host/client already started), bind immediately.
        TryBindFromSession();

        // Otherwise bind when SessionRoot spawns.
        SessionServices.OnReady += HandleSessionReady;

        if (initRoutine != null) StopCoroutine(initRoutine);
        initRoutine = StartCoroutine(InitWhenNetworkAndSessionReady());
    }

    private void OnDisable()
    {
        SessionServices.OnReady -= HandleSessionReady;

        if (initRoutine != null)
        {
            StopCoroutine(initRoutine);
            initRoutine = null;
        }

        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);

        isWired = false;
    }

    private void HandleSessionReady(SessionServices _)
    {
        TryBindFromSession();
        // if we are already listening, we can refresh now
        RefreshUI();
    }

    private void TryBindFromSession()
    {
        var s = SessionServices.Current;
        if (s == null) return;

        lobbyManager = s.LobbyManager;
        lobbyState = s.LobbyState;
    }

    private IEnumerator InitWhenNetworkAndSessionReady()
    {
        // Wait for Netcode to start (host/client)
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        // Wait for SessionRoot to exist
        while (SessionServices.Current == null)
            yield return null;

        // Bind references from SessionServices
        TryBindFromSession();

        // Wait for LobbyState spawn (so RPCs/list are valid)
        while (lobbyState == null || !lobbyState.IsSpawned)
            yield return null;

        // Apply host/client visibility and wire toggle
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (NetworkManager.Singleton == null) return;

        bool isHost = NetworkManager.Singleton.IsHost;

        // Hide for clients
        if (hideForClients && !isHost)
        {
            if (visualRoot != null) visualRoot.SetActive(false);
            return;
        }

        // Show for host
        if (visualRoot != null) visualRoot.SetActive(true);

        // Make sure we are bound (in case RefreshUI is called early)
        TryBindFromSession();

        // Wire callback exactly once
        if (!isWired && toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
            toggle.onValueChanged.AddListener(OnToggleChanged);
            isWired = true;
        }
    }

    private void OnToggleChanged(bool enabled)
    {
        // Only host is allowed to assign teams
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            return;

        if (lobbyManager == null || lobbyState == null || !lobbyState.IsSpawned)
            return;

        lobbyManager.SetTeamAssignmentMode(enabled ? TeamAssignmentMode.Random : TeamAssignmentMode.Manual);

        if (enabled)
        {
            lobbyManager.AssignTeamsAutomatically();
        }
        else
        {
            // Clear teams
            var players = lobbyState.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.teamId = -1;
                p.isAlive = false;
                players[i] = p;
            }
        }
    }
}