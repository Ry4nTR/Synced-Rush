using UnityEngine;
using Unity.Netcode;

public class WeaponNetworkAnimatorSync : NetworkBehaviour
{
    [Header("Target Animator (world model / 3p gun)")]
    [SerializeField] private Animator weaponAnimator;

    private bool pendingReload;
    private float pendingAim = -1f;

    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private static readonly int AimHash = Animator.StringToHash("Aim");

    //========================
    // Setup
    //========================
    public void SetWeaponAnimator(Animator anim)
    {
        weaponAnimator = anim;

        // apply pending state if any
        if (weaponAnimator == null) return;

        if (pendingAim >= 0f)
        {
            weaponAnimator.SetFloat(AimHash, pendingAim);
            pendingAim = -1f;
        }

        if (pendingReload)
        {
            weaponAnimator.SetTrigger(ReloadHash);
            pendingReload = false;
        }
    }

    public void ApplyOverride(AnimatorOverrideController aoc)
    {
        if (weaponAnimator == null || aoc == null) return;
        weaponAnimator.runtimeAnimatorController = aoc;
    }

    //========================
    // Networked Params
    //========================
    public void NetSetAim(float aim01)
    {
        if (!IsSpawned || weaponAnimator == null) return;

        weaponAnimator.SetFloat(AimHash, aim01);

        if (!IsOwner) return;
        SetAimServerRpc(aim01);
    }

    public void NetReload()
    {
        if (!IsSpawned || weaponAnimator == null) return;

        weaponAnimator.SetTrigger(ReloadHash);

        if (!IsOwner) return;
        ReloadServerRpc();
    }

    //========================
    // RPCs
    //========================
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetAimServerRpc(float aim01)
    {
        SetAimClientRpc(aim01);
    }

    [ClientRpc]
    private void SetAimClientRpc(float aim01)
    {
        if (IsOwner) return;

        if (weaponAnimator == null)
        {
            pendingAim = aim01;
            return;
        }

        weaponAnimator.SetFloat(AimHash, aim01);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReloadServerRpc()
    {
        ReloadClientRpc();
    }

    [ClientRpc]
    private void ReloadClientRpc()
    {
        if (IsOwner) return;

        if (weaponAnimator == null)
        {
            pendingReload = true;
            return;
        }

        weaponAnimator.SetTrigger(ReloadHash);
    }
}
