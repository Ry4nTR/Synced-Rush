using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    private MapSpawnPoints spawnPoints;

    private readonly Dictionary<int, List<Transform>> _teamSpawnOrder = new();
    private readonly Dictionary<int, int> _teamSpawnIndex = new();

    [Header("Prefabs")]
    [Tooltip("Networked player prefab to spawn for each lobby member")]
    public GameObject playerPrefab;

    [SerializeField] private NetworkLobbyState lobbyState;
    private void Awake()
    {
        if (lobbyState == null) lobbyState = FindFirstObjectByType<NetworkLobbyState>();
    }

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

        // Setup shuffled spawn order per team on initialization
        SetupTeamSpawnOrder();
    }

    private void SetupTeamSpawnOrder()
    {
        _teamSpawnOrder.Clear();
        _teamSpawnIndex.Clear();

        if (spawnPoints != null)
        {
            // Copy spawn points for Team A
            var aList = new System.Collections.Generic.List<Transform>(spawnPoints.teamASpawns);
            _teamSpawnOrder[0] = aList;
            _teamSpawnIndex[0] = 0;

            // Copy spawn points for Team B
            var bList = new System.Collections.Generic.List<Transform>(spawnPoints.teamBSpawns);
            _teamSpawnOrder[1] = bList;
            _teamSpawnIndex[1] = 0;

            // Shuffle initial order
            ShuffleSpawnList(aList);
            ShuffleSpawnList(bList);
        }
    }

    private void ShuffleSpawnList(System.Collections.Generic.List<Transform> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private Transform GetNextSpawn(int teamId)
    {
        if (!_teamSpawnOrder.TryGetValue(teamId, out var list) || list == null || list.Count == 0)
            return null;
        if (!_teamSpawnIndex.TryGetValue(teamId, out var idx)) idx = 0;
        // Clamp index within range
        if (idx >= list.Count) idx = 0;
        var spawn = list[idx];
        _teamSpawnIndex[teamId] = (idx + 1) % list.Count;
        return spawn;
    }

    private void ShuffleSpawnOrderForNewRound()
    {
        foreach (var kvp in _teamSpawnOrder)
        {
            var list = kvp.Value;
            if (list != null && list.Count > 0)
            {
                ShuffleSpawnList(list);
                _teamSpawnIndex[kvp.Key] = 0;
            }
        }
    }

    // =========================
    // Public API
    // =========================
    public void ResetAllPlayersForRound()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (lobbyState == null) { Debug.LogError("NetworkLobbyState missing"); return; }

        // Shuffle spawn order before assigning spawns this round
        ShuffleSpawnOrderForNewRound();

        var players = lobbyState.Players;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p.teamId < 0) p.teamId = 0;
            // Use the rotated spawn order instead of random selection
            Transform spawn = GetNextSpawn(p.teamId);
            if (spawn == null) continue;

            var client = NetworkManager.Singleton.ConnectedClients[p.clientId];
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            // Reset network-visible state
            p.isAlive = true;
            players[i] = p;

            // Reset gameplay state
            var health = playerObj.GetComponent<HealthSystem>();
            if (health != null) health.Respawn();

            var actor = playerObj.GetComponent<PlayerRoundActor>();
            if (actor != null) actor.ServerSetAliveState(true);

            var move = playerObj.GetComponent<MovementController>();
            if (move != null)
            {
                Vector3 finalPos = spawn.position;

                // Raycast downward to find actual ground
                if (Physics.Raycast(spawn.position + Vector3.up * 2f, Vector3.down,
                    out RaycastHit hit, 10f, LayerMask.GetMask("Default", "Ground")))
                {
                    finalPos = hit.point;
                }

                move.ServerResetForNewRound(finalPos, spawn.rotation);
            }

            var wc = playerObj.GetComponentInChildren<WeaponController>(true);
            if (wc != null) wc.ResetForNewRound();

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

        if (lobbyState == null)
        {
            Debug.LogError("NetworkLobbyState instance not found. Cannot spawn players.");
            return;
        }

        var players = lobbyState.Players;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p.teamId < 0) p.teamId = 0;

            // ✅ If already spawned, DO NOT spawn again.
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(p.clientId, out var client) &&
                client.PlayerObject != null)
            {
                // Optional: you can also reposition here if you want.
                continue;
            }
            // Use the rotated spawn order instead of random selection
            Transform spawn = GetNextSpawn(p.teamId);
            if (spawn == null)
            {
                Debug.LogError($"No spawn point for team {p.teamId}");
                continue;
            }

            SpawnPlayer(ref p, spawn);
            players[i] = p;
        }
    }

    public void ServerHardStopAllPlayers()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (lobbyState == null) return;

        var players = lobbyState.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(p.clientId, out var client)) continue;
            var po = client.PlayerObject;
            if (po == null) continue;

            var mc = po.GetComponent<MovementController>();
            if (mc != null)
            {
                // This is PER-PLAYER and server-only; it doesn't replace MatchFlowState gating.
                mc.ServerSetGameplayEnabled(false);
            }
        }
    }

    // =========================
    // Internal
    // =========================
    private void SpawnPlayer(ref NetLobbyPlayer player, Transform spawnPoint)
    {
        var instance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

        instance.transform.position = SnapRootToGround(instance, spawnPoint.position);

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

    private Vector3 SnapRootToGround(GameObject instance, Vector3 desiredPos, float rayStartUp = 2f, float rayDistance = 10f)
    {
        // If you have a ground-only layer, use it here. Otherwise, keep Default/Ground.
        int mask = LayerMask.GetMask("Default", "Ground");

        Vector3 rayStart = desiredPos + Vector3.up * rayStartUp;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance, mask, QueryTriggerInteraction.Ignore))
        {
            // If the prefab has a CharacterController, align its FEET to the hit point
            var cc = instance.GetComponent<CharacterController>();
            if (cc != null)
            {
                float feetOffset = (cc.height * 0.5f) - cc.center.y;
                float y = hit.point.y + feetOffset + cc.skinWidth;
                return new Vector3(desiredPos.x, y, desiredPos.z);
            }

            // Fallback: just put pivot on the ground (useful if pivot is already at feet)
            return new Vector3(desiredPos.x, hit.point.y, desiredPos.z);
        }

        // If no ground hit, keep original
        return desiredPos;
    }

    public void ServerSetAllGameplayEnabled(bool enabled)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (lobbyState == null) return;

        var players = lobbyState.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(p.clientId, out var client)) continue;
            var po = client.PlayerObject;
            if (po == null) continue;

            var mc = po.GetComponent<MovementController>();
            if (mc != null) mc.ServerSetGameplayEnabled(enabled);
        }
    }

    public void ServerSnapAllToGround()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (lobbyState == null) return;

        var players = lobbyState.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(p.clientId, out var client)) continue;
            var po = client.PlayerObject;
            if (po == null) continue;

            var mc = po.GetComponent<MovementController>();
            if (mc != null) mc.ServerSnapToGround();
        }
    }

}
