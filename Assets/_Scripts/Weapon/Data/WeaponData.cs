using System;
using UnityEngine;

/// <summary>
/// SO containing all data of a weapon.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
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

    [Header("Models")]
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