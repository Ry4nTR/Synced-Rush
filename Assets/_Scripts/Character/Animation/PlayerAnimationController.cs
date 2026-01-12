using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] private Animator armsAnimator;
    [SerializeField] private Animator fullBodyAnimator;

    public void SetWeaponAnimations(WeaponData data)
    {
        if (data.armsAnimatorOverride != null)
            armsAnimator.runtimeAnimatorController = data.armsAnimatorOverride;

        if (data.fullBodyAnimatorOverride != null && fullBodyAnimator != null)
            fullBodyAnimator.runtimeAnimatorController = data.fullBodyAnimatorOverride;
    }

    #region Arms Parameters

    public void SetAiming(bool value) =>
        armsAnimator.SetBool("IsAiming", value);

    public void SetWalking(bool value) =>
        armsAnimator.SetBool("IsWalking", value);

    public void SetSprinting(bool value) =>
        armsAnimator.SetBool("IsSprinting", value);

    public void SetSliding(bool value) =>
        armsAnimator.SetBool("IsSliding", value);

    public void Fire() =>
        armsAnimator.SetTrigger("Fire");

    public void Reload() =>
        armsAnimator.SetTrigger("Reload");

    public void Equip() =>
        armsAnimator.SetTrigger("Equip");

    public void Holster() =>
        armsAnimator.SetTrigger("Holster");

    #endregion
}
