using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Server-authoritative health system.
/// </summary>
public class HealthSystem : NetworkBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public bool allowDamageFromOwner = false;

    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private ulong _lastInstigatorClientId = ulong.MaxValue;

    private RoundDeathTracker _deathTracker;
    private RoundManager _roundManager;

    public float CurrentHealth => currentHealth.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _deathTracker = FindAnyObjectByType<RoundDeathTracker>();
        _roundManager = FindFirstObjectByType<RoundManager>();

        if (IsServer && currentHealth.Value <= 0f)
        {
            currentHealth.Value = maxHealth;
        }
    }

    public void TakeDamage(float amount, ulong instigatorClientId)
    {
        if (!IsServer)
            return;

        if (!allowDamageFromOwner && instigatorClientId == OwnerClientId)
            return;

        if (currentHealth.Value <= 0f) return;

        _lastInstigatorClientId = instigatorClientId;

        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);

        // === NEW: send attacker position to victim owner client ===
        Vector3 attackerPos = ResolveAttackerPosition(instigatorClientId);

        var targetParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId } // victim owner client only
            }
        };
        DamageIndicatorClientRpc(attackerPos, targetParams);

        if (currentHealth.Value <= 0f)
            Die();
    }


    [ClientRpc]
    private void DamageIndicatorClientRpc(Vector3 attackerPosition, ClientRpcParams rpcParams = default)
    {
        // This runs on the victim client only (targeted).
        // Find local HUD and call the indicator.
        var hud = FindAnyObjectByType<PlayerHUD>();
        if (hud == null) return;

        // This HealthSystem belongs to the local player only if OwnerClientId == LocalClientId,
        // but since we target only the owner client, that's true in practice.
        var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer == null) return;

        hud.ShowDamageIndicator(attackerPosition, localPlayer.transform);
    }

    public void Die()
    {
        if (!IsServer) return;

        Debug.Log($"Player {OwnerClientId} died");

        if (_deathTracker != null)
            _deathTracker.NotifyPlayerDeath(OwnerClientId, _lastInstigatorClientId);

        _roundManager?.ServerNotifyVictimDeathForKillCam(OwnerClientId, _lastInstigatorClientId);

        var actor = GetComponent<PlayerRoundActor>();
        if (actor != null)
            actor.ServerSetAliveState(false);

        var move = GetComponent<MovementController>();
        if (move != null) move.ServerSetGameplayEnabled(false);
    }

    public void Respawn()
    {
        if (!IsServer) return;

        currentHealth.Value = maxHealth;

        var actor = GetComponent<PlayerRoundActor>();
        if (actor != null)
            actor.ServerSetAliveState(true);

        var move = GetComponent<MovementController>();
        if (move != null) move.ServerSetGameplayEnabled(true);
    }

    // =========================
    // Helper methods
    // =========================
    private Vector3 ResolveAttackerPosition(ulong instigatorClientId)
    {
        // Best-case: get the instigator's player object on server
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.ConnectedClients.TryGetValue(instigatorClientId, out var client)
            && client.PlayerObject != null)
        {
            return client.PlayerObject.transform.position;
        }

        return Vector3.zero; // fallback
    }

}
