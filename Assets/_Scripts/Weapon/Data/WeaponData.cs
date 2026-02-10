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

    [Header("Damage Falloff")]
    public float falloffStartDistance; // Distance at which damage falloff begins
    public float falloffEndDistance; // Distance at which damage falloff ends
    public float falloffDistancePerStep; // Distance per damage reduction step
    public float damageReductionPerStep; // Damage reduction per step
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

    [Header("Animation (Network Sync)")]
    [Tooltip("Index in FullBodyNetworkAnimatorSync.fullBodyControllers[] (same order on all clients). 0 = default.")]
    public ushort fullBodyControllerIndex = 0;

    [Header("Animation")]
    public AnimatorOverrideController armsAnimatorOverride;
    public AnimatorOverrideController fullBodyAnimatorOverride;
    public AnimatorOverrideController weaponAnimatorOverride;
    public AnimatorOverrideController worldWeaponAnimatorOverride;
    public float recoilWeight = 1f;
    public float aimedRecoilMultiplier = 0.6f;
    public float aimBlendSpeed = 12f;


    [Header("Models & ModelParts")]
    public GameObject worldModelPrefab; // Third-person model prefab

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

        // Stepped falloff calculation
        if (falloffDistancePerStep > 0f)
        {
            // Calculate how many steps beyond falloffStartDistance
            float distanceBeyondStart = distance - falloffStartDistance;
            int steps = Mathf.FloorToInt(distanceBeyondStart / falloffDistancePerStep);

            // Calculate damage reduction
            float damageReduction = steps * damageReductionPerStep;
            float steppedDamage = damage - damageReduction;

            // Ensure damage doesn't go below minimum
            return Mathf.Max(steppedDamage, minimumDamage);
        }

        // Linear interpolation between damage and minimumDamage
        float t = Mathf.InverseLerp(
            falloffStartDistance,
            falloffEndDistance,
            distance
        );

        return Mathf.Lerp(damage, minimumDamage, t);
    }

    // Applies spread to a given direction vector.
    public Vector3 ApplySpread(Vector3 direction, float spreadDeg)
    {
        direction = direction.normalized;
        if (spreadDeg <= 0f)
            return direction;

        // Build an orthonormal basis around the direction
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(Vector3.forward, direction);
        right.Normalize();

        Vector3 up = Vector3.Cross(direction, right).normalized;

        // Convert cone half-angle to radians
        float maxAngleRad = Mathf.Deg2Rad * Mathf.Clamp(spreadDeg, 0f, 89.9f);

        // Center-heavy distribution:
        // r in [0..1] with sqrt makes points denser near center (common shooter feel)
        float phi = Random.Range(0f, 2f * Mathf.PI);
        float r = Mathf.Sqrt(Random.value);

        // Turn r into an angle inside the cone
        float theta = r * maxAngleRad;               // 0..maxAngleRad
        float sinTheta = Mathf.Sin(theta);
        float cosTheta = Mathf.Cos(theta);

        // Offset direction inside cone
        Vector3 lateral = (right * Mathf.Cos(phi) + up * Mathf.Sin(phi)) * sinTheta;

        // Final direction
        Vector3 result = direction * cosTheta + lateral;
        return result.normalized;
    }

}