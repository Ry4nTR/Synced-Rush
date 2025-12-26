using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PlayerInputHandler:
/// Component da mettere sul player che riceve input via PlayerInput (Send Messages)
/// e rende disponibili helper / valori per gli altri script (es. Player controller, LookController).
/// </summary>
[DisallowMultipleComponent]
public class PlayerInputHandler : MonoBehaviour
{
    // 1) Campi serializzati
    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnEnable = true;

    // 2) Campi pubblici (imitando StarterAssetsInputs)
    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 look;
    public bool jump;
    public bool sprint;
    public bool crouch; // è usato anche per lo slide
    public bool fire;
    public bool aim;
    public bool reload;
    public float scroll;
    public bool toggleWeaponPanel;

    //TODO da rimuovere quando non serve più
    public bool debugResetPos;

    // 3) Campi privati
    private PlayerInput _playerInput;
    private PlayerInputSystem _controls;

    // Proprietà campi input (usare queste al di fuori di questa classe)
    public Vector2 Move => move;
    public Vector2 Look => look;
    public bool Jump => jump;
    public bool Sprint => sprint;
    public bool Crouch => crouch;
    public bool Fire => fire;
    public bool Aim => aim;
    public bool Reload => reload;
    public float Scroll => scroll;

    // 4) Event facoltativi
    public event Action<Vector2> OnLookEvent = delegate { };
    public event Action OnToggleWeaponPanelEvent = delegate { };

    // 5) MonoBehaviour methods
    private void Awake()
    {
        _controls = new PlayerInputSystem();
        _playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        if (lockCursorOnEnable)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (_controls != null)
        {
            _controls.Enable();
            SubscribeAllInputs();
        }

        // reset iniziale
        move = Vector2.zero;
        look = Vector2.zero;
        jump = false;
        sprint = false;
        fire = false;
        aim = false;
        reload = false;
        scroll = 0f;
        toggleWeaponPanel = false;


        //TODO da rimuovere quando non serve più
        debugResetPos = false;
    }

    private void OnDisable()
    {
        if (_controls != null)
        {
            _controls.Disable();
            UnsubscribeAllInputs();
        }
    }

#if ENABLE_INPUT_SYSTEM
    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput(context.ReadValue<Vector2>());
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        LookInput(context.ReadValue<Vector2>());
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        JumpInput(context.performed);
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        SprintInput(context.performed);
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        CrouchInput(context.performed);
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        FireInput(context.performed);
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        AimInput(context.performed);
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        ReloadInput(context.performed);
    }

    public void OnScroll(InputAction.CallbackContext context)
    {
        ScrollInput(context.ReadValue<float>());
    }

    public void OnToggleWeaponPanel(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            toggleWeaponPanel = true;
            OnToggleWeaponPanelEvent.Invoke();
        }
    }

    //TODO da rimuovere quando non serve più
    public void OnDebugResetPos(InputAction.CallbackContext context)
    {
        debugResetPos = context.performed;
    }
#endif




    // 7) Helper publici (altri script leggono questi valori)
    public void MoveInput(Vector2 newMove)
    {
        move = newMove;
    }

    public void LookInput(Vector2 newLook)
    {
        look = newLook;
        OnLookEvent.Invoke(look);
    }

    public void JumpInput(bool newJump)
    {
        jump = newJump;
    }

    public void SprintInput(bool newSprint)
    {
        sprint = newSprint;
    }

    public void CrouchInput(bool newCrouch)
    {
        crouch = newCrouch;
    }

    public void FireInput(bool newFire)
    {
        fire = newFire;
    }

    public void AimInput(bool newAim)
    {
        aim = newAim;
    }

    public void ReloadInput(bool newReload)
    {
        reload = newReload;
    }

    public void ScrollInput(float newScroll)
    {
        scroll = newScroll;
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // Metodi privati
    private void SubscribeAllInputs()
    {
        _controls.Player.Move.performed += OnMove;
        _controls.Player.Move.canceled += OnMove;

        _controls.Player.Look.performed += OnLook;
        _controls.Player.Look.canceled += OnLook;

        _controls.Player.Jump.performed += OnJump;
        _controls.Player.Jump.canceled += OnJump;

        _controls.Player.Sprint.performed += OnSprint;
        _controls.Player.Sprint.canceled += OnSprint;

        _controls.Player.Crouch.performed += OnCrouch;
        _controls.Player.Crouch.canceled += OnCrouch;

        _controls.Player.Fire.performed += OnFire;
        _controls.Player.Fire.canceled += OnFire;

        _controls.Player.Aim.performed += OnAim;
        _controls.Player.Aim.canceled += OnAim;

        _controls.Player.Reload.performed += OnReload;
        _controls.Player.Reload.canceled += OnReload;

        _controls.Player.ToggleWeaponPanel.performed += OnToggleWeaponPanel;

        //TODO da rimuovere quando non serve più
        _controls.Player.DebugResetPos.performed += OnDebugResetPos;
        _controls.Player.DebugResetPos.canceled += OnDebugResetPos;
    }
    private void UnsubscribeAllInputs()
    {
        _controls.Player.Move.performed -= OnMove;
        _controls.Player.Move.canceled -= OnMove;

        _controls.Player.Look.performed -= OnLook;
        _controls.Player.Look.canceled -= OnLook;

        _controls.Player.Jump.performed -= OnJump;
        _controls.Player.Jump.canceled -= OnJump;

        _controls.Player.Sprint.performed -= OnSprint;
        _controls.Player.Sprint.canceled -= OnSprint;

        _controls.Player.Crouch.performed -= OnCrouch;
        _controls.Player.Crouch.canceled -= OnCrouch;

        _controls.Player.Fire.performed -= OnFire;
        _controls.Player.Fire.canceled -= OnFire;

        _controls.Player.Aim.performed -= OnAim;
        _controls.Player.Aim.canceled -= OnAim;

        _controls.Player.Reload.performed -= OnReload;
        _controls.Player.Reload.canceled -= OnReload;

        _controls.Player.ToggleWeaponPanel.performed -= OnToggleWeaponPanel;

        //TODO da rimuovere quando non serve più
        _controls.Player.DebugResetPos.performed -= OnDebugResetPos;
        _controls.Player.DebugResetPos.canceled -= OnDebugResetPos;
    }
}
