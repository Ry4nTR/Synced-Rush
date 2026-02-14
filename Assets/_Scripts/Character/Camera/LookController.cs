using UnityEngine;
using Unity.Netcode;
using SyncedRush.Generics;

/// <summary>
/// Client-side camera rotation controller.
/// Attach this to the YawPivot object (child of Character).
/// - Horizontal rotation (Yaw) happens on the Pivot.
/// - Vertical rotation (Pitch) happens on the CameraHolder.
/// - Only the owner runs this script.
/// </summary>
[DefaultExecutionOrder(-200)]
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

    private float simYaw;
    private float simPitch;

    private float localPitch;
    private float localYaw;
    private bool confirmVisualUpdate = false;

    public float SimYaw => simYaw;
    public float SimPitch => simPitch;


    // provide access to camera transform
    public Transform CameraTransform => cameraHolder;
    public float CurrentYaw => yaw;    
    public float CurrentPitch => pitch; // signed pitch, already clamped

    private void Start()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        confirmVisualUpdate = false;

        yaw = transform.eulerAngles.y;

        float rawPitch = cameraHolder.localEulerAngles.x;
        if (rawPitch > 180f) rawPitch -= 360f;
        pitch = Mathf.Clamp(rawPitch, minPitch, maxPitch);

        RefreshSensitivity();

        // ✅ rebase everything
        simYaw = yaw;
        simPitch = pitch;
        localYaw = yaw;
        localPitch = pitch;

        inputHandler.SetCursorLocked(true);
    }

    private void OnEnable()
    {
        if (IsOwner)
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.OnSettingsUpdate += RefreshSensitivity;
    }

    private void OnDisable()
    {
        if (IsOwner)
        {
            inputHandler.SetCursorLocked(false);

            if (SettingsManager.Instance != null)
                SettingsManager.Instance.OnSettingsUpdate -= RefreshSensitivity;
        }
    }

    private void LateUpdate()
    {
        if (!IsOwner || !confirmVisualUpdate) return;

        confirmVisualUpdate = false;

        yaw = simYaw;
        pitch = simPitch;

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // CameraHolder pitch stays local (relative to the Pivot we just rotated)
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector2 look = inputHandler.Look;

        float deltaX = look.x * sensitivity * 0.8f * Time.deltaTime;
        float deltaY = look.y * sensitivity * 0.8f * Time.deltaTime;

        localYaw += deltaX;

        localPitch += invertY ? deltaY : -deltaY;
        localPitch = Mathf.Clamp(localPitch, minPitch, maxPitch);
    }

    private void FixedUpdate()
    {
        confirmVisualUpdate = true;
        simPitch = localPitch;
        simYaw = localYaw;
    }

    public void ForceAimYawPitch(float newYaw, float newPitch)
    {
        // Clamp pitch
        newPitch = Mathf.Clamp(newPitch, minPitch, maxPitch);

        // Rebase ALL internal state
        yaw = newYaw;
        pitch = newPitch;

        simYaw = newYaw;
        simPitch = newPitch;

        localYaw = newYaw;
        localPitch = newPitch;

        // Apply immediately (works even if component is disabled)
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(newPitch, 0f, 0f);

        confirmVisualUpdate = false; // we already applied this frame
    }

    private void RefreshSensitivity()
    {
        sensitivity = SettingsManager.Instance.Sensitivity;
    }

}
