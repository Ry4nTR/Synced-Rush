using Unity.Netcode;
using Unity.Cinemachine;
using UnityEngine;

public class OwnerOnlyVcam : NetworkBehaviour
{
    [SerializeField] private CinemachineCamera vcam;

    public override void OnNetworkSpawn()
    {
        if (vcam == null) vcam = GetComponentInChildren<CinemachineCamera>(true);
        if (vcam == null) return;

        // Only owner keeps an active vcam on this client
        vcam.enabled = IsOwner;
        if (!IsOwner) vcam.Priority = -1000;
    }
}
