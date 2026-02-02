using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]

/// <summary>
/// Manages the runtime state of a weapon on a player.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Tooltip("Static data asset for this weapon")]
    public WeaponData weaponData;

    private ShootingSystem shootingSystem;
    private WeaponNetworkHandler networkHandler;
    private Transform fireOrigin;
    private PlayerAnimationController playerAnimationController;
    private Animator Animator;

    private int currentAmmo;
    private int reserveAmmo;
    private float currentSpread;
    private bool isReloading;
    private bool isAiming;
    private float nextFireTime;
    private float aimWeight;
    private float aimTarget;

    // Helper properties
    public float CurrentSpread => CalculateCurrentSpread();
    public bool CanShoot => !isReloading && currentAmmo > 0 && Time.time >= nextFireTime;
    public bool IsAiming => isAiming;

    // Public getters for HUD
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;

    private void Awake()
    {
        shootingSystem = GetComponent<ShootingSystem>();
        networkHandler = GetComponentInParent<WeaponNetworkHandler>();
        playerAnimationController = GetComponentInParent<PlayerAnimationController>();
        Animator = GetComponent<Animator>();

        AssignFireOrigin();
    }

    private void Update()
    {
        RecoverSpread(Time.deltaTime);

        CalculateAimWeight();
    }

    // Initializes the weapon data
    public void Initialize(WeaponData data)
    {
        weaponData = data;
        currentAmmo = weaponData.magazineSize;
        reserveAmmo = weaponData.ammoReserve;
        currentSpread = weaponData.baseSpread;
        nextFireTime = 0f;
        isReloading = false;
        isAiming = false;
    }

    // =========================
    // BASIC WEAPON ACTIONS
    // =========================
    // Performs local validation, fires the weapon locally and notifies the network about the shot.
    public void RequestFire()
    {
        if (!CanShoot) return;

        nextFireTime = Time.time + (1f / weaponData.fireRate);

        Vector3 origin = fireOrigin.position;
        Vector3 baseDirection = fireOrigin.forward;

        // 1. Calculate the FINAL direction with spread here on the Client
        float currentSpread = CurrentSpread;
        Vector3 finalDirection = weaponData.ApplySpread(baseDirection, currentSpread);

        // 2. Perform visual shoot (Tracer/Muzzle) using this EXACT direction
        shootingSystem?.PerformShoot(origin, finalDirection, weaponData.range);

        // 3. Notify Network - Send the FINAL direction, not the spread value
        // We pass 'currentSpread' only for server-side validation if needed, 
        // but we USE finalDirection for the hit.
        networkHandler?.NotifyShot(origin, finalDirection);

        ConsumeAmmo();
        IncreaseSpread();
    }

    public void SetAiming(bool aiming)
    {
        aimTarget = aiming ? 1f : 0f;
    }

    public void CalculateAimWeight()
    {
        aimWeight = Mathf.MoveTowards(
        aimWeight,
        aimTarget,
        weaponData.aimBlendSpeed * Time.deltaTime
        );

        playerAnimationController.SetAimWeight(aimWeight);
    }

    // Starts reloading if possible
    public void Reload()
    {
        if (isReloading || currentAmmo >= weaponData.magazineSize || reserveAmmo <= 0)
            return;

        // Play reload animation if available
        playerAnimationController.Reload();

        StartCoroutine(ReloadCoroutine());
    }

    // Reloading coroutine
    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;

        // Wait for the reload time (VEDI SE PUOI USARE UN EVENTO ALL'INTERNO DELL'ANIMAZIONE)
        yield return new WaitForSeconds(weaponData.reloadTime);

        // Ammo calculations
        int needed = weaponData.magazineSize - currentAmmo;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        currentAmmo += toLoad;
        reserveAmmo -= toLoad;

        // Infinite ammo (TESTING) <------------------------------------------------------------------------
        reserveAmmo = weaponData.ammoReserve;

        isReloading = false;
    }


    // =========================
    // SPREAD MANAGEMENT
    // =========================
    // Calculates the current spread including aiming modifiers.
    private float CalculateCurrentSpread()
    {
        float spread = currentSpread;

        if (isAiming)
        {
            spread *= weaponData.aimSpreadMultiplier;
        }

        // Ensure spread stays within bounds
        return Mathf.Clamp(spread, weaponData.baseSpread, weaponData.maxSpread);
    }

    // Increases spread after firing a shot.
    private void IncreaseSpread()
    {
        currentSpread = Mathf.Min(currentSpread + weaponData.spreadIncreasePerShot, weaponData.maxSpread);
    }

    // Gradually recovers spread back to the base spread over time.
    private void RecoverSpread(float deltaTime)
    {
        currentSpread = Mathf.Max(weaponData.baseSpread, currentSpread - weaponData.spreadRecoveryRate * deltaTime);
    }


    // =========================
    // AMMO MANAGEMENT
    // =========================
    // Consumes bullets from the magazine and triggers an automatic reload if the magazine is empty.
    private void ConsumeAmmo()
    {
        currentAmmo--;

        if (currentAmmo <= 0 && reserveAmmo > 0)
        {
            Reload();
        }
    }
    

    // =========================
    // OTHER METHODS
    // =========================
    // Assigns the fire origin based on the player's camera.
    private void AssignFireOrigin()
    {
        // Weapon must belong to a player
        LookController lookController = GetComponentInParent<LookController>();

        // Only the owner has a valid camera
        if (!lookController.IsOwner)
            return;

        fireOrigin = lookController.CameraTransform;
    }
}