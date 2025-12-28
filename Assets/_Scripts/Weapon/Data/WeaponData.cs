using System;
using UnityEngine;

/// <summary>
/// ScriptableObject containing all of the static data for a weapon.
/// This data does not change at runtime and therefore is not synchronized
/// over the network. Each client must have an identical copy of this asset.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Identification")]
    public string weaponName;
    public WeaponType weaponType; // AR, SMG, Sniper, Pistol, etc.
    public int weaponID; // Unique ID used for network synchronization
    public LayerMask layerMask; // Layers that can be hit by this weapon

    [Header("Combat Stats")]
    public float damage; // Base damage per shot
    public float fireRate; // Shots per second
    public float range; // Maximum effective range
    public float criticalMultiplier = 1.5f; // Multiplier for critical hits

    [Header("Damage Falloff")]
    /// <summary>
    /// Distance in meters from the shooter at which damage begins to fall off.
    /// Up to this distance the weapon deals its full base damage.
    /// </summary>
    public float falloffStartDistance;

    /// <summary>
    /// Distance in meters at which damage falloff reaches its minimum value.
    /// Beyond this distance the weapon will not deal less than minimumDamage.
    /// </summary>
    public float falloffEndDistance;

    /// <summary>
    /// The minimum damage this weapon can deal at extreme range. This value is
    /// applied once the target is beyond falloffEndDistance.
    /// </summary>
    public float minimumDamage;

    [Header("Ammo")]
    public int magazineSize; // Number of bullets per magazine
    public int maxAmmo; // Maximum ammo reserve
    public float reloadTime; // Time required to reload in seconds

    [Header("Accuracy")]
    public float baseSpread; // Base spread when idle/walking
    public float sprintSpread; // Spread when sprinting
    public float jumpSpread; // Spread when jumping
    public float crouchSpreadMultiplier = 0.7f; // Multiplier applied to spread while crouching
    public float aimSpreadMultiplier = 0.5f; // Multiplier applied to spread while aiming
    public float spreadRecoveryRate; // How quickly the spread recovers over time
    public float spreadIncreasePerShot; // Spread added with each shot
    public float maxSpread; // Maximum spread value

    [Header("Recoil")]
    public Vector2 recoilPattern; // X = horizontal recoil, Y = vertical recoil
    public float recoilRecoverySpeed; // Speed at which recoil recovers

    [Header("Visual & Audio")]
    public GameObject weaponPrefab; // Prefab for the weapon (client only)
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

    [Header("Models")]
    public GameObject viewModelPrefab; // First-person model prefab
    public GameObject worldModelPrefab; // Third-person model prefab
}

/// <summary>
/// Enumeration of possible weapon categories. Additional types can be added as needed.
/// </summary>
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