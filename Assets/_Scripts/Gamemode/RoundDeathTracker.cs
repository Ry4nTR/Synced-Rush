using UnityEngine;
using Unity.Netcode;

public class RoundDeathTracker : MonoBehaviour
{
    private LobbyManager lobbyManager;
    private RoundManager roundManager;

    // =========================
    // Initialization
    // =========================

    public void Initialize(LobbyManager lobby, RoundManager round)
    {
        lobbyManager = lobby;
        roundManager = round;
    }

    // =========================
    // Public API
    // =========================
    public void NotifyPlayerDeath(ulong clientId)
    {
        var lobbyState = NetworkLobbyState.Instance;
        if (lobbyState == null)
        {
            Debug.LogError("NetworkLobbyState instance not found. Cannot process death notification.");
            return;
        }

        var players = lobbyState.Players;
        int foundIndex = -1;
        NetLobbyPlayer deadPlayer = default;
        // Find the player in the network list
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].clientId == clientId)
            {
                foundIndex = i;
                deadPlayer = players[i];
                break;
            }
        }
        // Player not found
        if (foundIndex < 0)
        {
            Debug.LogWarning($"Player with clientId {clientId} not found in lobby state.");
            return;
        }
        // Ignore duplicate death notifications
        if (!deadPlayer.isAlive)
        {
            return;
        }

        // Mark player as dead and update the network list
        deadPlayer.isAlive = false;
        players[foundIndex] = deadPlayer;

        Debug.Log($"Player {deadPlayer.name} (client {clientId}) died");

        // Check if the player's team has been eliminated
        CheckTeamElimination(deadPlayer.teamId);
    }

    // =========================
    // Internal
    // =========================
    private void CheckTeamElimination(int teamId)
    {
        var lobbyState = NetworkLobbyState.Instance;
        if (lobbyState == null)
        {
            Debug.LogError("NetworkLobbyState instance not found. Cannot check team elimination.");
            return;
        }

        var players = lobbyState.Players;
        // If any player on the specified team is alive, the team is not eliminated
        foreach (var p in players)
        {
            if (p.teamId == teamId && p.isAlive)
                return;
        }

        // If we reach here, all players on the team are dead. Determine the opposing team id.
        int winningTeamId = teamId == 0 ? 1 : 0;
        roundManager.EndRound(winningTeamId);
    }
}
