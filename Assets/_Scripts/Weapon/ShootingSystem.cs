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
    public void PerformShoot(Vector3 origin, Vector3 direction, float spread, int weaponID)
    {
        if (weaponController == null || weaponController.weaponData == null)
            return;

        Vector3 finalDir = weaponData.ApplySpread(direction, spread);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            finalDir,
            weaponData.range,
            weaponData.layerMask,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit validHit = default;
        bool hasValidHit = false;

        foreach (var hit in hits)
        {
            // IGNORE SELF
            if (hit.collider.transform.IsChildOf(playerRoot))
                continue;

            validHit = hit;
            hasValidHit = true;
            break;
        }

        if (hasValidHit)
        {
            lastRayOrigin = origin;
            lastRayEnd = validHit.point;
            hasLastRay = true;

            ShowImpactEffect(validHit.point, validHit.normal);
            ShowBulletTracer(origin, validHit.point);

            float distance = Vector3.Distance(origin, validHit.point);
            float damage = weaponData.CalculateDamageByDistance(distance);
        }
        else
        {
            Vector3 endPoint = origin + finalDir * weaponData.range;

            lastRayOrigin = origin;
            lastRayEnd = endPoint;
            hasLastRay = true;

            ShowBulletTracer(origin, endPoint);
        }

        PlayFireAnimation();
        PlayMuzzleFlash();
        PlayShootSound();
    }

    /// =========================
    /// VFX & SFX
    /// =========================

    // Spawns an impact effect at the given point
    private void ShowImpactEffect(Vector3 position, Vector3 normal)
    {
        if (weaponData.impactEffectPrefab != null)
        {
            GameObject impact = Instantiate(weaponData.impactEffectPrefab,
                                            position, Quaternion.LookRotation(normal));
            Destroy(impact, 2f);
        }
    }

    // Spawns a bullet tracer from origin to end. Replace this with your own tracer implementation.
    private void ShowBulletTracer(Vector3 origin, Vector3 end)
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
        //playerAnimationController.SetRecoilWeight(recoil);

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