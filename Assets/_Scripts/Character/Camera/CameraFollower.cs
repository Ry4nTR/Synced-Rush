using Unity.Cinemachine;
using UnityEngine;
using Unity.Netcode;

public class CameraFollower : NetworkBehaviour
{
    public CinemachineCamera vcam;
    public Camera viewmodelCam;

    private void LateUpdate()
    {
        if (!IsOwner) return;
        if (vcam == null || viewmodelCam == null) return;

        var state = vcam.State;

        viewmodelCam.transform.SetPositionAndRotation(
            state.GetFinalPosition(),
            state.GetFinalOrientation()
        );
    }
}
