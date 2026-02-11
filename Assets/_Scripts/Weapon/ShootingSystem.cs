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

        Transform selfRoot = transform.root; // or the player root you want

        RaycastHit hit;
        Vector3 endPoint;

        if (TryGetFirstValidHit(origin, finalDirection, range, data.layerMask, selfRoot, out hit))
        {
            endPoint = hit.point;
            fxService?.PlayImpact(data.impactEffectPrefab, hit.point, hit.normal);
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

    private bool TryGetFirstValidHit(Vector3 origin, Vector3 dir, float range, int mask, Transform selfRoot, out RaycastHit bestHit)
    {
        bestHit = default;

        var hits = Physics.RaycastAll(origin, dir, range, mask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            var col = h.collider;
            if (col == null) continue;

            // Skip self hitboxes
            if (col.TryGetComponent(out Hitbox hb))
            {
                // If Hitbox stores NetworkObjectId, this works:
                var myNet = selfRoot.GetComponentInParent<Unity.Netcode.NetworkObject>();
                if (myNet != null && hb.OwnerNetworkId == myNet.NetworkObjectId)
                    continue;
            }

            // Fallback: hierarchy self-skip
            if (col.transform.IsChildOf(selfRoot))
                continue;

            bestHit = h;
            return true;
        }

        return false;
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