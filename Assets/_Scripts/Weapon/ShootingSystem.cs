using UnityEngine;

/// <summary>
/// Handles the logic of firing a weapon.
/// </summary>
public class ShootingSystem : MonoBehaviour
{
    private WeaponController weaponController;
    private PlayerAnimationController playerAnimationController;
    private WeaponData weaponData;
    private WeaponFxService fxService;
    private IWeaponAudioService audioService;

    // TEMPORARY DEBUG RAY (GIZMOS)
    private Vector3 lastRayOrigin;
    private Vector3 lastRayEnd;
    private bool hasLastRay;

    private void Awake()
    {
        weaponController = GetComponent<WeaponController>();
        playerAnimationController = GetComponentInParent<PlayerAnimationController>();
        weaponData = weaponController.weaponData;
    }

    // Client-side shooting logic
    public void PerformShoot(Vector3 origin, Vector3 finalDirection, float range)
    {
        var data = weaponController.weaponData;
        if (data == null) return;

        PlayFireAnimation();

        // Muzzle
        var muzzle = weaponController.VfxSockets != null ? weaponController.VfxSockets.muzzle : null;
        if (fxService != null && muzzle != null)
            fxService.PlayMuzzleFlash(data.muzzleFlashPrefab, muzzle);

        // Audio
        audioService?.Play(data, WeaponSfxEvent.Fire, transform.position, isOwner: true);

        // Raycast for end point (visual only)
        RaycastHit hit;
        Vector3 endPoint;
        if (Physics.Raycast(origin, finalDirection, out hit, range, data.layerMask))
        {
            endPoint = hit.point;
            if (fxService != null)
                fxService.PlayImpact(data.impactEffectPrefab, hit.point, hit.normal);
        }
        else
        {
            endPoint = origin + finalDirection * range;
        }

        if (fxService != null)
            fxService.PlayTracer(data.bulletTracerPrefab, origin, endPoint);
    }


    /// =========================
    /// VFX & SFX
    /// =========================
    public void SetServices(WeaponFxService fxService, IWeaponAudioService audioService)
    {
        this.fxService = fxService;
        this.audioService = audioService;
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