using Unity.Services.Lobbies.Models;
using UnityEngine;

public class RoundDeathTracker : MonoBehaviour
{
    [SerializeField] private NetworkLobbyState lobbyState;

    private RoundManager roundManager;

    public void Initialize(RoundManager round, NetworkLobbyState state)
    {
        roundManager = round;
        lobbyState = state;
    }

    private void Awake()
    {
        if (lobbyState == null) lobbyState = FindFirstObjectByType<NetworkLobbyState>();
    }

    public void NotifyPlayerDeath(ulong victimClientId, ulong killerClientId)
    {
        if (lobbyState == null)
        {
            Debug.LogError("NetworkLobbyState not found.");
            return;
        }

        var players = lobbyState.Players;

        int foundIndex = -1;
        NetLobbyPlayer deadPlayer = default;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId == victimClientId)
            {
                foundIndex = i;
                deadPlayer = players[i];
                break;
            }
        }

        if (foundIndex < 0)
        {
            Debug.LogWarning($"Player with clientId {victimClientId} not found in lobby state.");
            return;
        }

        if (!deadPlayer.isAlive)
            return;

        deadPlayer.isAlive = false;
        players[foundIndex] = deadPlayer;

        Debug.Log($"Player {deadPlayer.name} (client {victimClientId}) died. Killer={killerClientId}");

        CheckTeamElimination(deadPlayer.teamId, killerClientId);
    }

    private void CheckTeamElimination(int teamId, ulong killerClientId)
    {
        if (lobbyState == null)
        {
            Debug.LogError("NetworkLobbyState not found.");
            return;
        }

        foreach (var p in lobbyState.Players)
        {
            if (p.teamId == teamId && p.isAlive)
                return;
        }

        int winningTeamId = teamId == 0 ? 1 : 0;
        roundManager.EndRound(winningTeamId, killerClientId);
    }
}
