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

    // =========================
    // Overtime & scoring state
    // =========================
    // Tracks which team won the last round and how many rounds in a row they
    // have won.  Used to determine the winner during overtime.
    private int lastWinningTeamId = -1;
    private int consecutiveWins = 0;

    // Flag indicating whether the match is currently in overtime (sudden death).
    // Overtime starts when both teams reach the required roundsToWin value and
    // continues until a team wins ConsecutiveWinsRequired rounds in a row.
    private bool isOvertime = false;

    // Number of consecutive round wins required during overtime to win the match.
    private const int ConsecutiveWinsRequired = 2;

    // Pre‑round countdown durations.  The first round of the match uses a
    // longer countdown to give players more preparation time.  Subsequent
    // rounds use a shorter countdown.  These can be tuned per gamemode via
    // scriptable objects if desired.
    [SerializeField] private float firstRoundCountdown = 10f;
    [SerializeField] private float subsequentRoundCountdown = 5f;

    // Indicates whether the upcoming round is the first round of the match.
    private bool isFirstRound = true;

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

        // Reset overtime state for a new match
        isOvertime = false;
        lastWinningTeamId = -1;
        consecutiveWins = 0;
        isFirstRound = true;

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

            // Reset overtime and round tracking for the new match
            isOvertime = false;
            lastWinningTeamId = -1;
            consecutiveWins = 0;
            isFirstRound = true;

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
        // the loadout panel and countdown timer.  The first round of the match
        // uses a longer countdown to allow players more time to prepare, while
        // subsequent rounds use a shorter countdown for a snappier flow.
        float countdownDuration = isFirstRound ? firstRoundCountdown : subsequentRoundCountdown;
        StartPreRoundClientRpc(countdownDuration);

        // Start server coroutine to enable gameplay and update match state once countdown expires
        StartCoroutine(BeginRoundAfterCountdown(countdownDuration));

        // After scheduling the next round, mark that the first round has been
        // completed so that subsequent rounds use the shorter countdown.
        isFirstRound = false;
    }

    public void EndRound(int winningTeamId)
    {
        if (!IsServer) return;
        if (CurrentState.Value != MatchState.InRound) return;

        CurrentState.Value = MatchState.RoundEnd;

        if (winningTeamId == 0) TeamAScore++;
        else if (winningTeamId == 1) TeamBScore++;

        Debug.Log($"Round ended. Score A:{TeamAScore} B:{TeamBScore}");

        // Update the consecutive win tracker.  This always updates, even if
        // overtime has not started yet.  The logic in CheckMatchEnd will
        // determine when these values are used.
        if (winningTeamId == lastWinningTeamId)
        {
            consecutiveWins++;
        }
        else
        {
            lastWinningTeamId = winningTeamId;
            consecutiveWins = 1;
        }

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
        // If overtime has not started yet, determine whether to enter overtime
        // or simply continue playing.  Overtime begins only once both teams
        // reach the required roundsToWin threshold.  Prior to that, the match
        // continues until this condition is met.
        if (!isOvertime)
        {
            // Check if both teams have reached the threshold.  If not, start
            // another round without checking for match end.
            if (TeamAScore < gamemode.roundsToWin || TeamBScore < gamemode.roundsToWin)
            {
                StartNextRound();
                return;
            }

            // Both teams have met or exceeded the roundsToWin value → enter overtime.
            isOvertime = true;
            // Reset the consecutive win tracker for overtime so that the team
            // that wins two rounds in a row AFTER overtime starts wins the match.
            lastWinningTeamId = -1;
            consecutiveWins = 0;
        }

        // Overtime logic: a team must win the required number of consecutive rounds.
        if (consecutiveWins >= ConsecutiveWinsRequired)
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

        // Begin a coroutine to display the final results to all clients, wait
        // a short period, and then return to the lobby.  This ensures clients
        // have time to view the scoreboard before the lobby state resets.
        StartCoroutine(EndMatchSequence(winningTeam));
    }

    /// <summary>
    /// Called on the server when the match ends.  Sends the final results to
    /// clients via a client RPC, waits for a few seconds to allow players to
    /// read the scoreboard, then notifies the lobby manager of the match end
    /// which resets the lobby state.
    /// </summary>
    /// <param name="winningTeam">The ID of the winning team.</param>
    private IEnumerator EndMatchSequence(int winningTeam)
    {
        // Show results to clients
        ShowFinalResultsClientRpc(winningTeam, TeamAScore, TeamBScore);
        // Wait for a few seconds before returning to the lobby
        yield return new WaitForSeconds(5f);
        // Inform lobby manager of match end and reset lobby state
        lobbyManager.OnMatchEnded(winningTeam);
    }

    /// <summary>
    /// Client RPC to display the final match results.  This should invoke
    /// appropriate UI elements on clients to show which team won and the final
    /// scores.  For now it logs the results to the console; hooking into
    /// GameplayUIManager can be done when that script becomes available.
    /// </summary>
    /// <param name="winningTeam">The team that won.</param>
    /// <param name="teamAScore">Final score for Team A.</param>
    /// <param name="teamBScore">Final score for Team B.</param>
    [ClientRpc]
    private void ShowFinalResultsClientRpc(int winningTeam, int teamAScore, int teamBScore)
    {
        // TODO: integrate with GameplayUIManager to show a results panel on clients.
        Debug.Log($"[CLIENT] Match over. Team {winningTeam} wins {teamAScore}–{teamBScore}");
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
