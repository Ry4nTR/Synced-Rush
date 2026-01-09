using UnityEngine;

[CreateAssetMenu(fileName = "Map_", menuName = "Game/Map")]
public class MapDefinition : ScriptableObject
{
    [Header("Identity")]
    public string mapName;
    public string sceneName;

    [Header("Spawn Settings")]
    public int spawnsPerTeam;
}
