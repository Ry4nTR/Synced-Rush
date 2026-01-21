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

    private RoundDeathTracker deathTracker;

    public float CurrentHealth => currentHealth.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        deathTracker = FindAnyObjectByType<RoundDeathTracker>();

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
        if (!IsServer)
            return;

        // Log the owner client ID instead of requiring a PlayerCombatIdentity component.  
        Debug.Log($"Player {OwnerClientId} died");

        if (deathTracker != null)
        {
            deathTracker.NotifyPlayerDeath(OwnerClientId);
        }

        DisableGameplayClientRpc();

        // Disable player for rest of round
        gameObject.SetActive(false);
    }

    public void Respawn()
    {
        if (!IsServer)
            return;

        currentHealth.Value = maxHealth;
        gameObject.SetActive(true);
    }

    [ClientRpc]
    private void DisableGameplayClientRpc()
    {
        var switcher = GetComponent<ClientComponentSwitcher>();
        if (switcher != null)
        {
            // When the player dies, disable gameplay inputs and allow UI interaction
            switcher.SetState_Loadout();
        }
    }
}
