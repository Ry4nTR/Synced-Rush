using Unity.Cinemachine;
using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    public CinemachineCamera vcam;
    public Camera viewmodelCam;

    void LateUpdate()
    {
        if (vcam == null || viewmodelCam == null)
            return;

        viewmodelCam.transform.SetPositionAndRotation(
            vcam.transform.position,
            vcam.transform.rotation
        );
    }
}
