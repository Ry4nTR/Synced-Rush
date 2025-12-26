using UnityEngine;

public enum BodyPartType
{
    Head,
    Chest,
    Arms,
    Hands,
    Legs,
    Feet
}

/// <summary>
/// Attached to body-part colliders. Defines damage multiplier.
/// </summary>
public class Hitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    public BodyPartType bodyPart;
    public float damageMultiplier = 1f;

    // Cached reference to the owning HealthSystem
    private HealthSystem healthSystem;

    private void Awake()
    {
        healthSystem = GetComponentInParent<HealthSystem>();
    }

    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }
}
