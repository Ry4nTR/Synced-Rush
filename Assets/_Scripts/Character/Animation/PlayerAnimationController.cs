using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] public Animator armsAnimator;
    [SerializeField] private Animator fullBodyAnimator;
    [SerializeField] private Animator weaponAnimator;

    [Header("Networking (FullBody)")]
    [SerializeField] private FullBodyNetworkAnimatorSync fullBodyNetSync;

    // Hashes (avoid string every call)
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private static readonly int EquipHash = Animator.StringToHash("Equip");
    private static readonly int HolsterHash = Animator.StringToHash("Holster");

    private void Awake()
    {
        if (fullBodyNetSync == null)
            fullBodyNetSync = GetComponentInParent<FullBodyNetworkAnimatorSync>();
    }

    public void SetWeaponAnimations(WeaponData data)
    {
        if (data.armsAnimatorOverride != null)
            armsAnimator.runtimeAnimatorController = data.armsAnimatorOverride;

        if (data.fullBodyAnimatorOverride != null && fullBodyAnimator != null)
        {
            // Local apply
            fullBodyAnimator.runtimeAnimatorController = data.fullBodyAnimatorOverride;

            // Network apply (important!)
            fullBodyNetSync.NetSetFullBodyControllerByIndex(data.fullBodyControllerIndex);
        }
    }

    public void SetWeaponAnimation(Animator wAnimator)
    {
        weaponAnimator = wAnimator;
    }

    #region Arms & Fullbody Parameters

    public void SetMoveSpeed(float speed, float walkSpdThreshold, float runSpdThreshold)
    {
        float normalizedSpeed = 0f;

        if (speed <= walkSpdThreshold)
        {
            float t = Mathf.InverseLerp(0, walkSpdThreshold, speed);
            normalizedSpeed = Mathf.Lerp(0f, 0.4f, t);
        }
        else
        {
            float t = Mathf.InverseLerp(walkSpdThreshold, runSpdThreshold, speed);
            normalizedSpeed = Mathf.Lerp(0.4f, 1.0f, t);
        }

        // Arms local
        armsAnimator.SetFloat(MoveSpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);

        // Fullbody local (keep your smoothing locally)
        fullBodyAnimator.SetFloat(MoveSpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);

        // Fullbody network (remote clients don't need dampTime; they just need the value)
        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetFloat(MoveSpeedHash, normalizedSpeed);
    }

    public void SetAimWeight(float weight)
    {
        // Arms local
        armsAnimator.SetLayerWeight(1, weight);

        // Fullbody local
        fullBodyAnimator.SetLayerWeight(1, weight);

        // Fullbody network
        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetAimLayerWeight(weight, 1);
    }

    public void SetSliding(bool value)
    {
        armsAnimator.SetBool(IsSlidingHash, value);
        fullBodyAnimator.SetBool(IsSlidingHash, value);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetBool(IsSlidingHash, value);
    }

    public void SetGrounded(bool value)
    {
        armsAnimator.SetBool(IsGroundedHash, value);
        fullBodyAnimator.SetBool(IsGroundedHash, value);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetBool(IsGroundedHash, value);
    }

    public void Fire()
    {
        armsAnimator.SetTrigger(FireHash);
        fullBodyAnimator.SetTrigger(FireHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(FireHash);
    }

    public void Jump()
    {
        armsAnimator.SetTrigger(JumpHash);
        fullBodyAnimator.SetTrigger(JumpHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(JumpHash);
    }

    public void Reload()
    {
        armsAnimator.SetTrigger(ReloadHash);
        fullBodyAnimator.SetTrigger(ReloadHash);
        weaponAnimator.SetTrigger(ReloadHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(ReloadHash);
    }

    public void Equip()
    {
        armsAnimator.SetTrigger(EquipHash);
        fullBodyAnimator.SetTrigger(EquipHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(EquipHash);
    }

    public void Holster()
    {
        armsAnimator.SetTrigger(HolsterHash);
        fullBodyAnimator.SetTrigger(HolsterHash);

        if (fullBodyNetSync != null)
            fullBodyNetSync.NetSetTrigger(HolsterHash);
    }

    #endregion
}
