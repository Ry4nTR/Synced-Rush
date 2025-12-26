using System.Collections;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Component responsible for managing the runtime state of a weapon on a player.
/// It handles local ammo counts, spread, firing cooldowns, reloading and aiming.
/// Networking is performed through the WeaponNetworkHandler – this class does
/// not directly communicate with the server.
/// </summary>
public class WeaponController : MonoBehaviour
{
    // References to other systems
    [Tooltip("Static data asset for this weapon")]
    public WeaponData weaponData;
    private ShootingSystem shootingSystem;
    private WeaponNetworkHandler networkHandler;

    // Local state (not synced)
    private int currentAmmo;
    private int reserveAmmo;
    private float currentSpread;
    private bool isReloading;
    private bool isAiming;
    private float nextFireTime;

    // Cached references
    private Transform cameraTransform;
    private Animator weaponAnimator;

    /// <summary>
    /// Gets the current spread value, taking aiming into account.
    /// </summary>
    public float CurrentSpread => CalculateCurrentSpread();

    /// <summary>
    /// Returns true if the weapon can currently fire.
    /// </summary>
    public bool CanShoot => !isReloading && currentAmmo > 0 && Time.time >= nextFireTime;

    /// <summary>
    /// Exposes current ammo.
    /// </summary>
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;


    private void Awake()
    {
        shootingSystem = GetComponent<ShootingSystem>();
        networkHandler = GetComponent<WeaponNetworkHandler>();
        // Attempt to locate the player's camera – this might need to be set explicitly
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cameraTransform = cam.transform;
        }
        // Optionally find an animator on the child weapon model
        weaponAnimator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// Initializes the weapon controller with the given weapon data.
    /// Sets up ammo counts, spread and cooldown.
    /// </summary>
    /// <param name="data">The static data describing the weapon.</param>
    public void Initialize(WeaponData data)
    {
        weaponData = data;
        currentAmmo = weaponData.magazineSize;
        reserveAmmo = weaponData.maxAmmo;
        currentSpread = weaponData.baseSpread;
        nextFireTime = 0f;
        isReloading = false;
        isAiming = false;
    }

    /// <summary>
    /// Called by input to request a shot. Performs local validation, fires the weapon
    /// locally and notifies the network handler.
    /// </summary>
    public void RequestFire()
    {
        if (!CanShoot)
        {
            return;
        }


        // Update the next allowed fire time based on fire rate (shots per second)
        nextFireTime = Time.time + (1f / weaponData.fireRate);

        // Determine origin/direction based on camera or this transform
        Vector3 origin = cameraTransform != null ? cameraTransform.position : transform.position;
        Vector3 direction = cameraTransform != null ? cameraTransform.forward : transform.forward;

        // Perform the actual shot locally for immediate feedback
        shootingSystem?.PerformShoot(origin, direction, CurrentSpread, weaponData.weaponID);

        // Consume ammo and increase spread
        ConsumeAmmo();
        IncreaseSpread();

        // Notify the network handler about the shot
        networkHandler?.NotifyShot(origin, direction, currentSpread);
    }

    /// <summary>
    /// Begins the reloading process if possible.
    /// </summary>
    public void Reload()
    {
        if (isReloading || currentAmmo >= weaponData.magazineSize || reserveAmmo <= 0)
            return;

        StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        // Play reload animation and sound if available
        if (weaponAnimator != null && !string.IsNullOrEmpty(weaponData.reloadAnimationTrigger))
        {
            weaponAnimator.SetTrigger(weaponData.reloadAnimationTrigger);
        }
        // Wait for the reload time
        yield return new WaitForSeconds(weaponData.reloadTime);
        // Calculate how much ammo to load into the magazine
        int needed = weaponData.magazineSize - currentAmmo;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        currentAmmo += toLoad;
        reserveAmmo -= toLoad;

        // Infinite ammo(TESTING) <---------------------------------------------------------------------------------------------------------------------------------------------------------------------
        reserveAmmo = weaponData.maxAmmo;

        isReloading = false;
    }

    /// <summary>
    /// Sets the aiming state. When aiming, spread is reduced via aimSpreadMultiplier.
    /// Additional handling such as adjusting camera FOV can be added here.
    /// </summary>
    /// <param name="aiming">True when the player is aiming down sights.</param>
    public void SetAiming(bool aiming)
    {
        isAiming = aiming;
    }

    /// <summary>
    /// Calculates the current spread including aiming modifiers.
    /// </summary>
    /// <returns>The spread value to apply to a shot.</returns>
    private float CalculateCurrentSpread()
    {
        // no spread (TESTING)<---------------------------------------------------------------------------------------------------------------------------------------------------------------------
        return 0f;

        float spread = currentSpread;
        if (isAiming)
        {
            spread *= weaponData.aimSpreadMultiplier;
        }
        // Ensure spread stays within bounds
        return Mathf.Clamp(spread, weaponData.baseSpread, weaponData.maxSpread);
    }

    /// <summary>
    /// Increases spread after firing a shot.
    /// </summary>
    private void IncreaseSpread()
    {
        currentSpread = Mathf.Min(currentSpread + weaponData.spreadIncreasePerShot, weaponData.maxSpread);
    }

    /// <summary>
    /// Gradually recovers spread back to the base spread over time.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame.</param>
    private void RecoverSpread(float deltaTime)
    {
        currentSpread = Mathf.Max(weaponData.baseSpread,
                                  currentSpread - weaponData.spreadRecoveryRate * deltaTime);
    }

    private void Update()
    {
        // Recover spread each frame
        if (weaponData != null)
        {
            RecoverSpread(Time.deltaTime);
        }
    }

    /// <summary>
    /// Consumes a single bullet from the magazine and triggers an automatic reload
    /// if the magazine becomes empty and there is reserve ammo available.
    /// </summary>
    private void ConsumeAmmo()
    {
        currentAmmo--;
        if (currentAmmo <= 0 && reserveAmmo > 0)
        {
            Reload();
        }
    }

    /// <summary>
    /// Calculates the damage to apply based on the distance between shooter and target.
    /// Implements a linear falloff between falloffStartDistance and falloffEndDistance.
    /// </summary>
    /// <param name="distance">The distance from the shooter to the hit point in meters.</param>
    /// <returns>The damage value after falloff is applied.</returns>
    public float CalculateDamageByDistance(float distance)
    {
        if (weaponData == null)
            return 0f;
        // No falloff if end distance is not set or start/end reversed
        if (weaponData.falloffEndDistance <= weaponData.falloffStartDistance)
            return weaponData.damage;
        // Full damage inside the falloff start range
        if (distance <= weaponData.falloffStartDistance)
            return weaponData.damage;
        // Minimum damage beyond the falloff end range
        if (distance >= weaponData.falloffEndDistance)
            return weaponData.minimumDamage;
        // Linearly interpolate between base damage and minimum damage
        float t = (distance - weaponData.falloffStartDistance) /
                  (weaponData.falloffEndDistance - weaponData.falloffStartDistance);
        return Mathf.Lerp(weaponData.damage, weaponData.minimumDamage, t);
    }
}