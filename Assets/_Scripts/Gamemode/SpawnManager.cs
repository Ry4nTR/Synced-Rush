using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    private LobbyManager lobbyManager;
    private MapSpawnPoints spawnPoints;

    // =========================
    // Initialization
    // =========================

    public void Initialize(LobbyManager lobby)
    {
        lobbyManager = lobby;
        spawnPoints = FindAnyObjectByType<MapSpawnPoints>();

        if (spawnPoints == null)
            Debug.LogError("MapSpawnPoints not found in scene");
    }

    // =========================
    // Public API
    // =========================

    public void SpawnAllPlayers()
    {
        if (spawnPoints == null)
            return;
        /*
        foreach (var player in lobbyManager.Players)
        {
            Transform spawn = spawnPoints.GetRandomSpawn(player.teamId);
            SpawnPlayer(player, spawn);
        }
        */
    }

    // =========================
    // Internal
    // =========================
    /*
    private void SpawnPlayer(LobbyPlayerData player, Transform spawnPoint)
    {
        if (spawnPoint == null)
        {
            Debug.LogError("Invalid spawn point");
            return;
        }

        // Placeholder for now
        Debug.Log($"Spawning {player.playerName} at {spawnPoint.position}");

        // Later:
        // Instantiate player prefab
        // Assign ownership
        // Set position & rotation
    }
    */
}
