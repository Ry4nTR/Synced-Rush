using UnityEngine;

/// <summary>
/// Handles the logic of firing a weapon.
/// </summary>
public class ShootingSystem : MonoBehaviour
{
    private WeaponController weaponController;
    private PlayerAnimationController playerAnimationController;
    private WeaponData weaponData;
    private Transform playerRoot;

    // TEMPORARY DEBUG RAY (GIZMOS)
    private Vector3 lastRayOrigin;
    private Vector3 lastRayEnd;
    private bool hasLastRay;

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
        playerAnimationController = GetComponentInParent<PlayerAnimationController>();
        weaponData = weaponController.weaponData;
        playerRoot = weaponController.transform.root;
    }

    // Client-side shooting logic
    public void PerformShoot(Vector3 origin, Vector3 finalDirection, float range)
    {
        // Visuals only - Logic is handled by NetworkHandler or Controller
        PlayFireAnimation();
        PlayMuzzleFlash();
        PlayShootSound();

        // Raycast purely for visual placement (Impact effect & Tracer end point)
        RaycastHit hit;
        Vector3 endPoint;

        if (Physics.Raycast(origin, finalDirection, out hit, range, weaponData.layerMask))
        {
            endPoint = hit.point;
            ShowImpactEffect(hit.point, hit.normal);
        }
        else
        {
            endPoint = origin + finalDirection * range;
        }

        ShowBulletTracer(origin, endPoint);
    }

    /// =========================
    /// VFX & SFX
    /// =========================
    // Spawns an impact effect at the given point
    public void ShowImpactEffect(Vector3 position, Vector3 normal)
    {
        if (weaponData.impactEffectPrefab != null)
        {
            GameObject impact = Instantiate(weaponData.impactEffectPrefab,
                                            position, Quaternion.LookRotation(normal));
            Destroy(impact, 2f);
        }
    }

    // Spawns a bullet tracer from origin to end. Replace this with your own tracer implementation.
    public void ShowBulletTracer(Vector3 origin, Vector3 end)
    {
        if (weaponData.bulletTracerPrefab != null)
        {
            GameObject tracer = Instantiate(weaponData.bulletTracerPrefab,
                                            origin, Quaternion.identity);
            // Set up tracer movement here or use a line renderer
            Destroy(tracer, 1f);
        }
    }

    // Plays the muzzle flash effect at the muzzle position.
    private void PlayMuzzleFlash()
    {
        if (weaponData.muzzleFlashPrefab != null)
        {
            Instantiate(weaponData.muzzleFlashPrefab, weaponData.Muzzle.transform.position, transform.rotation);
        }
    }

    // Plays the shoot sound at the weapon's position.
    private void PlayShootSound()
    {
        if (weaponData.shootSound != null)
        {
            AudioSource.PlayClipAtPoint(weaponData.shootSound, transform.position);
        }
    }

    private void PlayFireAnimation()
    {
        float recoil = weaponController.IsAiming
        ? weaponData.recoilWeight * weaponData.aimedRecoilMultiplier
        : weaponData.recoilWeight;

        // 1. Set the weight
        playerAnimationController.armsAnimator.SetLayerWeight(2, recoil);

        // 2. Trigger the animation
        playerAnimationController.Fire();
    }

    // TEMPORARY: Draws the last raycast in the editor for debugging
    private void OnDrawGizmos()
    {
        if (!hasLastRay)
            return;

        Gizmos.color = Color.purple;
        Gizmos.DrawLine(lastRayOrigin, lastRayEnd);

        // Small sphere to clearly show the hit point
        Gizmos.DrawSphere(lastRayEnd, 0.05f);
    }
}