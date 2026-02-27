using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundManager : NetworkBehaviour
{
    // =========================================================
    // NETWORK STATE
    // =========================================================

    public NetworkVariable<MatchState> CurrentState =
        new NetworkVariable<MatchState>(
            MatchState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Replicated baseline for UI countdown (server time)
    private NetworkVariable<double> _preRoundEndServerTime = new NetworkVariable<double>(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // =========================================================
    // EVENTS (CLIENT SIDE)
    // =========================================================

    public static event Action<bool> OnLoadingScreen;
    public static event Action<int, int, bool, float> OnRoundEndPresentation;
    public static event Action<float> OnPreRoundStarted;
    public static event Action<int, int, int> OnMatchEnded;
    public static event Action<ulong, float> OnKillcamRequested;

    // =========================================================
    // SCORE / MATCH RULES
    // =========================================================

    [Header("Score")]
    public int TeamAScore { get; private set; }
    public int TeamBScore { get; private set; }

    private int lastWinningTeamId = -1;
    private int consecutiveWins = 0;
    private bool isOvertime = false;
    private const int ConsecutiveWinsRequired = 2;

    [Header("Round Timings")]
    [SerializeField] private float firstRoundCountdown = 15f;
    [SerializeField] private float subsequentRoundCountdown = 10f;
    [SerializeField] private float scoreboardSeconds = 3f;
    [SerializeField] private float deathCamSeconds = 2.0f;

    // =========================================================
    // READY HANDSHAKE
    // =========================================================

    private int _roundId = 0;
    private readonly HashSet<ulong> _readyClients = new HashSet<ulong>();
    private Coroutine _startRoundRoutine;

    [Header("Ready Handshake")]
    [SerializeField] private float readyTimeoutSeconds = 3.0f; // keep low: start ASAP, avoid 5s delays

    // =========================================================
    // RUNTIME REFERENCES
    // =========================================================

    private bool isFirstRound = true;

    private GamemodeDefinition gamemode;
    private LobbyManager lobbyManager;
    private SpawnManager spawnManager;
    private RoundDeathTracker deathTracker;
    private MapDefinition currentMap;
    private NetworkLobbyState lobbyState;

    // =========================================================
    // MATCH START / SCENE LOAD
    // =========================================================
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
            Debug.LogWarning($"[RoundManager] Failed to load scene {currentMap.sceneName} with status {status}");
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            SetLoadingScreenClientRpc(false);
        }
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (!IsServer) return;
        if (sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted) return;
        if (currentMap == null || sceneEvent.SceneName != currentMap.sceneName) return;

        NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;

        try
        {
            spawnManager = FindAnyObjectByType<SpawnManager>();
            if (spawnManager == null)
                throw new Exception("[RoundManager] SpawnManager not found in loaded scene.");

            spawnManager.Initialize(lobbyManager);

            deathTracker = FindAnyObjectByType<RoundDeathTracker>();
            if (deathTracker == null)
                throw new Exception("[RoundManager] RoundDeathTracker not found in loaded scene.");

            lobbyState = FindAnyObjectByType<NetworkLobbyState>();
            deathTracker.Initialize(this, lobbyState);

            // Reset match
            TeamAScore = 0;
            TeamBScore = 0;
            isOvertime = false;
            lastWinningTeamId = -1;
            consecutiveWins = 0;
            isFirstRound = true;

            SetLoadingScreenClientRpc(false);

            spawnManager.SpawnAllPlayers();

            // server hard-freeze simulation so nobody falls
            spawnManager.ServerSetAllGameplayEnabled(false);

            // also snap once right away (optional but good)
            spawnManager.ServerSnapAllToGround();

            SetFrozenClientRpc(true);
            SetPreRoundInputStateClientRpc();

            StartNextRound();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetLoadingScreenClientRpc(false);
        }
    }

    // =========================================================
    // ROUND FLOW
    // =========================================================
    private void StartNextRound()
    {
        if (!IsServer) return;

        float duration = isFirstRound ? firstRoundCountdown : subsequentRoundCountdown;
        isFirstRound = false;

        _roundId++;
        _readyClients.Clear();

        // Ensure everyone is frozen before countdown starts
        SetFrozenClientRpc(true);
        SetPreRoundInputStateClientRpc();

        if (_startRoundRoutine != null)
            StopCoroutine(_startRoundRoutine);

        _startRoundRoutine = StartCoroutine(ServerStartRoundWhenAllReady(duration, _roundId));
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
        // 1) Victim-only killcam while winners still move
        yield return new WaitForSeconds(deathCamSeconds);

        // 2) Freeze everyone before scoreboard
        SetFrozenClientRpc(true);

        // 3) Scoreboard
        PlayRoundEndPresentationClientRpc(TeamAScore, TeamBScore, matchOver: false, scoreboardSeconds);
        yield return new WaitForSeconds(scoreboardSeconds);

        // 4) Reset players for next round (still frozen)
        spawnManager.ResetAllPlayersForRound();

        // 5) Continue or end match
        CheckMatchEnd();
    }

    // =========================================================
    // READY HANDSHAKE + COUNTDOWN (SERVER TIME BASELINE)
    // =========================================================

    private IEnumerator ServerStartRoundWhenAllReady(float duration, int roundId)
    {
        // Ask clients to report ready for this round
        RequestClientReadyForRoundClientRpc(roundId);

        float t = 0f;
        while (t < readyTimeoutSeconds)
        {
            int expected = NetworkManager.Singleton.ConnectedClientsIds.Count;
            if (_readyClients.Count >= expected)
                break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        int expectedFinal = NetworkManager.Singleton.ConnectedClientsIds.Count;
        if (_readyClients.Count < expectedFinal)
        {
            Debug.LogWarning($"[RoundManager] Ready timeout. ready={_readyClients.Count}/{expectedFinal}. Starting anyway.");
        }

        // Shared baseline end time (server time)
        double endTime = NetworkManager.ServerTime.Time + duration;
        _preRoundEndServerTime.Value = endTime;

        // Tell clients to start countdown based on server time
        StartPreRoundClientRpc(endTime, roundId);

        // Wait using server time (not WaitForSeconds)
        while (NetworkManager.ServerTime.Time < endTime)
            yield return null;

        // final snap so everyone starts “on ground”
        spawnManager.ServerSnapAllToGround();

        // enable server simulation
        spawnManager.ServerSetAllGameplayEnabled(true);

        // now let clients play
        SetFrozenClientRpc(false);
        SetGameplayInputStateClientRpc();
        CurrentState.Value = MatchState.InRound;
    }

    [ClientRpc]
    private void RequestClientReadyForRoundClientRpc(int roundId)
    {
        StartCoroutine(ClientReportReadyWhenLocalPlayerExists(roundId));
    }

    private IEnumerator ClientReportReadyWhenLocalPlayerExists(int roundId)
    {
        // We want “ready” to mean: local player spawned + switcher exists
        const float timeout = 8f;
        float t = 0f;

        while (t < timeout)
        {
            var nm = NetworkManager.Singleton;
            var localPlayer = nm != null ? nm.LocalClient?.PlayerObject : null;

            if (localPlayer != null)
            {
                var sw = localPlayer.GetComponent<ClientComponentSwitcher>();
                if (sw != null)
                {
                    ReportClientReadyRpc(roundId);
                    yield break;
                }
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning($"[CLIENT] Ready timeout local. Reporting anyway. roundId={roundId}");
        ReportClientReadyRpc(roundId);
    }

    // IMPORTANT FIX: RoundManager is server-owned, so clients must be allowed to call this.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ReportClientReadyRpc(int roundId, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (roundId != _roundId) return; // ignore stale ready reports

        ulong sender = rpcParams.Receive.SenderClientId;
        _readyClients.Add(sender);
    }

    // =========================================================
    // PRE-ROUND UI (CLIENT)
    // =========================================================

    [ClientRpc]
    private void StartPreRoundClientRpc(double preRoundEndServerTime, int roundId)
    {
        // Keep client in loadout state until countdown ends
        SetPreRoundInputStateClientRpc();

        // UI is local-only, use ClientSystems (NO Instances)
        var clientSystems = FindFirstObjectByType<ClientSystems>();
        var ui = clientSystems != null ? clientSystems.UI : null;
        if (ui == null) return;

        double now = NetworkManager.Singleton.ServerTime.Time;
        float remaining = Mathf.Max(0f, (float)(preRoundEndServerTime - now));

        // For any other systems that want it
        OnPreRoundStarted?.Invoke(remaining);

        // Start the countdown via the GameplayUIManager.  The UI manager will
        // handle displaying the loadout panel, hiding the HUD and restoring
        // the appropriate state when the countdown finishes.  Do not
        // manually show/hide panels here.
        ui.StartCountdown(remaining);
    }

    // =========================================================
    // INPUT STATE CONTROL (CLIENT)
    // =========================================================

    [ClientRpc]
    private void SetPreRoundInputStateClientRpc()
    {
        StartCoroutine(WaitLocalSwitcherThen(s => s.SetState_Loadout(), "PreRound"));
    }

    [ClientRpc]
    private void SetGameplayInputStateClientRpc()
    {
        StartCoroutine(WaitLocalSwitcherThen(s => s.SetState_Gameplay(), "Gameplay"));
    }

    private IEnumerator WaitLocalSwitcherThen(Action<ClientComponentSwitcher> apply, string label)
    {
        const float timeout = 3f;
        float t = 0f;

        while (t < timeout)
        {
            var s = GetLocalSwitcherSafe();
            if (s != null)
            {
                // Apply the state change (e.g. SetState_Loadout or SetState_Gameplay)
                apply(s);

                // After switching the action map, update the UI input locks based on the
                // current UI state.  This ensures that if the pause menu or
                // loadout UI are visible, gameplay input remains disabled even
                // when the server tells us to switch to gameplay.
                try
                {
                    var clientSystems = FindFirstObjectByType<ClientSystems>();
                    var ui = clientSystems != null ? clientSystems.UI : null;
                    if (ui != null)
                    {
                        // Call the public wrapper rather than the private method for safety
                        ui.EnforceInputLockForCurrentUI();
                    }
                }
                catch { /* ignore: optional enforcement */ }

                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning($"[CLIENT] {label} input state failed: switcher still NULL after {timeout}s");
    }

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
        return ClientComponentSwitcher.ClientComponentSwitcherLocal.Local;
    }

    [ClientRpc]
    private void SetFrozenClientRpc(bool frozen)
    {
        var sw = GetLocalSwitcherSafe();
        if (sw == null) return;

        if (frozen)
        {
            sw.SetState_UIMenu(); // blocks gameplay maps
            sw.SetMovementGameplayEnabled(false);
            sw.SetWeaponGameplayEnabled(false);
        }
        else
        {
            // Allow movement and weapons again, but the UI manager will
            // enforce its own locks if pause or loadout are active.
            sw.SetMovementGameplayEnabled(true);
            sw.SetWeaponGameplayEnabled(true);
        }

        // After adjusting frozen state, update UI locks to ensure that if
        // the pause menu or loadout panel are still active, gameplay
        // remains disabled.
        try
        {
            var clientSystems = FindFirstObjectByType<ClientSystems>();
            var ui = clientSystems != null ? clientSystems.UI : null;
            ui?.EnforceInputLockForCurrentUI();
        }
        catch { /* ignore optional enforcement */ }
    }

    // =========================================================
    // MATCH MANAGEMENT
    // =========================================================

    private void CheckMatchEnd()
    {
        if (!IsServer) return;

        int target = gamemode.roundsToWin;

        bool someoneReachedTarget = (TeamAScore >= target) || (TeamBScore >= target);
        if (someoneReachedTarget && TeamAScore != TeamBScore)
        {
            EndMatch();
            return;
        }

        if (!someoneReachedTarget)
        {
            StartNextRound();
            return;
        }

        if (!isOvertime)
        {
            isOvertime = true;
            lastWinningTeamId = -1;
            consecutiveWins = 0;
        }

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
        else winningTeam = lastWinningTeamId;

        Debug.Log($"[RoundManager] Match ended. Winning team: {winningTeam}");
        StartCoroutine(EndMatchSequence(winningTeam));
    }

    private IEnumerator EndMatchSequence(int winningTeam)
    {
        ShowFinalResultsClientRpc(winningTeam, TeamAScore, TeamBScore);
        yield return new WaitForSeconds(5f);
        lobbyManager.OnMatchEnded(winningTeam);
    }

    // =========================================================
    // KILLCAM + SCORE UI
    // =========================================================

    public void ServerNotifyVictimDeathForKillCam(ulong victimClientId, ulong killerClientId)
    {
        if (!IsServer) return;
        if (killerClientId == ulong.MaxValue) return;

        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { victimClientId } }
        };

        StartKillCamClientRpc(killerClientId, deathCamSeconds, rpcParams);
    }

    [ClientRpc]
    private void StartKillCamClientRpc(ulong killerClientId, float seconds, ClientRpcParams rpcParams = default)
    {
        OnKillcamRequested?.Invoke(killerClientId, seconds);
    }

    [ClientRpc]
    private void PlayRoundEndPresentationClientRpc(int teamAScore, int teamBScore, bool matchOver, float showScoreSeconds)
    {
        OnRoundEndPresentation?.Invoke(teamAScore, teamBScore, matchOver, showScoreSeconds);
    }

    [ClientRpc]
    private void ShowFinalResultsClientRpc(int winningTeam, int teamAScore, int teamBScore)
    {
        OnMatchEnded?.Invoke(winningTeam, teamAScore, teamBScore);
        Debug.Log($"[CLIENT] Match over. Team {winningTeam} wins {teamAScore}–{teamBScore}");
    }

    // =========================================================
    // LOADING SCREEN
    // =========================================================

    [ClientRpc]
    private void SetLoadingScreenClientRpc(bool show)
    {
        OnLoadingScreen?.Invoke(show);
    }
}
