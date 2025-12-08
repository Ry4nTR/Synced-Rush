using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Component that tracks and synchronizes a player's or object's health. It implements
/// IDamageable so that other systems can apply damage. Health is authoritative on
/// the server and synchronized to clients via a NetworkVariable.
/// </summary>
public class HealthSystem : NetworkBehaviour, IDamageable
{
    [Header("Health Settings")]
    public float maxHealth = 100f;

    // Current health synchronized across the network. Only the server should modify this.
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>();

    private void OnEnable()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    /// <inheritdoc/>
    public void TakeDamage(float amount, ulong instigatorClientId)
    {
        if (!IsServer)
        {
            // Only the server should process damage
            return;
        }
        // Reduce health and clamp to zero
        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);
        // If health reaches zero, handle death logic
        if (currentHealth.Value <= 0f)
        {
            HandleDeath(instigatorClientId);
        }
    }

    /// <summary>
    /// Called on the server when health drops to zero. Override this method to implement
    /// custom death behaviour (respawn, ragdoll, disable object, etc.).
    /// </summary>
    /// <param name="instigatorClientId">The client ID of the player who caused the death.</param>
    protected virtual void HandleDeath(ulong instigatorClientId)
    {
        // Placeholder: disable the object on the server
        // In a real game you might respawn the player or despawn the object
        gameObject.SetActive(false);
    }
}