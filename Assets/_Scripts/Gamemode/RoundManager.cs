using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundManager : NetworkBehaviour
{
    public NetworkVariable<MatchState> CurrentState =
        new NetworkVariable<MatchState>(
            MatchState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public static event Action<ulong, float> OnKillcamRequested;
    public static event Action<bool> OnLoadingScreen;
    public static event Action<int, int, bool, float> OnRoundEndPresentation;
    public static event Action<float> OnPreRoundStarted;
    public static event Action<int, int, int> OnMatchEnded;


    [Header("Score")]
    public int TeamAScore { get; private set; }
    public int TeamBScore { get; private set; }

    private int lastWinningTeamId = -1;
    private int consecutiveWins = 0;

    private bool isOvertime = false;

    private const int ConsecutiveWinsRequired = 2;

    [SerializeField] private float firstRoundCountdown = 15f;
    [SerializeField] private float subsequentRoundCountdown = 10f;

    [SerializeField] private float scoreboardSeconds = 3f;
    [SerializeField] private float deathCamSeconds = 2.0f;


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
        // Persist across scene loads so lobby and game scenes share the same round manager
        DontDestroyOnLoad(gameObject);
    }

    // Starts a match by loading the selected map scene and initializing the round system.
    public void StartMatch(LobbyManager lobby, GamemodeDefinition mode, MapDefinition map)
    {
        if (!IsServer) return;

        if (lobby == null || mode == null || map == null || string.IsNullOrEmpty(map.sceneName))
        {
            Debug.LogError($"[RoundManager] StartMatch invalid args. lobby={lobby} mode={mode} map={map} sceneName={(map != null ? map.sceneName : "null")}");
            SetLoadingScreenClientRpc(false);
            return;
        }

        lobbyManager = lobby;
        gamemode = mode;
        currentMap = map;

        NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;

        SetLoadingScreenClientRpc(true);

        var status = NetworkManager.SceneManager.LoadScene(currentMap.sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogWarning($"Failed to load scene {currentMap.sceneName} with status {status}");
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            SetLoadingScreenClientRpc(false);
        }
    }

    // When the selected map finishes loading, initialize the round system and spawn players.
    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted)
            return;

        if (currentMap == null || sceneEvent.SceneName != currentMap.sceneName)
            return;

        NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;

        try
        {
            spawnManager = FindAnyObjectByType<SpawnManager>();
            if (spawnManager == null)
                throw new Exception("[RoundManager] SpawnManager not found in loaded scene.");

            // If SpawnManager.Initialize doesn't actually use lobby, remove that parameter (see below)
            spawnManager.Initialize(lobbyManager);

            deathTracker = FindAnyObjectByType<RoundDeathTracker>();
            if (deathTracker == null)
                throw new Exception("[RoundManager] RoundDeathTracker not found in loaded scene.");

            var lobbyState = FindAnyObjectByType<NetworkLobbyState>();
            deathTracker.Initialize(this, lobbyState);

            TeamAScore = 0;
            TeamBScore = 0;
            isOvertime = false;
            lastWinningTeamId = -1;
            consecutiveWins = 0;
            isFirstRound = true;

            SetLoadingScreenClientRpc(false);

            spawnManager.SpawnAllPlayers();
            StartNextRound();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetLoadingScreenClientRpc(false); // ✅ critical: don’t leave clients stuck
        }
    }


    // =========================
    // Round Flow
    // =========================
    private void StartNextRound()
    {
        if (!IsServer) return;

        // IMPORTANT: undo scoreboard freeze before going to pre-round
        SetFrozenClientRpc(false);

        float countdownDuration = isFirstRound ? firstRoundCountdown : subsequentRoundCountdown;

        StartPreRoundClientRpc(countdownDuration);
        StartCoroutine(BeginRoundAfterCountdown(countdownDuration));

        isFirstRound = false;
    }

    public void EndRound(int winningTeamId, ulong killerClientId)
    {
        if (!IsServer) return;
        if (CurrentState.Value != MatchState.InRound) return;

        CurrentState.Value = MatchState.RoundEnd;

        if (winningTeamId == 0) TeamAScore++;
        else if (winningTeamId == 1) TeamBScore++;

        StartCoroutine(RoundEndSequence(killerClientId));
    }

    private IEnumerator RoundEndSequence(ulong killerClientId)
    {
        // 1) Let the victim-only killcam play while winners can still move
        yield return new WaitForSeconds(deathCamSeconds);

        // 2) NOW freeze everyone (inputs off) because scoreboard is about to show
        SetFrozenClientRpc(true);

        // 3) Show scoreboard
        PlayRoundEndPresentationClientRpc(TeamAScore, TeamBScore, matchOver: false, scoreboardSeconds);
        yield return new WaitForSeconds(scoreboardSeconds);

        // 4) Reset/respawn for next round (still frozen)
        spawnManager.ResetAllPlayersForRound();

        // 5) Decide match end or next round
        CheckMatchEnd();
    }

    public void ServerNotifyVictimDeathForKillCam(ulong victimClientId, ulong killerClientId)
    {
        if (!IsServer) return;

        if (killerClientId == ulong.MaxValue) return;

        // Target ONLY the victim client
        StartKillCamForClients(killerClientId, deathCamSeconds, new[] { victimClientId });
    }

    private void StartKillCamForClients(ulong killerClientId, float seconds, ulong[] targetClientIds)
    {
        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = targetClientIds }
        };
        StartKillCamClientRpc(killerClientId, seconds, rpcParams);
    }

    [ClientRpc]
    private void StartKillCamClientRpc(ulong killerClientId, float seconds, ClientRpcParams rpcParams = default)
    {
        OnKillcamRequested?.Invoke(killerClientId, seconds);
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
        OnMatchEnded?.Invoke(winningTeam, teamAScore, teamBScore);

        Debug.Log($"[CLIENT] Match over. Team {winningTeam} wins {teamAScore}–{teamBScore}");
    }

    [ClientRpc]
    private void PlayRoundEndPresentationClientRpc(int teamAScore, int teamBScore, bool matchOver, float scoreboardSeconds)
    {
        OnRoundEndPresentation?.Invoke(teamAScore, teamBScore, matchOver, scoreboardSeconds);
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
        OnPreRoundStarted?.Invoke(duration);

        var sw = GetLocalSwitcherSafe();
        if (sw != null) sw.OwnerResetWeaponForNewRound();
    }

    // Server coroutine that waits for the pre‑round countdown to finish before
    // enabling gameplay and setting the match state to InRound.
    private IEnumerator BeginRoundAfterCountdown(float duration)
    {
        yield return new WaitForSeconds(duration);

        SetFrozenClientRpc(false);
        SetGameplayInputStateClientRpc();
        CurrentState.Value = MatchState.InRound;
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
        const float timeout = 12f;
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


    [ClientRpc]
    private void SetLoadingScreenClientRpc(bool show)
    {
        OnLoadingScreen?.Invoke(show);
    }

    [ClientRpc]
    private void SetFrozenClientRpc(bool frozen)
    {
        var sw = GetLocalSwitcherSafe();
        if (sw == null) return;

        if (frozen)
        {
            sw.SetState_UIMenu();
            sw.SetMovementGameplayEnabled(false);
            sw.SetWeaponGameplayEnabled(false);
        }
        else
        {
            // Do not force gameplay map here if your timeline will do it (pre-round/gameplay RPCs).
            sw.SetMovementGameplayEnabled(true);
            sw.SetWeaponGameplayEnabled(true);
        }
    }
}
