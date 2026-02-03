using UnityEngine;

[RequireComponent(typeof(FullBodyNetworkAnimatorSync))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] public Animator armsAnimator;
    [SerializeField] private Animator fullBodyAnimator;   // optional local reference (not required if using net sync)
    [SerializeField] private Animator weaponAnimator;

    [Header("Networking (FullBody)")]
    [SerializeField] private FullBodyNetworkAnimatorSync fullBodyNetSync;

    //========================
    // Animator Hashes
    //========================
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsWallRunningHash = Animator.StringToHash("IsWallRunning");
    private static readonly int WallRunSideHash = Animator.StringToHash("WallRunSide");

    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private static readonly int EquipHash = Animator.StringToHash("Equip");
    private static readonly int HolsterHash = Animator.StringToHash("Holster");

    //========================
    // Weapon Setup
    //========================
    public void SetWeaponAnimations(WeaponData data)
    {
        if (data == null) return;

        // Arms override (local only)
        if (data.armsAnimatorOverride != null && armsAnimator != null)
            armsAnimator.runtimeAnimatorController = data.armsAnimatorOverride;

        // FullBody override (local + networked index)
        if (data.fullBodyAnimatorOverride != null)
        {
            // Local apply (optional, mostly for host/owner immediate visuals)
            if (fullBodyAnimator != null)
                fullBodyAnimator.runtimeAnimatorController = data.fullBodyAnimatorOverride;

            // Network apply (late joiner safe if sync uses NV index)
            if (fullBodyNetSync != null)
                fullBodyNetSync.NetSetFullBodyControllerByIndex(data.fullBodyControllerIndex);
        }
    }

    public void SetWeaponAnimation(WeaponData data, Animator wAnimator)
    {
        if (data == null) return;

        weaponAnimator = wAnimator;
        if (weaponAnimator != null && data.weaponAnimatorOverride != null)
            weaponAnimator.runtimeAnimatorController = data.weaponAnimatorOverride;
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
        // Arms local
        if (armsAnimator != null)
            armsAnimator.SetLayerWeight(1, weight);

        // Fullbody local + network (NV fanout for late joiners)
        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetAimLayerWeight(weight, 1);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetLayerWeight(1, weight);
    }

    //========================
    // WallRun (Lower body only layer)
    //========================
    public void SetWallRun(bool isWallRunning, float side /* -1 left, +1 right */)
    {
        // You can also set these on arms if needed (usually not)
        if (fullBodyNetSync != null)
        {
            fullBodyNetSync.NetSetBool(IsWallRunningHash, isWallRunning);
            fullBodyNetSync.NetSetFloat(WallRunSideHash, side);
            fullBodyNetSync.NetSetLayerWeight(isWallRunning ? 1f : 0f, FullBodyNetworkAnimatorSync.DefaultWallRunLayerIndex);
        }
        else if (fullBodyAnimator != null)
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

        if (weaponAnimator != null)
            weaponAnimator.SetTrigger(ReloadHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(ReloadHash);
        else if (fullBodyAnimator != null)
            fullBodyAnimator.SetTrigger(ReloadHash);
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
