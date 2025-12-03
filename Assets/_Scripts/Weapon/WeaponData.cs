using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Weapons/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("General Info")]

    [Tooltip("Name of the weapon. Used for UI, logging, etc.")]
    public string weaponName;

    [Tooltip("Type/category of the weapon (Rifle, SMG, etc.).")]
    public WeaponType weaponType;

    [Tooltip("First-person weapon model, attached to the FP Arms rig. Only visible to the local player.")]
    public GameObject viewModelPrefab;

    [Tooltip("Third-person weapon model, attached to the character’s hand bone. Visible to all other players.")]
    public GameObject worldModelPrefab;


    [Header("Base Stats")]

    [Tooltip("Base damage dealt on body shots.")]
    public float damage = 20f;

    [Tooltip("Multiplier applied when hitting the head hitbox.")]
    public float headshotMultiplier = 2.0f;

    [Tooltip("Time between shots in seconds (1 / rounds per minute).")]
    public float fireRate = 0.1f;

    [Tooltip("Maximum hitscan distance.")]
    public float maxDistance = 300f;

    [Tooltip("LayerMask used for hitscan raycast (Players, World, etc.).")]
    public LayerMask hitMask;


    [Header("Ammo & Reload")]

    [Tooltip("Number of bullets per magazine.")]
    public int magazineSize = 30;

    [Tooltip("Maximum ammo that the player can carry outside the magazine.")]
    public int maxReserveAmmo = 90;

    [Tooltip("Time needed to complete a reload.")]
    public float reloadTime = 1.6f;


    [Header("Spread & Recoil")]

    [Tooltip("Bullet spread (in degrees) when not aiming.")]
    public float hipfireSpread = 1.2f;

    [Tooltip("Bullet spread (in degrees) when aiming down sights.")]
    public float aimSpread = 0.3f;

    [Tooltip("Amount of recoil added per shot.")]
    public float recoilPerShot = 1.5f;

    [Tooltip("Speed at which recoil returns to neutral.")]
    public float recoilRecovery = 8f;


    [Header("FX & Audio")]

    [Tooltip("Prefab for the muzzle flash effect (local player only).")]
    public ParticleSystem muzzleFlashPrefab;

    [Tooltip("Prefab spawned when a bullet hits a surface.")]
    public GameObject hitEffectPrefab;

    [Tooltip("Sound played when firing.")]
    public AudioClip fireSFX;

    [Tooltip("Sound played when reloading.")]
    public AudioClip reloadSFX;


    [Header("Camera Shake")]

    [Tooltip("Intensity of the camera shake when firing.")]
    public float shakeIntensity = 0.2f;

    [Tooltip("Duration of the camera shake.")]
    public float shakeDuration = 0.08f;
}

public enum WeaponType
{
    Pistol,
    Rifle,
    Shotgun,
    SMG
}
