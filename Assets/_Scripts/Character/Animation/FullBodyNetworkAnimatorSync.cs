using UnityEngine;
using Unity.Netcode;

public class FullBodyNetworkAnimatorSync : NetworkBehaviour
{
    //========================
    // Constants / Defaults
    //========================
    public const int DefaultAimLayerIndex = 1;

    // Set this to whatever you decide in the Animator (Layer 3 recommended)
    public const int DefaultWallRunLayerIndex = 3;

    //========================
    // Inspector
    //========================
    [Header("Target Animator")]
    [SerializeField] private Animator fullBodyAnimator;

    [Header("FullBody Controller Overrides (same order on all builds)")]
    [Tooltip("Index 0 = default/base. Add all possible fullBody controllers/overrides here.")]
    [SerializeField] private RuntimeAnimatorController[] fullBodyControllers;

    //========================
    // Network Variables (late-joiner safe)
    //========================

    // Aim layer weight (fan-out + late joiners)
    private readonly NetworkVariable<float> netAimLayerWeight = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Fullbody controller index (fan-out + late joiners)
    private readonly NetworkVariable<ushort> netControllerIndex = new NetworkVariable<ushort>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    //========================
    // Internal State
    //========================
    private int aimLayerIndex = DefaultAimLayerIndex;

    private NetworkVariable<float>.OnValueChangedDelegate _aimChangedHandler;
    private NetworkVariable<ushort>.OnValueChangedDelegate _controllerChangedHandler;

    //========================
    // Unity Netcode Lifecycle
    //========================
    public override void OnNetworkSpawn()
    {
        // Subscribe (store delegates so unsubscribe works!)
        _aimChangedHandler = (_, v) => ApplyAimLayerWeight(v);
        netAimLayerWeight.OnValueChanged += _aimChangedHandler;

        _controllerChangedHandler = (_, v) => ApplyControllerIndex(v);
        netControllerIndex.OnValueChanged += _controllerChangedHandler;

        // Apply current values on spawn
        ApplyAimLayerWeight(netAimLayerWeight.Value);
        ApplyControllerIndex(netControllerIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (_aimChangedHandler != null)
            netAimLayerWeight.OnValueChanged -= _aimChangedHandler;

        if (_controllerChangedHandler != null)
            netControllerIndex.OnValueChanged -= _controllerChangedHandler;
    }

    //========================
    // Public API (called by PlayerAnimationController)
    //========================

    public void NetSetFloat(int paramHash, float value, float dampTime = 0f)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        // Local apply first (owner/host feel)
        if (dampTime > 0f)
            fullBodyAnimator.SetFloat(paramHash, value, dampTime, Time.deltaTime);
        else
            fullBodyAnimator.SetFloat(paramHash, value);

        // Only owner sends state changes
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

    /// <summary>
    /// Sets aim layer weight locally, and replicates via NetworkVariable (late joiners safe).
    /// </summary>
    public void NetSetAimLayerWeight(float weight, int layerIndex = DefaultAimLayerIndex)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        aimLayerIndex = layerIndex;
        ApplyAimLayerWeight(weight);

        if (!IsOwner) return;

        SetAimLayerWeightServerRpc(weight, layerIndex);
    }

    /// <summary>
    /// Sets fullbody runtime controller index (late joiners safe).
    /// </summary>
    public void NetSetFullBodyControllerByIndex(ushort index)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        ApplyControllerIndex(index);

        if (!IsOwner) return;

        SetControllerIndexServerRpc(index);
    }

    /// <summary>
    /// Utility: layer weights must be set by code (Animator doesn't conditionally control weights).
    /// </summary>
    public void NetSetLayerWeight(float weight, int layerIndex)
    {
        if (!IsSpawned || fullBodyAnimator == null) return;

        // Local
        if (layerIndex >= 0 && layerIndex < fullBodyAnimator.layerCount)
            fullBodyAnimator.SetLayerWeight(layerIndex, weight);

        // Optional: replicate if you want exact matching on remotes.
        // Usually NOT necessary if remotes derive from IsWallRunning/IsAiming etc.
        // If you want replication, add a NetworkVariable per layer or pack into a small struct.
    }

    //========================
    // Apply Helpers
    //========================
    private void ApplyAimLayerWeight(float w)
    {
        if (fullBodyAnimator == null) return;

        if (aimLayerIndex >= 0 && aimLayerIndex < fullBodyAnimator.layerCount)
            fullBodyAnimator.SetLayerWeight(aimLayerIndex, w);
    }

    private void ApplyControllerIndex(ushort index)
    {
        if (fullBodyAnimator == null) return;
        if (fullBodyControllers == null || fullBodyControllers.Length == 0) return;
        if (index >= fullBodyControllers.Length) return;

        var ctrl = fullBodyControllers[index];
        if (ctrl != null)
            fullBodyAnimator.runtimeAnimatorController = ctrl;
    }

    //========================
    // RPCs (Owner -> Server -> Everyone)
    //========================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetFloatServerRpc(int paramHash, float value)
    {
        SetFloatClientRpc(paramHash, value);
    }

    [ClientRpc]
    private void SetFloatClientRpc(int paramHash, float value)
    {
        // Owner already applied locally
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
        netAimLayerWeight.Value = weight;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetControllerIndexServerRpc(ushort index)
    {
        netControllerIndex.Value = index;
    }
}
