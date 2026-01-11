using System;
using System.Collections;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;

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

    // =========================
    // Initialization
    // =========================
    private void Awake()
    {
        Instance = this;
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

    // =========================
    // Round Management
    // =========================
    private void StartNextRound()
    {
        if (!IsServer) return;

        Debug.Log("Preparing next round");

        //foreach (var player in lobbyManager.Players)
            //player.isAlive = true;

        spawnManager.SpawnAllPlayers();

        GameplayUtils.EnableGameplayForAllPlayers();

        CurrentState.Value = MatchState.InRound;

        Debug.Log("Round started");
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


}
