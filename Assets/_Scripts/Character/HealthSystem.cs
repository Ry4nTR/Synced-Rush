using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Server-authoritative health component.
/// NetworkVariable writes happen ONLY after NGO registration.
/// </summary>
public class HealthSystem : NetworkBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;

    // Server writes, everyone reads
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Initialize ONLY ONCE, ONLY ON SERVER, ONLY AFTER SPAWN
        if (IsServer && currentHealth.Value <= 0f)
        {
            currentHealth.Value = maxHealth;
        }
    }

    public void TakeDamage(float amount, ulong instigatorClientId)
    {
        if (!IsServer)
            return;

        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);

        if (currentHealth.Value <= 0f)
        {
            HandleDeath(instigatorClientId);
        }
    }

    protected virtual void HandleDeath(ulong instigatorClientId)
    {
        // Example logic
        gameObject.SetActive(false);
    }
}
