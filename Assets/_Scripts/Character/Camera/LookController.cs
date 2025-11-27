using UnityEngine;

/// <summary>
/// CharacterLookController: usa l'InputManager locale (PlayerInput → Send Messages) per leggere look.
/// Nessuno smoothing: raw look * sensitivity per massima reattività.
/// </summary>
[DisallowMultipleComponent]
public class LookController : MonoBehaviour
{
    // 1) Statici / costanti
    private const float DEFAULT_MAX_PITCH = 90f;
    private const float DEFAULT_MIN_PITCH = -90f;

    // 2) Campi pubblici/serializzati
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float sensitivity = 5f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float maxPitch = DEFAULT_MAX_PITCH;
    [SerializeField] private float minPitch = DEFAULT_MIN_PITCH;

    // 3) Campi privati
    private float pitch;
    private float yaw;

    // 4) Proprietà
    public float Pitch
    {
        get { return pitch; }
        private set { pitch = Mathf.Clamp(value, minPitch, maxPitch); }
    }

    public float Yaw
    {
        get { return yaw; }
        private set { yaw = value; }
    }

    public float Sensitivity
    {
        get { return sensitivity; }
        set { sensitivity = Mathf.Max(0.01f, value); }
    }

    // 5) MonoBehaviour methods
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
        // lock cursore tramite manager se disponibile
        if (inputHandler != null) inputHandler.SetCursorLocked(true);
    }

    private void Start()
    {
        if (cameraHolder == null)
        {
            Debug.LogWarning("CharacterLookController: cameraHolder non è assegnato. Il pitch non sarà applicato.", this);
        }
    }

    private void Update()
    {
        HandleLook();
    }

    private void OnDisable()
    {
        if (inputHandler != null) inputHandler.SetCursorLocked(false);
    }

    // 6) Metodi pubblici
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

    // 7) Metodi privati
    private void HandleLook()
    {
        // leggi look dal manager locale (preferenza)
        if (inputHandler == null) return;

        Vector2 rawDelta = inputHandler.look;

        // invert Y se richiesto
        if (invertY) rawDelta.y = -rawDelta.y;

        // applica sensitivity (no deltaTime, no smoothing)
        float deltaYaw = rawDelta.x * Sensitivity * .01f;
        float deltaPitch = rawDelta.y * Sensitivity * .01f;

        Yaw += deltaYaw;
        Pitch += deltaPitch;
        Pitch = Mathf.Clamp(Pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(-Pitch, 0f, 0f);
        }
    }

    private void ApplyRotationInstant()
    {
        transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
        }
    }
}
