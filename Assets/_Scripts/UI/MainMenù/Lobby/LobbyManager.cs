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
    public static LobbyManager Instance { get; private set; }
    public string LobbyName { get; private set; }

    [Header("State")]
    public LobbyState CurrentState { get; private set; } = LobbyState.None;

    [Header("Gamemode & Map")]
    [SerializeField] private GamemodeDefinition selectedGamemode;
    [SerializeField] private MapDefinition selectedMap;

    [Header("Team Assignment")]
    [SerializeField] private TeamAssignmentMode teamAssignmentMode = TeamAssignmentMode.Random;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        if (CurrentState != LobbyState.Open)
            return false;

        if (selectedGamemode == null)
            return false;

        if (!selectedGamemode.IsPlayerCountValid(playerCount))
            return false;

        if (!allReady)
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

    public void OnMatchEnded(int winningTeamId)
    {
        CurrentState = LobbyState.PostMatch;

        Debug.Log($"Lobby entering PostMatch. Winning team: {winningTeamId}");
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

    public void SetMap(MapDefinition map)
    {
        if (CurrentState != LobbyState.Open)
            return;

        selectedMap = map;
    }

    public GamemodeDefinition GetSelectedGamemode()
    {
        return selectedGamemode;
    }

    public MapDefinition GetSelectedMap()
    {
        return selectedMap;
    }


}
