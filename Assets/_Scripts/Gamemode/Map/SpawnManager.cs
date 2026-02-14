using UnityEngine;
using Unity.Netcode;

public class SpawnManager : MonoBehaviour
{
    private MapSpawnPoints spawnPoints;

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
    }

    // =========================
    // Public API
    // =========================
    public void ResetAllPlayersForRound()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (lobbyState == null) { Debug.LogError("NetworkLobbyState missing"); return; }

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
