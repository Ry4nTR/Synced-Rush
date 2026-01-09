using System.Collections.Generic;
using UnityEngine;

public class MapSpawnPoints : MonoBehaviour
{
    [Header("Team A Spawns")]
    public List<Transform> teamASpawns = new();

    [Header("Team B Spawns")]
    public List<Transform> teamBSpawns = new();

    public Transform GetRandomSpawn(int teamId)
    {
        if (teamId == 0 && teamASpawns.Count > 0)
            return teamASpawns[Random.Range(0, teamASpawns.Count)];

        if (teamId == 1 && teamBSpawns.Count > 0)
            return teamBSpawns[Random.Range(0, teamBSpawns.Count)];

        return null;
    }
}
