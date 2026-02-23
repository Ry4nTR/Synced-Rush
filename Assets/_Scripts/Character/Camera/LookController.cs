using SyncedRush.Generics;
using SyncedRush.UI.Settings;
using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class LookController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("Camera")]
    [SerializeField] private Transform cameraHolder;

    [Header("Arms")]
    [Tooltip("Optional reference to the root of the first-person arms. If assigned, the arms will follow the camera's vertical rotation.")]
    [SerializeField] private Transform armsRoot;

    [Header("Settings")]
    [SerializeField] private float sensitivity = 5f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float _runtimeSensitivity;
    private bool _runtimeInvertY;

    private float pitch;    // Vertical
    private float yaw;      // Horizontal

    private float simYaw;
    private float simPitch;

    private float localPitch;
    private float localYaw;
    private bool confirmVisualUpdate = false;

    public float SimYaw => simYaw;
    public float SimPitch => simPitch;

    public Transform CameraTransform => cameraHolder;
    public float CurrentYaw => yaw;
    public float CurrentPitch => pitch;

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

        // Keep your original behavior
        ApplyLookSettings();

        // New: pull runtime settings (used by Update look math)
        RefreshFromSettings();

        simYaw = yaw;
        simPitch = pitch;
        localYaw = yaw;
        localPitch = pitch;

        inputHandler.SetCursorLocked(true);
    }

    private void OnEnable()
    {
        if (!IsOwner) return;

        // New: subscribe to settings changes (runtime cache)
        RefreshFromSettings();
        var sm = SettingsManager.Instance;
        if (sm != null) sm.OnSettingsChanged += RefreshFromSettings;

        // Keep your original subscription if you still want it
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsChanged += ApplyLookSettings;
    }

    private void OnDisable()
    {
        if (!IsOwner) return;

        inputHandler.SetCursorLocked(false);

        // New: correct unsubscribe (must match what we subscribed)
        var sm = SettingsManager.Instance;
        if (sm != null) sm.OnSettingsChanged -= RefreshFromSettings;

        // Keep your original unsubscribe
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsChanged -= ApplyLookSettings;
    }

    private void LateUpdate()
    {
        if (!IsOwner || !confirmVisualUpdate) return;

        confirmVisualUpdate = false;

        yaw = simYaw;
        pitch = simPitch;

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector2 look = inputHandler.Look;

        // New: use runtime values so it updates live
        float sens = _runtimeSensitivity > 0f ? _runtimeSensitivity : sensitivity;
        bool inv = _runtimeInvertY;

        float deltaX = look.x * sens * 0.8f * Time.deltaTime;
        float deltaY = look.y * sens * 0.8f * Time.deltaTime;

        localYaw += deltaX;

        localPitch += inv ? deltaY : -deltaY;
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
        newPitch = Mathf.Clamp(newPitch, minPitch, maxPitch);

        yaw = newYaw;
        pitch = newPitch;

        simYaw = newYaw;
        simPitch = newPitch;

        localYaw = newYaw;
        localPitch = newPitch;

        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(newPitch, 0f, 0f);

        confirmVisualUpdate = false;
    }

    private void ApplyLookSettings()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;
        sensitivity = sm.Sensitivity;
        invertY = sm.InvertY;
    }

    private void RefreshFromSettings()
    {
        var sm = SettingsManager.Instance;
        if (sm == null) return;

        _runtimeSensitivity = sm.GetFloat(FloatSettingKey.Sensitivity);
        _runtimeInvertY = sm.GetBool(BoolSettingKey.InvertY);

        // Optional debug (remove once verified)
        // Debug.Log($"[LookController] runtime sens={_runtimeSensitivity} invertY={_runtimeInvertY}");
    }
}