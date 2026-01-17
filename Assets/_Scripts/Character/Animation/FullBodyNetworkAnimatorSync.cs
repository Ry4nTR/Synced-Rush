using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public class FullBodyNetworkAnimatorSync : NetworkBehaviour
{
    [Header("Target Animator")]
    [SerializeField] private Animator fullBodyAnimator;

    [Header("Optional: FullBody Controller Overrides (same order on all builds)")]
    [Tooltip("Index 0 = default/base. Add all possible fullBody controllers/overrides here.")]
    [SerializeField] private RuntimeAnimatorController[] fullBodyControllers;

    // Replication of aim layer weight using NetworkVariable for best performance
    private NetworkVariable<float> netAimLayerWeight =
        new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int aimLayerIndex = 1;

    public override void OnNetworkSpawn()
    {
        // Apply server-driven NetworkVariable changes on everyone
        netAimLayerWeight.OnValueChanged += (_, v) => ApplyAimLayerWeight(v);

        // Make sure current value is applied when we spawn
        ApplyAimLayerWeight(netAimLayerWeight.Value);
    }

    public override void OnNetworkDespawn()
    {
        netAimLayerWeight.OnValueChanged -= (_, v) => ApplyAimLayerWeight(v);
    }

    private void ApplyAimLayerWeight(float w)
    {
        if (fullBodyAnimator == null) return;
        if (aimLayerIndex >= 0 && aimLayerIndex < fullBodyAnimator.layerCount)
            fullBodyAnimator.SetLayerWeight(aimLayerIndex, w);
    }

    // ----------------------------
    // Public API called by your PlayerAnimationController
    // ----------------------------

    public void NetSetFloat(int paramHash, float value)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        // Always apply locally first (important for host/local feel)
        fullBodyAnimator.SetFloat(paramHash, value);

        // Only the owner should broadcast state changes
        if (!IsOwner) return;

        SetFloatServerRpc(paramHash, value);
    }

    public void NetSetBool(int paramHash, bool value)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        fullBodyAnimator.SetBool(paramHash, value);

        if (!IsOwner) return;

        SetBoolServerRpc(paramHash, value);
    }

    public void NetSetTrigger(int paramHash)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        fullBodyAnimator.SetTrigger(paramHash);

        if (!IsOwner) return;

        SetTriggerServerRpc(paramHash);
    }

    public void NetSetAimLayerWeight(float weight, int layerIndex = 1)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        aimLayerIndex = layerIndex;
        ApplyAimLayerWeight(weight);

        if (!IsOwner) return;

        SetAimLayerWeightServerRpc(weight, layerIndex);
    }

    /// <summary>
    /// Optional: sync fullBody runtime controller by index from the inspector list.
    /// Put your default/base controller at index 0.
    /// </summary>
    public void NetSetFullBodyControllerByIndex(ushort index)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        ApplyControllerIndex(index);

        if (!IsOwner) return;

        SetControllerIndexServerRpc(index);
    }

    private void ApplyControllerIndex(ushort index)
    {
        if (fullBodyControllers == null || fullBodyControllers.Length == 0) return;
        if (index >= fullBodyControllers.Length) return;

        var ctrl = fullBodyControllers[index];
        if (ctrl != null)
            fullBodyAnimator.runtimeAnimatorController = ctrl;
    }

    // ----------------------------
    // RPCs (Owner -> Server -> Everyone)
    // ----------------------------

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetFloatServerRpc(int paramHash, float value)
    {
        SetFloatClientRpc(paramHash, value);
    }

    [ClientRpc]
    private void SetFloatClientRpc(int paramHash, float value)
    {
        // Owner already applied locally in NetSetFloat
        if (IsOwner) return;
        if (fullBodyAnimator == null) return;

        fullBodyAnimator.SetFloat(paramHash, value);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetBoolServerRpc(int paramHash, bool value)
    {
        SetBoolClientRpc(paramHash, value);
    }

    [ClientRpc]
    private void SetBoolClientRpc(int paramHash, bool value)
    {
        if (IsOwner) return;
        if (fullBodyAnimator == null) return;

        fullBodyAnimator.SetBool(paramHash, value);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetTriggerServerRpc(int paramHash)
    {
        SetTriggerClientRpc(paramHash);
    }

    [ClientRpc]
    private void SetTriggerClientRpc(int paramHash)
    {
        if (IsOwner) return;
        if (fullBodyAnimator == null) return;

        fullBodyAnimator.SetTrigger(paramHash);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetAimLayerWeightServerRpc(float weight, int layerIndex)
    {
        aimLayerIndex = layerIndex;
        netAimLayerWeight.Value = weight; // NetworkVariable fan-out (and applies to late joiners)
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetControllerIndexServerRpc(ushort index)
    {
        SetControllerIndexClientRpc(index);
    }

    [ClientRpc]
    private void SetControllerIndexClientRpc(ushort index)
    {
        if (IsOwner) return;
        ApplyControllerIndex(index);
    }
}