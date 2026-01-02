using UnityEngine;

// Enumeration of possible weapon categories. Additional types can be added as needed.
public enum WeaponType
{
    AssaultRifle,
    SubmachineGun,
    SniperRifle,
    Pistol,
    Shotgun,
    LightMachineGun,
    Melee,
    GrenadeLauncher
}

/// <summary>
/// SO containing all data of a weapon and helper methods for shooting systems.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    // =========================
    // WEAPON PROPERTIES
    // =========================

    [Header("Identification")]
    public string weaponName;
    public WeaponType weaponType; // AssaultRifle, SubmachineGun, SniperRifle, etc.
    public int weaponID; // Unique ID used for network synchronization
    public LayerMask layerMask; // Layers that this weapon can hit

    [Header("Combat Stats")]
    public float damage; // Base damage per shot
    public float fireRate; // Shots per second
    public float range; // Maximum effective range
    public float criticalMultiplier = 1.5f; // Multiplier for critical hits

    [Header("Damage Falloff")]
    public float falloffStartDistance; // Distance at which damage falloff begins
    public float falloffEndDistance; // Distance at which damage falloff ends
    public float minimumDamage; // Minimum damage at maximum range

    [Header("Ammo")]
    public int magazineSize; // Magazine capacity
    public int ammoReserve; // Ammo reserve capacity
    public float reloadTime; // Time required to reload (in seconds)

    [Header("Accuracy")]
    public float baseSpread; // Spread when idle/walking
    public float jumpSpread; // Spread when jumping
    public float crouchSpreadMultiplier = 0.7f; // Spread multiplier when crouching
    public float aimSpreadMultiplier = 0.5f; // Spread multiplier when aiming
    public float spreadRecoveryRate; // Spread recovery over time
    public float spreadIncreasePerShot; // Spread increase with each shot
    public float maxSpread; // Maximum spread

    [Header("Visual & Audio")]
    public GameObject weaponPrefab; // Prefab of the weapon
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;
    public GameObject muzzleFlashPrefab;
    public GameObject bulletTracerPrefab;
    public GameObject impactEffectPrefab;

    [Header("Animation")]
    public float aimSpeed; // Speed of transition to aiming
    public string shootAnimationTrigger = "Shoot";
    public string reloadAnimationTrigger = "Reload";
    public string aimAnimationBool = "IsAiming";
    public string walkAnimationBool = "IsWalking";
    public string sprintAnimationBool = "IsSprinting";

    [Header("Models & ModelParts")]
    public GameObject worldModelPrefab; // Third-person model prefab
    public GameObject Muzzle; // Muzzle transform for effects (TO TEST AND SOLVE)

    // =========================
    // HELPER METHODS
    // =========================

    // Calculates the damage based on the distance to the target.
    public float CalculateDamageByDistance(float distance)
    {
        // Clamp negative or zero distances
        if (distance <= 0f)
            return damage;

        // No falloff before start distance
        if (distance <= falloffStartDistance)
            return damage;

        // Fully fallen off after end distance
        if (distance >= falloffEndDistance)
            return minimumDamage;

        // Linear interpolation between damage and minimumDamage
        float t = Mathf.InverseLerp(
            falloffStartDistance,
            falloffEndDistance,
            distance
        );

        return Mathf.Lerp(damage, minimumDamage, t);
    }

    // Applies spread to a given direction vector.
    public Vector3 ApplySpread(Vector3 direction, float spread)
    {
        if (spread <= 0f)
            return direction;

        // Calculate spread in radians for more intuitive control
        float spreadRad = spread * Mathf.Deg2Rad;

        // Generate random angles with normal distribution for better "center-heavy" spread
        float angle = Random.Range(0f, 2f * Mathf.PI); // Random direction around circle
        float distance = Mathf.Sqrt(Random.Range(0f, 1f)) * spreadRad; // Square root for uniform disk distribution

        // Calculate offsets
        float x = Mathf.Sin(angle) * distance;
        float y = Mathf.Cos(angle) * distance;

        // Apply spread as rotation
        Quaternion spreadRotation = Quaternion.Euler(x * Mathf.Rad2Deg, y * Mathf.Rad2Deg, 0f);
        return spreadRotation * direction;
    }
}