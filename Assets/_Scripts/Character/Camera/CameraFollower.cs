using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    public Transform virtualCamera;
    public Camera viewmodelCam;

    void LateUpdate()
    {
        if (virtualCamera == null) return;

        // Arms view
        viewmodelCam.transform.SetPositionAndRotation(
            virtualCamera.position,
            virtualCamera.rotation
        );
    }
}
