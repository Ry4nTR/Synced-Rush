using UnityEngine;

/// <summary>
/// Handles the logic of firing a weapon: applying spread, performing a local raycast
/// for immediate feedback, spawning visual/audio effects and sending damage
/// requests to the network handler. This script does not manage ammo or cooldowns;
/// those are handled by the WeaponController.
/// </summary>
public class ShootingSystem : MonoBehaviour
{
    private WeaponController weaponController;
    private WeaponNetworkHandler networkHandler;

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
        networkHandler = GetComponent<WeaponNetworkHandler>();
    }

    /// <summary>
    /// Performs a shot from the given origin and direction using the specified spread.
    /// Performs a local raycast for immediate feedback and then forwards a damage
    /// request to the server via the network handler.
    /// </summary>
    /// <param name="origin">The world position where the shot originates.</param>
    /// <param name="direction">The forward direction of the shot before spread.</param>
    /// <param name="spread">The spread value to apply to the direction.</param>
    /// <param name="weaponID">The ID of the weapon being used (not currently used but kept for future expansion).</param>
    public void PerformShoot(Vector3 origin, Vector3 direction, float spread, int weaponID)
    {
        if (weaponController == null || weaponController.weaponData == null)
            return;

        // 1. Apply spread to the direction
        Vector3 finalDir = ApplySpread(direction, spread);

        // 2. Local raycast for immediate feedback
        if (Physics.Raycast(origin, finalDir, out RaycastHit hit, weaponController.weaponData.range))
        {
            // 3. Spawn impact effect and tracer locally
            ShowImpactEffect(hit.point, hit.normal);
            ShowBulletTracer(origin, hit.point);

            // 4. Calculate damage based on distance for falloff
            float distance = Vector3.Distance(origin, hit.point);
            float damage = weaponController.CalculateDamageByDistance(distance);

            // 5. Request damage via the network handler
            networkHandler?.RequestDamage(hit.collider.gameObject, damage, hit.point);
        }
        else
        {
            // Miss: show tracer to max range
            Vector3 endPoint = origin + finalDir * weaponController.weaponData.range;
            ShowBulletTracer(origin, endPoint);
        }

        // 6. Play muzzle flash and shoot sound locally
        PlayMuzzleFlash();
        PlayShootSound();
    }

    /// <summary>
    /// Applies random spread to a direction vector by rotating it within a cone of the given angle.
    /// </summary>
    private Vector3 ApplySpread(Vector3 direction, float spread)
    {
        float yaw = Random.Range(-spread, spread);
        float pitch = Random.Range(-spread, spread);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        return rot * direction;
    }

    /// <summary>
    /// Spawns an impact effect at the given point. This can be replaced with more
    /// sophisticated VFX logic in your project.
    /// </summary>
    private void ShowImpactEffect(Vector3 position, Vector3 normal)
    {
        if (weaponController.weaponData.impactEffectPrefab != null)
        {
            GameObject impact = Instantiate(weaponController.weaponData.impactEffectPrefab,
                                            position, Quaternion.LookRotation(normal));
            Destroy(impact, 2f);
        }
    }

    /// <summary>
    /// Spawns a bullet tracer from origin to end. Replace this with your own tracer implementation.
    /// </summary>
    private void ShowBulletTracer(Vector3 origin, Vector3 end)
    {
        if (weaponController.weaponData.bulletTracerPrefab != null)
        {
            GameObject tracer = Instantiate(weaponController.weaponData.bulletTracerPrefab,
                                            origin, Quaternion.identity);
            // Set up tracer movement here or use a line renderer
            Destroy(tracer, 1f);
        }
    }

    /// <summary>
    /// Plays the muzzle flash effect at the muzzle position. Assumes the muzzle flash prefab
    /// is configured to auto-destroy.
    /// </summary>
    private void PlayMuzzleFlash()
    {
        if (weaponController.weaponData.muzzleFlashPrefab != null)
        {
            Instantiate(weaponController.weaponData.muzzleFlashPrefab, transform.position, transform.rotation);
        }
    }

    /// <summary>
    /// Plays the shoot sound at the weapon's position.
    /// </summary>
    private void PlayShootSound()
    {
        if (weaponController.weaponData.shootSound != null)
        {
            AudioSource.PlayClipAtPoint(weaponController.weaponData.shootSound, transform.position);
        }
    }
}