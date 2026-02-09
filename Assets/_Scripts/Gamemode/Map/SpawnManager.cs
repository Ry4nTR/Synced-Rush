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
    public void ResetAllPlayersForRound()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var lobbyState = NetworkLobbyState.Instance;
        if (lobbyState == null) return;

        var players = lobbyState.Players;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p.teamId < 0) p.teamId = 0;

            Transform spawn = spawnPoints.GetRandomSpawn(p.teamId);
            if (spawn == null) continue;

            var client = NetworkManager.Singleton.ConnectedClients[p.clientId];
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            // Reset network-visible state
            p.isAlive = true;
            players[i] = p;

            // Reset gameplay state
            var health = playerObj.GetComponent<HealthSystem>();
            if (health != null) health.Respawn(); // server only

            var move = playerObj.GetComponent<MovementController>();
            if (move != null)
            {
                move.ServerResetForNewRound(spawn.position, spawn.rotation);
            }

            // If you have weapon reset, call it here too
            // var weapon = playerObj.GetComponent<...>();
            // weapon.ServerResetForNewRound();
        }
    }

    public void SpawnAllPlayers()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (spawnPoints == null)
        {
            Debug.LogError("SpawnManager not initialised: spawnPoints missing");
            return;
        }

        var lobbyState = NetworkLobbyState.Instance;
        if (lobbyState == null)
        {
            Debug.LogError("NetworkLobbyState instance not found. Cannot spawn players.");
            return;
        }

        var players = lobbyState.Players;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];

            if (p.teamId < 0)
                p.teamId = 0;

            Transform spawn = spawnPoints.GetRandomSpawn(p.teamId);
            if (spawn == null)
            {
                Debug.LogError($"No spawn point for team {p.teamId}");
                continue;
            }

            SpawnPlayer(ref p, spawn);
            players[i] = p;
        }
    }

    // =========================
    // Internal
    // =========================
    private void SpawnPlayer(ref NetLobbyPlayer player, Transform spawnPoint)
    {
        var instance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("Player prefab does not have a NetworkObject component.");
            Destroy(instance);
            return;
        }

        netObj.SpawnAsPlayerObject(player.clientId, true);

        player.isAlive = true;

        Debug.Log($"[SpawnManager] Spawned player clientId={player.clientId} netId={netObj.NetworkObjectId}");
    }
}
