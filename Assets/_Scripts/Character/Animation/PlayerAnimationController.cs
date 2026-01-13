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

    public void SetMoveSpeed(float speed)
    {
        armsAnimator.SetFloat("MoveSpeed", speed);
    }

    public void SetAimWeight(float weight)
    {
        armsAnimator.SetFloat("AimWeight", weight);
        armsAnimator.SetLayerWeight(1, weight);
    }

    public void SetRecoilWeight(float weight)
    {
        armsAnimator.SetFloat("RecoilWeight", weight);
    }

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
