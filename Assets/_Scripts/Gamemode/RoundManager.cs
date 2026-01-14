using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    public NetworkVariable<MatchState> CurrentState =
        new NetworkVariable<MatchState>(
            MatchState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [Header("Score")]
    public int TeamAScore { get; private set; }
    public int TeamBScore { get; private set; }

    private GamemodeDefinition gamemode;
    private LobbyManager lobbyManager;
    private SpawnManager spawnManager;
    private RoundDeathTracker deathTracker;
    private MapDefinition currentMap;

    // =========================
    // Initialization
    // =========================
    private void Awake()
    {
        // Ensure a single persistent instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Persist across scene loads so lobby and game scenes share the same round manager
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            CurrentState.OnValueChanged += OnMatchStateChanged;
        }
    }

    public void Initialize(LobbyManager lobby, GamemodeDefinition mode)
    {
        if (!IsServer) return;

        lobbyManager = lobby;
        gamemode = mode;

        spawnManager = FindAnyObjectByType<SpawnManager>();
        spawnManager.Initialize(lobby);

        deathTracker = FindAnyObjectByType<RoundDeathTracker>();
        deathTracker.Initialize(lobby, this);

        TeamAScore = 0;
        TeamBScore = 0;

        StartNextRound();
    }

    /// <summary>
    /// Starts a match by loading the selected map scene and initializing the round
    /// system once the scene load completes. This method should only be called
    /// by the host/server.
    /// </summary>
    public void StartMatch(LobbyManager lobby, GamemodeDefinition mode, MapDefinition map)
    {
        if (!IsServer)
            return;

        // Store references for later initialization
        lobbyManager = lobby;
        gamemode = mode;
        currentMap = map;

        // Subscribe to scene events so we know when the map scene has finished loading
        NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;

        // Attempt to load the map scene. Scenes must be added to the Build Settings.
        var status = NetworkManager.SceneManager.LoadScene(currentMap.sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogWarning($"Failed to load scene {currentMap.sceneName} with status {status}");
            // Unsubscribe to avoid memory leaks
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    // Callback for Netcode scene events. When the selected map finishes loading,
    // initialize the round system and spawn players.
    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Only act on LoadEventCompleted events for the current map
        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
            !string.IsNullOrEmpty(currentMap?.sceneName) &&
            sceneEvent.SceneName == currentMap.sceneName)
        {
            // Unsubscribe to prevent repeated calls
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;

            // Initialize managers now that the map is loaded
            spawnManager = FindAnyObjectByType<SpawnManager>();
            if (spawnManager == null)
            {
                Debug.LogError("SpawnManager not found in the loaded scene.");
            }
            else
            {
                spawnManager.Initialize(lobbyManager);
            }

            deathTracker = FindAnyObjectByType<RoundDeathTracker>();
            if (deathTracker == null)
            {
                Debug.LogError("RoundDeathTracker not found in the loaded scene.");
            }
            else
            {
                deathTracker.Initialize(lobbyManager, this);
            }

            // Reset scores
            TeamAScore = 0;
            TeamBScore = 0;

            // Start the first round
            StartNextRound();
        }
    }

    // =========================
    // Round Management
    // =========================
    private void StartNextRound()
    {
        if (!IsServer) return;

        Debug.Log("Preparing next round");

        if (!IsServer) return;

        // Spawn players using their assigned teams (set in the lobby)
        spawnManager.SpawnAllPlayers();

        // Disable gameplay inputs for all players during the pre‑round countdown
        GameplayUtils.DisableGameplayForAllPlayers();

        // Begin pre‑round countdown on all clients.  Clients will display
        // the loadout panel and countdown timer.
        float countdownDuration = gamemode != null ? gamemode.preRoundCountdown : 3f;
        StartPreRoundClientRpc(countdownDuration);

        // Start server coroutine to enable gameplay and update match state once countdown expires
        StartCoroutine(BeginRoundAfterCountdown(countdownDuration));
    }

    public void EndRound(int winningTeamId)
    {
        if (!IsServer) return;
        if (CurrentState.Value != MatchState.InRound) return;

        CurrentState.Value = MatchState.RoundEnd;

        if (winningTeamId == 0) TeamAScore++;
        else if (winningTeamId == 1) TeamBScore++;

        Debug.Log($"Round ended. Score A:{TeamAScore} B:{TeamBScore}");

        GameplayUtils.DisableGameplayForAllPlayers();

        StartCoroutine(RoundTransitionCoroutine());
    }

    private IEnumerator RoundTransitionCoroutine()
    {
        yield return new WaitForSeconds(3f);
        CheckMatchEnd();
    }

    // =========================
    // Match Management
    // =========================
    private void CheckMatchEnd()
    {
        if (TeamAScore >= gamemode.roundsToWin ||
            TeamBScore >= gamemode.roundsToWin)
        {
            EndMatch();
        }
        else
        {
            StartNextRound();
        }
    }

    private void EndMatch()
    {
        if (!IsServer) return;

        CurrentState.Value = MatchState.MatchEnd;

        int winningTeam = TeamAScore > TeamBScore ? 0 : 1;
        Debug.Log($"Match ended. Winning team: {winningTeam}");

        lobbyManager.OnMatchEnded(winningTeam);
    }

    private void OnMatchStateChanged(MatchState oldState, MatchState newState)
    {
        Debug.Log($"MatchState changed: {oldState} → {newState}");

        switch (newState)
        {
            case MatchState.InRound:
                GameplayUtils.EnableGameplayForAllPlayers();
                break;

            case MatchState.RoundEnd:
            case MatchState.MatchEnd:
            case MatchState.Lobby:
                GameplayUtils.DisableGameplayForAllPlayers();
                break;
        }
    }

    // =========================
    // Pre‑round Countdown
    // =========================
    /// <summary>
    /// Invoked on all clients at the start of a new round.  Shows the loadout
    /// selection UI and starts the countdown timer for the specified duration.
    /// </summary>
    /// <param name="duration">Length of the countdown in seconds.</param>
    [ClientRpc]
    private void StartPreRoundClientRpc(float duration)
    {
        // Only local clients handle UI.  Show the loadout panel and start the countdown.
        var ui = GameplayUIManager.Instance;
        if (ui != null)
        {
            ui.ShowLoadoutPanel();
            ui.StartCountdown(duration, () =>
            {
                // Hide the loadout panel when the countdown finishes and show the HUD
                ui.HideLoadoutPanel();
                ui.ShowHUD();
            });
        }
    }

    /// <summary>
    /// Server coroutine that waits for the pre‑round countdown to finish before
    /// enabling gameplay and setting the match state to InRound.
    /// </summary>
    /// <param name="duration">Duration of the countdown.</param>
    private IEnumerator BeginRoundAfterCountdown(float duration)
    {
        yield return new WaitForSeconds(duration);

        // Re‑enable gameplay for all players now that the round is live
        GameplayUtils.EnableGameplayForAllPlayers();

        // Set the match state to InRound
        CurrentState.Value = MatchState.InRound;

        Debug.Log("Round started");
    }
}
