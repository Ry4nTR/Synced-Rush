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

    public float CurrentHealth => currentHealth.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

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

        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);

        if (currentHealth.Value <= 0f)
            Die();
    }


    public void Die()
    {
        //Debug.Log("Player died");

        Respawn();
    }

    public void Respawn()
    {
        if (!IsServer)
            return;

        currentHealth.Value = maxHealth;

        //Debug.Log("Player respawned");
    }
}
