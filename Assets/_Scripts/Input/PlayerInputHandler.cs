using UnityEngine;

/// <summary>
/// Centralized input buffer using Unity Input System.
/// Continuous inputs are polled, discrete actions are edge-checked.
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Character Input Values")]
    [SerializeField] private Vector2 move;
    [SerializeField] private Vector2 look;
    [SerializeField] private bool sprint;
    [SerializeField] private bool crouch;
    [SerializeField] private bool fire;
    [SerializeField] private bool aim;
    [SerializeField] private bool jetpack;

    [SerializeField] private int jumpPressCount;
    [SerializeField] private int reloadPressCount;
    [SerializeField] private int abilityPressCount;

    private PlayerInputSystem _controls;

    // Read-only accessors
    public Vector2 Move => move;
    public Vector2 Look => look;
    public bool Sprint => sprint;
    public bool Crouch => crouch;
    public bool Fire => fire;
    public bool Aim => aim;
    public bool Jetpack => jetpack;
    public int JumpCount => jumpPressCount;
    public int ReloadCount => reloadPressCount;
    public int AbilityCount => abilityPressCount;

    private void Awake()
    {
        _controls = new PlayerInputSystem();
    }

    private void OnEnable()
    {
        SetCursorLocked(true);
        _controls.Enable();
    }

    private void OnDisable()
    {
        _controls.Disable();
    }

    private void Update()
    {
        ReadContinuousInputs();
        ReadDiscreteInputs();
    }
    private void FixedUpdate()
    {
        ReadContinuousInputs();
    }


    // Continuous input polling (every frame)
    private void ReadContinuousInputs()
    {
        move = _controls.Player.Move.ReadValue<Vector2>();
        look = _controls.Player.Look.ReadValue<Vector2>();

        fire = _controls.Player.Fire.IsPressed();
        aim = _controls.Player.Aim.IsPressed();
        sprint = _controls.Player.Sprint.IsPressed();
        crouch = _controls.Player.Crouch.IsPressed();

        jetpack = _controls.Player.Jetpack.IsPressed();
    }

    // Discrete input edge-checking (latched until consumed by FixedUpdate)
    private void ReadDiscreteInputs()
    {
        if (_controls.Player.Jump.WasPressedThisFrame())
            jumpPressCount++;

        if (_controls.Player.Reload.WasPressedThisFrame())
            reloadPressCount++;

        if (_controls.Player.Ability.WasPressedThisFrame())
            abilityPressCount++;
    }


    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
    public void ClearAllInputs()
    {
        move = Vector2.zero;
        look = Vector2.zero;
        sprint = false;
        crouch = false;
        fire = false;
        aim = false;
        jetpack = false;
        jumpPressCount = 0;
        reloadPressCount = 0;
        abilityPressCount = 0;
    }
}
