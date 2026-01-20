using Unity.Cinemachine;
using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    public CinemachineCamera vcam;
    public Camera viewmodelCam;

    void Update()
    {
        if (vcam == null) return;

        var state = vcam.State;

        viewmodelCam.transform.SetPositionAndRotation(
            state.GetFinalPosition(),
            state.GetFinalOrientation()
        );
    }
}
