using UnityEngine;

/// <summary>
/// Centralized input buffer using Unity Input System.
/// Continuous inputs are polled, discrete actions are edge-checked.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Character Input Values")]
    [SerializeField] private Vector2 move;
    [SerializeField] private Vector2 look;
    [SerializeField] private bool jump;
    [SerializeField] private bool sprint;
    [SerializeField] private bool crouch;
    [SerializeField] private bool fire;
    [SerializeField] private bool aim;
    [SerializeField] private bool reload;
    [SerializeField] private bool ability;
    [SerializeField] private bool jetpack;

    private PlayerInputSystem _controls;

    // Read-only accessors
    public Vector2 Move => move;
    public Vector2 Look => look;
    public bool Jump => jump;
    public bool Sprint => sprint;
    public bool Crouch => crouch;
    public bool Fire => fire;
    public bool Aim => aim;
    public bool Reload => reload;
    public bool Ability => ability;
    public bool Jetpack => jetpack;

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
        // LATCH: once true, it stays true until NetworkPlayerInput consumes it
        jump |= _controls.Player.Jump.WasPressedThisFrame();
        reload |= _controls.Player.Reload.WasPressedThisFrame();
        ability |= _controls.Player.Ability.WasPressedThisFrame();
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
        jump = false;
        sprint = false;
        crouch = false;
        fire = false;
        aim = false;
        reload = false;
        ability = false;
        jetpack = false;
    }

    public bool ConsumeJump()
    {
        bool v = jump;
        jump = false;
        return v;
    }

    public bool ConsumeAbility()
    {
        bool v = ability;
        ability = false;
        return v;
    }

    public bool ConsumeReload()
    {
        bool v = reload;
        reload = false;
        return v;
    }
}
