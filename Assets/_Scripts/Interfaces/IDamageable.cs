/// <summary>
/// Interface for any object that can take damage.
/// </summary>
public interface IDamageable
{
    // Applies amount of damage
    void TakeDamage(float amount, ulong instigatorClientId);
    // Handles death logic
    void Die();
    //Handles respawn logic
    void Respawn();
}