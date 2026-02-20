using Unity.Netcode;
using UnityEngine;

public enum TeamAssignmentMode
{
    Random,
    Manual
}
public enum LobbyState
{
    None,
    Open,
    InGame,
    PostMatch
}

public class LobbyManager : MonoBehaviour
{
    [Header("Lobby Info")]
    public string LobbyName { get; private set; }

    [Header("State")]
    public LobbyState CurrentState { get; private set; } = LobbyState.None;

    [Header("Gamemode & Map")]
    [SerializeField] private GamemodeDefinition selectedGamemode;
    [SerializeField] private MapDefinition selectedMap;

    [Header("Team Assignment")]
    [SerializeField] private TeamAssignmentMode teamAssignmentMode = TeamAssignmentMode.Random;

    [SerializeField] private NetworkLobbyState lobbyState;

    public TeamAssignmentMode TeamAssignmentMode => teamAssignmentMode;

    private void Awake()
    {
        if (lobbyState == null)
            lobbyState = FindFirstObjectByType<NetworkLobbyState>();
    }

    // =========================
    // PLAYER TEAM ASSIGNMENT
    // =========================
    // Randomly assigns players to two teams based on the selected gamemode's playersPerTeam.
    public void AssignTeamsAutomatically()
    {
        if (lobbyState == null || selectedGamemode == null)
            return;

        var players = lobbyState.Players;
        int totalPlayers = players.Count;
        int perTeam = selectedGamemode.playersPerTeam;
        if (perTeam <= 0)
            perTeam = 1;

        // Build list of indices and shuffle
        var indices = new System.Collections.Generic.List<int>(totalPlayers);
        for (int i = 0; i < totalPlayers; i++) indices.Add(i);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var temp = indices[i];
            indices[i] = indices[j];
            indices[j] = temp;
        }

        // Assign teams sequentially based on playersPerTeam
        for (int idx = 0; idx < indices.Count; idx++)
        {
            int playerIndex = indices[idx];
            var p = players[playerIndex];
            p.teamId = idx / perTeam;
            p.isAlive = false;
            players[playerIndex] = p;
        }
    }

    // Sets the team assignment mode
    public void SetTeamAssignmentMode(TeamAssignmentMode mode)
    {
        teamAssignmentMode = mode;
    }

    // Assigns a player to a team.
    public void SetPlayerTeam(ulong clientId, int teamId)
    {
        if (lobbyState == null)
            return;
        // Forward the call to the network state. Only the host can invoke
        lobbyState.SetPlayerTeamServerRpc(clientId, teamId);
    }

    public bool AreTeamsValidForStart(out string reason)
    {
        reason = string.Empty;

        if (lobbyState == null || selectedGamemode == null)
        {
            reason = "Missing lobbyState or selectedGamemode";
            return false;
        }

        var players = lobbyState.Players;
        int perTeam = Mathf.Max(1, selectedGamemode.playersPerTeam);

        // Assuming exactly 2 teams (0 and 1) since your UI is Team A/B
        int teamCount = 2;

        int[] counts = new int[teamCount];

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];

            if (p.teamId < 0)
            {
                reason = $"Player {p.clientId} is Unassigned (teamId={p.teamId})";
                return false;
            }

            if (p.teamId >= teamCount)
            {
                reason = $"Player {p.clientId} has invalid teamId={p.teamId} (expected 0..{teamCount - 1})";
                return false;
            }

            counts[p.teamId]++;
        }

        // Optional: enforce max players per team (based on gamemode playersPerTeam)
        for (int t = 0; t < teamCount; t++)
        {
            if (counts[t] > perTeam)
            {
                reason = $"Team {t} has too many players ({counts[t]}/{perTeam})";
                return false;
            }
        }

        // Optional: enforce no empty teams (if your mode requires both teams present)
        // if (counts[0] == 0 || counts[1] == 0) { reason = "One team is empty"; return false; }

        return true;
    }

    // =========================
    // LOBBY MANAGEMENT
    // =========================
    public void CreateLobby(string hostName, string lobbyName)
    {
        LobbyName = lobbyName;
        CurrentState = LobbyState.Open;
    }

    public void LeaveLobby()
    {
        LobbyName = null;
        selectedGamemode = null;
        selectedMap = null;
        CurrentState = LobbyState.None;
    }

    // =========================
    // MATCH LIFECYCLE
    // =========================
    public bool CanStartMatch(int playerCount, bool allReady)
    {
        if (CurrentState != LobbyState.Open) return false;
        if (selectedGamemode == null) return false;
        if (!selectedGamemode.IsPlayerCountValid(playerCount)) return false;
        if (!allReady) return false;
        if (selectedMap == null) return false;

        // NEW: enforce team assignment rules
        if (!AreTeamsValidForStart(out var reason))
        {
            Debug.LogWarning($"[LobbyManager] Cannot start: teams invalid. {reason}");
            return false;
        }

        return true;
    }

    public void LockLobby()
    {
        CurrentState = LobbyState.InGame;
    }

    // Resets lobby state after a match ends.
    public void ResetLobbyAfterMatch()
    {
        if (lobbyState != null)
        {
            var players = lobbyState.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.isReady = false;
                p.isAlive = false;
                p.teamId = -1;
                players[i] = p;
            }
        }
        // Unlock the lobby for another match
        CurrentState = LobbyState.Open;
    }

    public void OnMatchEnded(int winningTeamId)
    {
        CurrentState = LobbyState.PostMatch;

        Debug.Log($"Lobby entering PostMatch. Winning team: {winningTeamId}");

        // Immediately reset lobby state so players must ready up again for the next match.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            ResetLobbyAfterMatch();
        }
    }

    // =========================
    // GAMEMODE & MAP SETTING
    // =========================
    public void SetGamemode(GamemodeDefinition gamemode)
    {
        if (CurrentState != LobbyState.Open)
            return;

        selectedGamemode = gamemode;
    }

    public GamemodeDefinition GetSelectedGamemode()
    {
        return selectedGamemode;
    }

    // Returns the currently selected map definition. This is used by the host when starting a match.
    public MapDefinition GetSelectedMap()
    {
        return selectedMap;
    }

    public void SetMap(MapDefinition map)
    {
        if (CurrentState != LobbyState.Open)
            return;

        selectedMap = map;
    }
}
