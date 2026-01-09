using System;

[Serializable]
public class LobbyPlayerData
{
    public int playerId;
    public string playerName;
    public int teamId;
    public bool isReady;
    public bool isHost;
    public bool isAlive;

    public LobbyPlayerData(int id, string name, bool host)
    {
        playerId = id;
        playerName = name;
        isHost = host;
        isReady = false;
        teamId = -1;
        isAlive = true;
    }
}
