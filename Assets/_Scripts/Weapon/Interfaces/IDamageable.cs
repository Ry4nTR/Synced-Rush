/// <summary>
/// Interface for any object that can take damage. Implementing classes
/// should handle reducing health and death behaviour on the server.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Applies damage to the object. This should only be called on the server.
    /// </summary>
    /// <param name="amount">The amount of damage to apply.</param>
    /// <param name="instigatorClientId">The client ID of the player who caused the damage.</param>
    void TakeDamage(float amount, ulong instigatorClientId);
}