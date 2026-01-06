using UnityEngine;

public enum GamemodeType
{
    OneVsOne,
    TwoVsTwo
}

[CreateAssetMenu(fileName = "Gamemode_", menuName = "Game/Gamemode")]
public class GamemodeDefinition : ScriptableObject
{
    [Header("Identity")]
    public GamemodeType gamemodeType;

    [Header("Players")]
    public int requiredPlayers;
    public int playersPerTeam;

    [Header("Match Rules")]
    public int roundsToWin;

    // =========================
    // Validation
    // =========================

    public bool IsPlayerCountValid(int playerCount)
    {
        return playerCount == requiredPlayers;
    }
}
