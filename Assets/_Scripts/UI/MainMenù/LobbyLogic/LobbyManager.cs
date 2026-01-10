using System.Collections.Generic;
using UnityEngine;

public enum TeamAssignmentMode
{
    Random,
    Manual
}

public class LobbyManager : MonoBehaviour
{
    [Header("Lobby Info")]
    public string LobbyName { get; private set; }

    [Header("State")]
    public LobbyState CurrentState { get; private set; } = LobbyState.None;

    [Header("Players")]
    [SerializeField] private List<LobbyPlayerData> players = new List<LobbyPlayerData>();

    [Header("Gamemode")]
    [SerializeField] private GamemodeDefinition selectedGamemode;

    [Header("Map")]
    [SerializeField] private MapDefinition selectedMap;

    [Header("Teams")]
    private readonly List<LobbyPlayerData> teamA = new();
    private readonly List<LobbyPlayerData> teamB = new();

    [Header("Team Assignment")]
    [SerializeField] private TeamAssignmentMode teamAssignmentMode = TeamAssignmentMode.Random;

    // TEMP: local id generator
    private int nextPlayerId = 0;

    public IReadOnlyList<LobbyPlayerData> Players => players;

    // =========================
    // LOBBY LIFECYCLE
    // =========================

    public void CreateLobby(string hostName, string lobbyName)
    {
        Debug.Log($"Lobby created: {lobbyName}");

        players.Clear();
        CurrentState = LobbyState.Open;
        LobbyName = lobbyName;

        var hostPlayer = new LobbyPlayerData(nextPlayerId++, hostName, true);
        players.Add(hostPlayer);
    }

    public void LeaveLobby()
    {
        Debug.Log("Lobby closed");

        players.Clear();
        selectedGamemode = null;
        selectedMap = null;
        CurrentState = LobbyState.None;
    }

    // =========================
    // PLAYER MANAGEMENT
    // =========================

    public void AddPlayer(string playerName)
    {
        if (CurrentState != LobbyState.Open)
            return;

        var player = new LobbyPlayerData(nextPlayerId++, playerName, false);
        players.Add(player);

        Debug.Log($"Player joined: {playerName}");
    }

    public void RemovePlayer(int playerId)
    {
        players.RemoveAll(p => p.playerId == playerId);
    }

    // =========================
    // READY SYSTEM
    // =========================

    public void SetReady(int playerId, bool ready)
    {
        var player = players.Find(p => p.playerId == playerId);
        if (player == null) return;

        player.isReady = ready;
        Debug.Log($"{player.playerName} ready: {ready}");
    }

    public bool AreAllPlayersReady()
    {
        foreach (var p in players)
        {
            if (!p.isReady && !p.isHost)
                return false;
        }
        return true;
    }

    // =========================
    // MATCH LIFECYCLE
    // =========================

    public bool CanStartMatch()
    {
        if (CurrentState != LobbyState.Open)
            return false;

        if (selectedGamemode == null)
            return false;

        if (!selectedGamemode.IsPlayerCountValid(players.Count))
            return false;

        if (!AreAllPlayersReady())
            return false;

        if (selectedMap == null)
            return false;

        return true;
    }

    public void LockLobby()
    {
        Debug.Log("Lobby locked");
        CurrentState = LobbyState.InGame;
    }

    public void StartMatch()
    {
        if (!CanStartMatch())
            return;

        Debug.Log("MATCH STARTED — LOBBY OK");

        /*
        if (teamAssignmentMode == TeamAssignmentMode.Random)
        {
            AssignTeamsAutomatically();

            if (!AreTeamsValid())
            {
                Debug.LogError("Team assignment failed. Match aborted.");
                return;
            }
        }

        CurrentState = LobbyState.InGame;

        var roundManager = FindAnyObjectByType<RoundManager>();
        roundManager.Initialize(this, selectedGamemode);

        Debug.Log("Match started");
        */
    }

    public void OnMatchEnded(int winningTeamId)
    {
        CurrentState = LobbyState.PostMatch;

        Debug.Log($"Lobby entering PostMatch. Winning team: {winningTeamId}");

        ResetLobbyAfterMatch();
    }

    private void ResetLobbyAfterMatch()
    {
        foreach (var player in players)
        {
            player.isReady = false;
            player.teamId = -1;
            player.isAlive = false;
        }

        teamA.Clear();
        teamB.Clear();

        Debug.Log("Lobby reset after match");
    }

    // =========================
    // GAMEMODE SELECTION
    // =========================

    public void SetGamemode(GamemodeDefinition gamemode)
    {
        if (CurrentState != LobbyState.Open)
            return;

        selectedGamemode = gamemode;
    }

    public GamemodeDefinition GetGamemode()
    {
        return selectedGamemode;
    }

    // =========================
    // GAMEMODE SELECTION
    // =========================

    public void SetMap(MapDefinition map)
    {
        if (CurrentState != LobbyState.Open)
            return;

        selectedMap = map;
    }

    // =========================
    // TEAM ASSIGNMENT & VALIDATION
    // =========================

    public void AssignTeamsAutomatically()
    {
        if (selectedGamemode == null)
            return;

        teamA.Clear();
        teamB.Clear();

        int perTeam = selectedGamemode.playersPerTeam;

        // Clone + shuffle players
        var shuffledPlayers = new List<LobbyPlayerData>(players);
        shuffledPlayers.Shuffle();

        foreach (var player in shuffledPlayers)
        {
            if (teamA.Count < perTeam)
            {
                teamA.Add(player);
                player.teamId = 0; // Team A
            }
            else
            {
                teamB.Add(player);
                player.teamId = 1; // Team B
            }
        }

        Debug.Log("Teams assigned automatically");
    }

    private bool AreTeamsValid()
    {
        if (selectedGamemode == null)
            return false;

        return teamA.Count == selectedGamemode.playersPerTeam &&
               teamB.Count == selectedGamemode.playersPerTeam;
    }

    // =========================
    // DEBUG / TEMP
    // =========================

    public LobbyPlayerData GetPlayerById(int playerId)
    {
        return players.Find(p => p.playerId == playerId);
    }
}
