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
    [SerializeField] private Transform cameraHolder;     // The object that pitches up/down

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

    private void Start()
    {
        // Disable on non-owner
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

        // Lock cursor
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

        Vector2 look = inputHandler.look;

        // Sensitivity scaling
        float deltaX = look.x * sensitivity * 0.01f;
        float deltaY = look.y * sensitivity * 0.01f;

        // YAW: rotate pivot (horizontal)
        yaw += deltaX;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // PITCH: rotate camera holder (vertical)
        pitch += invertY ? deltaY : -deltaY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Rotate arms  if assigned
        if (armsRoot != null)
        {
            Vector3 currentEuler = armsRoot.localEulerAngles;
            float correctedYaw = currentEuler.y;
            float correctedRoll = currentEuler.z;
            armsRoot.localRotation = Quaternion.Euler(pitch, correctedYaw, correctedRoll);
        }
    }
}
