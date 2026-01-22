using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Client-side camera rotation controller.
/// </summary>
public class LookController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("Pivots")]
    [SerializeField] private Transform pitchPivot; // the PitchPivot (child of YawPivot)

    [Header("Settings")]
    [SerializeField] private float sensitivity = 5f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float _pitch;
    private float _yaw;

    public Transform CameraTransform => transform;

    private void Start()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Init yaw from current pivot
        _yaw = transform.localEulerAngles.y;

        // Init pitch from pitchPivot
        float rawPitch = pitchPivot.localEulerAngles.x;
        if (rawPitch > 180f) rawPitch -= 360f;
        _pitch = rawPitch;

        inputHandler.SetCursorLocked(true);
    }

    private void OnDisable()
    {
        if (IsOwner && inputHandler != null)
            inputHandler.SetCursorLocked(false);
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector2 look = inputHandler.Look;

        float dx = look.x * sensitivity * 0.01f;
        float dy = look.y * sensitivity * 0.01f;

        // YAW (left/right)
        _yaw += dx;
        transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);

        // PITCH (up/down)
        _pitch += invertY ? dy : -dy;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}
