using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Client-side Look Controller.
/// Rotates the local player's camera & body using owner-only input.
/// </summary>
[DisallowMultipleComponent]
public class LookController : NetworkBehaviour
{
    private const float DEFAULT_MAX_PITCH = 90f;
    private const float DEFAULT_MIN_PITCH = -90f;

    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Transform cameraHolder;

    [Header("Settings")]
    [SerializeField] private float sensitivity = 5f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float maxPitch = DEFAULT_MAX_PITCH;
    [SerializeField] private float minPitch = DEFAULT_MIN_PITCH;

    private float pitch;
    private float yaw;

    // Properties
    public float Pitch
    {
        get => pitch;
        private set => pitch = Mathf.Clamp(value, minPitch, maxPitch);
    }

    public float Yaw
    {
        get => yaw;
        private set => yaw = value;
    }

    private void Awake()
    {
        Vector3 e = transform.eulerAngles;
        Yaw = e.y;

        if (cameraHolder != null)
        {
            float localPitch = cameraHolder.localEulerAngles.x;
            if (localPitch > 180f) localPitch -= 360f;
            Pitch = localPitch;
        }
        else
        {
            Pitch = 0f;
        }
    }

    private void OnEnable()
    {
        if (IsOwner && inputHandler != null)
            inputHandler.SetCursorLocked(true);
    }

    private void OnDisable()
    {
        if (IsOwner && inputHandler != null)
            inputHandler.SetCursorLocked(false);
    }

    private void Update()
    {
        // IMPORTANT: Only the owner rotates the camera.
        if (!IsOwner) return;

        HandleLook();
    }

    private void HandleLook()
    {
        if (inputHandler == null) return;

        Vector2 rawDelta = inputHandler.look;

        if (invertY)
            rawDelta.y = -rawDelta.y;

        float deltaYaw = rawDelta.x * sensitivity * 0.01f;
        float deltaPitch = rawDelta.y * sensitivity * 0.01f;

        Yaw += deltaYaw;
        Pitch += deltaPitch;
        Pitch = Mathf.Clamp(Pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(-Pitch, 0f, 0f);
        }
    }

    public void ResetView()
    {
        Yaw = 0f;
        Pitch = 0f;
        ApplyRotationInstant();
    }

    public void SetView(Vector2 yawPitch)
    {
        Yaw = yawPitch.x;
        Pitch = yawPitch.y;
        ApplyRotationInstant();
    }

    private void ApplyRotationInstant()
    {
        transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
    }
}
