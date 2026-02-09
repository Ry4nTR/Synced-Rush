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

    private int lastWinningTeamId = -1;
    private int consecutiveWins = 0;

    private bool isOvertime = false;

    private const int ConsecutiveWinsRequired = 2;

    [SerializeField] private float firstRoundCountdown = 15f;
    [SerializeField] private float subsequentRoundCountdown = 10f;

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
        // Ensure a single persistent instance.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Persist across scene loads so lobby and game scenes share the same round manager
        DontDestroyOnLoad(gameObject);
    }

    // Starts a match by loading the selected map scene and initializing the round system.
    public void StartMatch(LobbyManager lobby, GamemodeDefinition mode, MapDefinition map)
    {
        if (!IsServer)
            return;

        // Store references for later initialization
        lobbyManager = lobby;
        gamemode = mode;
        currentMap = map;

        // Subscribe to scene events so we know when the map scene has finished loading.
        NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;

        // Attempt to load the map scene.
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.Show();
        }

        var status = NetworkManager.SceneManager.LoadScene(currentMap.sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogWarning($"Failed to load scene {currentMap.sceneName} with status {status}");
            // Unsubscribe to avoid memory leaks
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    // When the selected map finishes loading, initialize the round system and spawn players.
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

            // Hide the loading screen now that the map and managers are ready
            if (LoadingScreenManager.Instance != null)
            {
                LoadingScreenManager.Instance.Hide();
            }

            // Spawn players ONCE for this match scene
            spawnManager.SpawnAllPlayers();

            // Start the first round (this will only RESET players)
            StartNextRound();
        }
    }

    // =========================
    // Round Flow
    // =========================
    private void StartNextRound()
    {
        if (!IsServer) return;

        Debug.Log("Preparing next round");

        spawnManager.ResetAllPlayersForRound();

        float countdownDuration = isFirstRound ? firstRoundCountdown : subsequentRoundCountdown;

        StartPreRoundClientRpc(countdownDuration);
        StartCoroutine(BeginRoundAfterCountdown(countdownDuration));

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

        // Update the consecutive win tracker.
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

        // Notify clients to display the round scores.  This will show the
        // scoreboard for the current round (not match end).  The panel
        // remains visible for the 3 second RoundTransition delay.
        ShowRoundScoreClientRpc(TeamAScore, TeamBScore);

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
        if (!IsServer) return;

        int target = gamemode.roundsToWin;

        // If someone reached the target and it's not a tie -> match ends immediately.
        bool someoneReachedTarget = (TeamAScore >= target) || (TeamBScore >= target);
        if (someoneReachedTarget && TeamAScore != TeamBScore)
        {
            EndMatch(); // will compute winner from scores
            return;
        }

        // If nobody reached target yet -> continue playing normally.
        if (!someoneReachedTarget)
        {
            StartNextRound();
            return;
        }

        // If we reach here: someoneReachedTarget AND tie -> overtime.
        if (!isOvertime)
        {
            isOvertime = true;
            lastWinningTeamId = -1;
            consecutiveWins = 0;
        }

        // Overtime: need consecutive wins.
        if (consecutiveWins >= ConsecutiveWinsRequired)
            EndMatch();
        else
            StartNextRound();
    }

    private void EndMatch()
    {
        if (!IsServer) return;

        CurrentState.Value = MatchState.MatchEnd;

        int winningTeam;
        if (TeamAScore > TeamBScore) winningTeam = 0;
        else if (TeamBScore > TeamAScore) winningTeam = 1;
        else winningTeam = lastWinningTeamId; // fallback (should not happen if match end is correct)

        Debug.Log($"Match ended. Winning team: {winningTeam}");
        StartCoroutine(EndMatchSequence(winningTeam));
    }

    // Called on the server when the match ends.
    private IEnumerator EndMatchSequence(int winningTeam)
    {
        // Show results to clients
        ShowFinalResultsClientRpc(winningTeam, TeamAScore, TeamBScore);
        // Wait for a few seconds before returning to the lobby
        yield return new WaitForSeconds(5f);
        // Inform lobby manager of match end and reset lobby state
        lobbyManager.OnMatchEnded(winningTeam);
    }

    // Client RPC to display the final match results.
    [ClientRpc]
    private void ShowFinalResultsClientRpc(int winningTeam, int teamAScore, int teamBScore)
    {
        var ui = GameplayUIManager.Instance;
        if (ui != null)
        {
            ui.ShowScorePanel(teamAScore, teamBScore, true);
        }

        Debug.Log($"[CLIENT] Match over. Team {winningTeam} wins {teamAScore}–{teamBScore}");
    }

    [ClientRpc]
    private void ShowRoundScoreClientRpc(int teamAScore, int teamBScore)
    {
        var ui = GameplayUIManager.Instance;
        if (ui != null)
        {
            ui.ShowScorePanel(teamAScore, teamBScore, false);
        }
    }

    // =========================
    // Pre‑round Countdown
    // =========================
    // Invoked on all clients at the start of a new round.
    [ClientRpc]
    private void StartPreRoundClientRpc(float duration)
    {

        Debug.Log($"[CLIENT] StartPreRoundClientRpc duration={duration} localId={NetworkManager.Singleton.LocalClientId}");

        SetPreRoundInputStateClientRpc();

        // Only local clients handle UI.  Show the loadout panel and start the countdown.
        var ui = GameplayUIManager.Instance;
        if (ui != null)
        {
            // Hide any scoreboard from the previous round
            ui.HideScorePanel();

            ui.ShowLoadoutPanel();
            ui.StartCountdown(duration, () =>
            {
                // Hide the loadout panel when the countdown finishes and show the HUD
                ui.HideLoadoutPanel();
                ui.ShowHUD();
            });
        }
    }

    // Server coroutine that waits for the pre‑round countdown to finish before
    // enabling gameplay and setting the match state to InRound.
    private IEnumerator BeginRoundAfterCountdown(float duration)
    {
        yield return new WaitForSeconds(duration);

        // Re‑enable gameplay for all players now that the round is live
        SetGameplayInputStateClientRpc();

        // Set the match state to InRound
        CurrentState.Value = MatchState.InRound;

        Debug.Log("Round started");
    }

    // =========================
    // SET INPUT STATE
    // =========================
    [ClientRpc]
    private void SetPreRoundInputStateClientRpc()
    {
        StartCoroutine(WaitAndSetInputStateCoroutine(preRound: true));
    }

    [ClientRpc]
    private void SetGameplayInputStateClientRpc()
    {
        StartCoroutine(WaitAndSetInputStateCoroutine(preRound: false));
    }

    private IEnumerator WaitAndSetInputStateCoroutine(bool preRound)
    {
        const float timeout = 3f;
        float t = 0f;

        ClientComponentSwitcher switcher = null;

        while (switcher == null && t < timeout)
        {
            switcher = GetLocalSwitcherSafe();
            if (switcher != null) break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (switcher == null)
        {
            Debug.LogWarning($"[CLIENT] WaitAndSetInputStateCoroutine timeout preRound={preRound}");
            yield break;
        }

        if (preRound) switcher.SetState_Loadout();
        else switcher.SetState_Gameplay();
    }


    /// <summary>
    /// Helper to obtain the ClientComponentSwitcher for the local player.
    /// </summary>
    private ClientComponentSwitcher GetLocalSwitcherSafe()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            var po = nm.LocalClient?.PlayerObject;
            if (po != null)
            {
                var c = po.GetComponent<ClientComponentSwitcher>();
                if (c != null) return c;
            }
        }
        // Fall back to static cache set in ClientComponentSwitcher
        return ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
    }

}
