using UnityEngine;

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

    /*
    public void NotifyPlayerDeath(int playerId)
    {
        var player = lobbyManager.GetPlayerById(playerId);
        if (player == null)
            return;

        if (!player.isAlive)
            return;

        player.isAlive = false;

        Debug.Log($"Player {player.playerName} died");

        CheckTeamElimination(player.teamId);
    }
    */

    // =========================
    // Internal
    // =========================

    /*
    private void CheckTeamElimination(int teamId)
    {
        foreach (var p in lobbyManager.Players)
        {
            if (p.teamId == teamId && p.isAlive)
                return;
        }

        // All players of this team are dead
        int winningTeamId = teamId == 0 ? 1 : 0;
        roundManager.EndRound(winningTeamId);
    }
    */
}
