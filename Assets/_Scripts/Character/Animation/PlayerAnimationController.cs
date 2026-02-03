using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] public Animator armsAnimator;
    [SerializeField] private Animator fullBodyAnimator;   // optional local reference (not required if using net sync)
    [SerializeField] private Animator weaponAnimator;

    [Header("Networking (FullBody)")]
    [SerializeField] private FullBodyNetworkAnimatorSync fullBodyNetSync;
    [SerializeField] private WeaponNetworkAnimatorSync worldWeaponNetSync;

    //========================
    // Animator Hashes
    //========================
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsWallRunningHash = Animator.StringToHash("IsWallRunning");
    private static readonly int WallRunSideHash = Animator.StringToHash("WallRunSide");
    private static readonly int AimHash = Animator.StringToHash("Aim");

    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private static readonly int EquipHash = Animator.StringToHash("Equip");
    private static readonly int HolsterHash = Animator.StringToHash("Holster");

    //========================
    // Weapon Setup (single entry point)
    //========================
    public void ApplyWeaponSetup(WeaponData data, Animator fpsWeaponAnimator = null, Animator tpsWeaponAnimator = null)
    {
        if (data == null) return;

        //------------- Arms (1p) -------------
        if (data.armsAnimatorOverride != null && armsAnimator != null)
            armsAnimator.runtimeAnimatorController = data.armsAnimatorOverride;

        //------------- FullBody (3p) -------------
        if (data.fullBodyAnimatorOverride != null)
        {
            // optional local apply (host feel)
            if (fullBodyAnimator != null)
                fullBodyAnimator.runtimeAnimatorController = data.fullBodyAnimatorOverride;

            // authoritative network apply (late join safe)
            if (fullBodyNetSync != null)
                fullBodyNetSync.NetSetFullBodyControllerByIndex(data.fullBodyControllerIndex);
        }

        //------------- FPS weapon animator (viewmodel gun) -------------
        if (fpsWeaponAnimator != null && data.weaponAnimatorOverride != null)
        {
            weaponAnimator = fpsWeaponAnimator;
            weaponAnimator.runtimeAnimatorController = data.weaponAnimatorOverride;
        }

        //------------- TPS weapon animator (world model gun) -------------
        if (tpsWeaponAnimator != null)
        {
            if (worldWeaponNetSync != null)
            {
                worldWeaponNetSync.SetWeaponAnimator(tpsWeaponAnimator);

                if (data.worldWeaponAnimatorOverride != null)
                    worldWeaponNetSync.ApplyOverride(data.worldWeaponAnimatorOverride);
            }
            else
            {
                if (data.worldWeaponAnimatorOverride != null)
                    tpsWeaponAnimator.runtimeAnimatorController = data.worldWeaponAnimatorOverride;
            }
        }
    }

    //========================
    // Movement Parameters
    //========================
    public void SetMoveSpeed(float speed, float walkSpdThreshold, float runSpdThreshold)
    {
        float normalizedSpeed = NormalizeSpeed(speed, walkSpdThreshold, runSpdThreshold);

        // Arms: local smoothing is fine
        if (armsAnimator != null)
            armsAnimator.SetFloat(MoveSpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);

        // FullBody: route through net sync ONLY to avoid double-setting & overwriting damp
        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetFloat(MoveSpeedHash, normalizedSpeed, dampTime: 0.1f);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetFloat(MoveSpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);
    }

    public void SetGrounded(bool value)
    {
        if (armsAnimator != null)
            armsAnimator.SetBool(IsGroundedHash, value);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetBool(IsGroundedHash, value);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetBool(IsGroundedHash, value);
    }

    public void SetSliding(bool value)
    {
        if (armsAnimator != null)
            armsAnimator.SetBool(IsSlidingHash, value);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetBool(IsSlidingHash, value);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetBool(IsSlidingHash, value);
    }

    public void SetVerticalSpeed(float verticalSpeed)
    {
        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetFloat(VerticalSpeedHash, verticalSpeed);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetFloat(VerticalSpeedHash, verticalSpeed);
    }

    //========================
    // Aiming / Layers
    //========================
    public void SetAimWeight(float weight)
    {
        if (armsAnimator != null)
            armsAnimator.SetLayerWeight(1, weight);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetAimLayerWeight(weight, 1);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetLayerWeight(1, weight);

        // Drive world weapon Aim param too
        if (worldWeaponNetSync != null)
            worldWeaponNetSync.NetSetAim(weight);
        else if (weaponAnimator != null)
            weaponAnimator.SetFloat(AimHash, weight);
    }

    //========================
    // WallRun (Lower body only layer)
    //========================
    public void SetWallRun(bool isWallRunning, float side /* -1 left, +1 right */)
    {
        if (fullBodyNetSync != null)
        {
            fullBodyNetSync.NetSetWallRunState(isWallRunning, side);
            return;
        }

        // fallback (offline / no net sync)
        if (fullBodyAnimator != null)
        {
            fullBodyAnimator.SetBool(IsWallRunningHash, isWallRunning);
            fullBodyAnimator.SetFloat(WallRunSideHash, side);
            fullBodyAnimator.SetLayerWeight(FullBodyNetworkAnimatorSync.DefaultWallRunLayerIndex, isWallRunning ? 1f : 0f);
        }
    }

    //========================
    // One-shot Events
    //========================
    public void Fire()
    {
        if (armsAnimator != null)
            armsAnimator.SetTrigger(FireHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(FireHash);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetTrigger(FireHash);
    }

    public void Jump()
    {
        // Keep trigger for responsiveness (arms especially)
        if (armsAnimator != null)
            armsAnimator.SetTrigger(JumpHash);

        // Optional for fullbody (you may remove later if using only grounded/vertical speed)
        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(JumpHash);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetTrigger(JumpHash);
    }

    public void Reload()
    {
        if (armsAnimator != null)
            armsAnimator.SetTrigger(ReloadHash);

        // local weapon animator (if this is 1p weapon animator)
        if (weaponAnimator != null)
            weaponAnimator.SetTrigger(ReloadHash);

        // fullbody network
        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(ReloadHash);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetTrigger(ReloadHash);

        // world model weapon network
        if (worldWeaponNetSync != null)
            worldWeaponNetSync.NetReload();
    }

    public void Equip()
    {
        if (armsAnimator != null)
            armsAnimator.SetTrigger(EquipHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(EquipHash);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetTrigger(EquipHash);
    }

    public void Holster()
    {
        if (armsAnimator != null)
            armsAnimator.SetTrigger(HolsterHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(HolsterHash);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetTrigger(HolsterHash);
    }

    //========================
    // Helpers
    //========================
    private static float NormalizeSpeed(float speed, float walkSpdThreshold, float runSpdThreshold)
    {
        if (walkSpdThreshold <= 0f) return 0f;

        if (speed <= walkSpdThreshold)
        {
            float t = Mathf.InverseLerp(0f, walkSpdThreshold, speed);
            return Mathf.Lerp(0f, 0.4f, t);
        }
        else
        {
            float t = Mathf.InverseLerp(walkSpdThreshold, runSpdThreshold, speed);
            return Mathf.Lerp(0.4f, 1.0f, t);
        }
    }
}
