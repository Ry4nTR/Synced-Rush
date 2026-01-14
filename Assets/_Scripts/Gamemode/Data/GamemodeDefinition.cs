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

    [Header("Round Timing")]
    [Tooltip("Duration of the pre‑round countdown in seconds.  During this time players can select their loadout before the round starts.")]
    public float preRoundCountdown = 10f;

    // =========================
    // Validation
    // =========================

    public bool IsPlayerCountValid(int playerCount)
    {
        return playerCount == requiredPlayers;
    }
}
