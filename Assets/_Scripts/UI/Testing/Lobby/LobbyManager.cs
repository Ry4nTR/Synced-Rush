using System.Collections.Generic;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    [Header("State")]
    public LobbyState CurrentState { get; private set; } = LobbyState.Inactive;

    [Header("Players")]
    [SerializeField] private List<LobbyPlayerData> players = new List<LobbyPlayerData>();

    [Header("Settings")]
    public bool randomTeams = true;

    // TEMP: local id generator
    private int nextPlayerId = 0;

    // =========================
    // LOBBY LIFECYCLE
    // =========================

    public void CreateLobby(string hostName)
    {
        Debug.Log("Lobby created");

        players.Clear();
        CurrentState = LobbyState.Open;

        // Host joins immediately
        var hostPlayer = new LobbyPlayerData(nextPlayerId++, hostName, true);
        players.Add(hostPlayer);
    }

    public void LeaveLobby()
    {
        Debug.Log("Lobby closed");

        players.Clear();
        CurrentState = LobbyState.Inactive;
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
    // MATCH START
    // =========================

    public bool CanStartMatch()
    {
        if (CurrentState != LobbyState.Open)
            return false;

        if (!AreAllPlayersReady())
            return false;

        return true;
    }

    public void LockLobby()
    {
        Debug.Log("Lobby locked");
        CurrentState = LobbyState.Locked;
    }

    // =========================
    // DEBUG / TEMP
    // =========================

    public List<LobbyPlayerData> GetPlayers()
    {
        return players;
    }
}
