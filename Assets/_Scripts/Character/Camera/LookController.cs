using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Client-side camera rotation controller.
/// Attach this to the YawPivot object (child of Character).
/// - Horizontal rotation (Yaw) happens on the Pivot.
/// - Vertical rotation (Pitch) happens on the CameraHolder.
/// - Only the owner runs this script.
/// </summary>
public class LookController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("Camera")]
    [SerializeField] private Transform cameraHolder;

    [Header("Arms")]
    [Tooltip("Optional reference to the root of the first‑person arms. If assigned, the arms will follow the camera's vertical rotation.")]
    [SerializeField] private Transform armsRoot;

    [Header("Settings")]
    [SerializeField] private float sensitivity = 5f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float pitch;    // Vertical
    private float yaw;      // Horizontal

    // provide access to camera transform
    public Transform CameraTransform => cameraHolder;

    private void Start()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Initialize yaw with pivot rotation
        yaw = transform.localEulerAngles.y;

        // Initialize pitch with camera holder angle
        float rawPitch = cameraHolder.localEulerAngles.x;
        if (rawPitch > 180f) rawPitch -= 360f;
        pitch = rawPitch;

        inputHandler.SetCursorLocked(true);
    }

    private void OnDisable()
    {
        if (IsOwner)
            inputHandler.SetCursorLocked(false);
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector2 look = inputHandler.Look;

        float deltaX = look.x * sensitivity * 0.01f;
        float deltaY = look.y * sensitivity * 0.01f;

        // YAW
        yaw += deltaX;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // PITCH
        pitch += invertY ? deltaY : -deltaY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Arms follow pitch
        if (armsRoot != null)
        {
            Vector3 euler = armsRoot.localEulerAngles;
            armsRoot.localRotation = Quaternion.Euler(pitch, euler.y, euler.z);
        }
    }
}
