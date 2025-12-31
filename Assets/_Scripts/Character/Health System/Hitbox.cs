using UnityEngine;

/// <summary>
/// Types of body parts for hitboxes.
/// </summary>
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

    private HealthSystem healthSystem;

    private void Awake()
    {
        healthSystem = GetComponentInParent<HealthSystem>();
    }

    //returns the HealthSystem of the player this hitbox belongs to
    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }
}
