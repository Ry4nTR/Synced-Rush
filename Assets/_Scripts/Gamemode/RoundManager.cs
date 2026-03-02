// ================================
// RoundManager (MODIFIED) ✅
// - Removed client-freeze RPC usage (pause must NOT freeze physics)
// - Match flow state is now the ONLY authority gate for gameplay sim
// - Deathcam keeps flow InRound, then switches to RoundEnd before scoreboard
// ================================

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using SyncedRush.Gamemode;

public class RoundManager : NetworkBehaviour
{
    public NetworkVariable<MatchFlowState> CurrentFlowState =
        new NetworkVariable<MatchFlowState>(
            MatchFlowState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public static event Action<MatchFlowState, MatchFlowState> OnMatchFlowStateChanged;

    private NetworkVariable<double> _preRoundEndServerTime = new NetworkVariable<double>(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

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
    [SerializeField] private float readyTimeoutSeconds = 3.0f;

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

    private SessionServices _sessionServices;

    // =========================================================
    // PROPERTIES
    // =========================================================
    public bool IsGameplayPhase => CurrentFlowState.Value == MatchFlowState.InRound;
    public bool IsPreRound => CurrentFlowState.Value == MatchFlowState.PreRoundFrozen;

    private void Awake()
    {
        _sessionServices = SessionServices.Current;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CurrentFlowState.OnValueChanged += HandleFlowStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        CurrentFlowState.OnValueChanged -= HandleFlowStateChanged;
        base.OnNetworkDespawn();
    }

    private void HandleFlowStateChanged(MatchFlowState oldState, MatchFlowState newState)
    {
        OnMatchFlowStateChanged?.Invoke(oldState, newState);
    }

    // =========================================================
    // MATCH START / SCENE LOAD
    // =========================================================
    public void StartMatch(LobbyManager lobby, GamemodeDefinition mode, MapDefinition map)
    {
        if (!IsServer) return;

        if (lobby == null || mode == null || map == null || string.IsNullOrEmpty(map.sceneName))
        {
            Debug.LogError($"[RoundManager] StartMatch invalid args. lobby={lobby} mode={mode} map={map} sceneName={(map != null ? map.sceneName : "null")} ");
            SetLoadingScreenClientRpc(false);
            return;
        }

        lobbyManager = lobby;
        gamemode = mode;
        currentMap = map;

        SetFlowState(MatchFlowState.Loading);

        NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;

        SetLoadingScreenClientRpc(true);

        var status = NetworkManager.SceneManager.LoadScene(currentMap.sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogWarning($"[RoundManager] Failed to load scene {currentMap.sceneName} with status {status}");
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            SetLoadingScreenClientRpc(false);
            SetFlowState(MatchFlowState.Lobby);
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

            TeamAScore = 0;
            TeamBScore = 0;
            isOvertime = false;
            lastWinningTeamId = -1;
            consecutiveWins = 0;
            isFirstRound = true;

            SetLoadingScreenClientRpc(false);

            spawnManager.SpawnAllPlayers();
            spawnManager.ServerSnapAllToGround();

            SetFlowState(MatchFlowState.Spawning);
            SetFlowState(MatchFlowState.PreRoundFrozen);

            StartNextRound();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetLoadingScreenClientRpc(false);
            SetFlowState(MatchFlowState.Lobby);
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

        SetFlowState(MatchFlowState.PreRoundFrozen);

        if (_startRoundRoutine != null)
            StopCoroutine(_startRoundRoutine);

        _startRoundRoutine = StartCoroutine(ServerStartRoundWhenAllReady(duration, _roundId));
    }

    public void EndRound(int winningTeamId, ulong killerClientId)
    {
        if (!IsServer) return;
        if (CurrentFlowState.Value != MatchFlowState.InRound) return;

        if (winningTeamId == 0) TeamAScore++;
        else if (winningTeamId == 1) TeamBScore++;

        StartCoroutine(RoundEndSequence(killerClientId));
    }

    private IEnumerator RoundEndSequence(ulong killerClientId)
    {
        // 1) deathcam time while winners still move (state remains InRound)
        yield return new WaitForSeconds(deathCamSeconds);

        // 2) now lock the round
        SetFlowState(MatchFlowState.RoundEnd);

        // 3) scoreboard
        PlayRoundEndPresentationClientRpc(TeamAScore, TeamBScore, matchOver: false, scoreboardSeconds);
        yield return new WaitForSeconds(scoreboardSeconds);

        // 4) reset players for next round (still not InRound)
        spawnManager.ResetAllPlayersForRound();

        // 5) continue/end match (StartNextRound will set PreRoundFrozen)
        CheckMatchEnd();
    }

    // =========================================================
    // READY HANDSHAKE + COUNTDOWN
    // =========================================================
    private IEnumerator ServerStartRoundWhenAllReady(float duration, int roundId)
    {
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

        double endTime = NetworkManager.ServerTime.Time + duration;
        _preRoundEndServerTime.Value = endTime;

        StartPreRoundClientRpc(endTime, roundId);

        while (NetworkManager.ServerTime.Time < endTime)
            yield return null;

        spawnManager.ServerSnapAllToGround();

        SetFlowState(MatchFlowState.InRound);
    }

    [ClientRpc]
    private void RequestClientReadyForRoundClientRpc(int roundId)
    {
        StartCoroutine(ClientReportReadyWhenLocalPlayerExists(roundId));
    }

    private IEnumerator ClientReportReadyWhenLocalPlayerExists(int roundId)
    {
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ReportClientReadyRpc(int roundId, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (roundId != _roundId) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        _readyClients.Add(sender);
    }

    // =========================================================
    // PRE-ROUND UI (CLIENT)
    // =========================================================
    [ClientRpc]
    private void StartPreRoundClientRpc(double preRoundEndServerTime, int roundId)
    {
        var clientSystems = FindFirstObjectByType<ClientSystems>();
        var ui = clientSystems != null ? clientSystems.UI : null;
        if (ui == null) return;

        double now = NetworkManager.Singleton.ServerTime.Time;
        float remaining = Mathf.Max(0f, (float)(preRoundEndServerTime - now));

        OnPreRoundStarted?.Invoke(remaining);
        ui.StartCountdown(remaining);
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

        SetFlowState(MatchFlowState.MatchEnd);

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

    private void SetFlowState(MatchFlowState newState)
    {
        if (!IsServer) return;

        if (CurrentFlowState.Value == newState) return;
        CurrentFlowState.Value = newState;
    }
}