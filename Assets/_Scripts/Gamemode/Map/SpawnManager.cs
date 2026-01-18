using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SpawnManager : MonoBehaviour
{
    private MapSpawnPoints spawnPoints;
    [Header("Prefabs")]
    [Tooltip("Networked player prefab to spawn for each lobby member")]
    public GameObject playerPrefab;

    // =========================
    // Initialization
    // =========================

    public void Initialize(LobbyManager lobby)
    {
        // At the start of a match we need to locate the map's spawn point data.
        if (spawnPoints == null)
        {
            spawnPoints = FindAnyObjectByType<MapSpawnPoints>();
            if (spawnPoints == null)
            {
                Debug.LogError("MapSpawnPoints not found in scene");
                return;
            }
        }
    }

    // =========================
    // Public API
    // =========================

    public void SpawnAllPlayers()
    {
        if (spawnPoints == null)
        {
            Debug.LogError("SpawnManager not initialised: spawnPoints missing");
            return;
        }

        // Validate player prefab assignment
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab is not assigned on SpawnManager.");
            return;
        }

        // Ensure we have a network lobby state to pull players from
        var lobbyState = NetworkLobbyState.Instance;
        if (lobbyState == null)
        {
            Debug.LogError("NetworkLobbyState instance not found. Cannot spawn players.");
            return;
        }

        var players = lobbyState.Players;
        // Iterate by index so we can modify entries (e.g. mark alive)
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            // Safety: ensure teamId is assigned. If unassigned (-1), default to team 0
            if (p.teamId < 0)
            {
                p.teamId = 0;
            }
            Transform spawn = spawnPoints.GetRandomSpawn(p.teamId);
            // Instantiate and spawn the player prefab
            SpawnPlayer(ref p, spawn);
            // Update the network list entry with potentially modified player state
            players[i] = p;
        }
    }

    // =========================
    // Internal
    // =========================
    private void SpawnPlayer(ref NetLobbyPlayer player, Transform spawnPoint)
    {
        if (spawnPoint == null)
        {
            Debug.LogError("Invalid spawn point");
            return;
        }

        // Instantiate the player prefab at the spawn location
        var instance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        var networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("Player prefab does not have a NetworkObject component.");
            Destroy(instance);
            return;
        }

        // Spawn the network object with ownership assigned to the appropriate client
        networkObject.SpawnWithOwnership(player.clientId);

        // Mark the lobby player as alive
        player.isAlive = true;
    }
}
