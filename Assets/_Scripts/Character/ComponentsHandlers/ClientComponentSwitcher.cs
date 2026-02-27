using SyncedRush.Character.Movement;
using SyncedRush.Gamemode;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Enables/disables components based on ownership and server authority.
/// </summary>
public class ClientComponentSwitcher : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("Action Map Names")]
    [SerializeField] private string gameplayMapName = "Player";
    [SerializeField] private string uiMapName = "UI";

    [Header("Character Components (Owner Only)")]
    [SerializeField] private LookController lookController;

    [Header("Character Components (Server Only)")]
    [SerializeField] private MovementController moveController;
    [SerializeField] private CharacterMovementFSM movementFSM;
    [SerializeField] private HealthSystem healthSystem;

    [Header("Camera System")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private CinemachineCamera cineCam;
    [SerializeField] private AudioListener audioListener;

    [Header("Weapon Components (Owner Only)")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private ShootingSystem shootingSystem;

    [Header("Weapon Components (Server Only)")]
    [SerializeField] private WeaponNetworkHandler weaponNetworkHandler;

    [Header("UI Components")]
    [SerializeField] private UIManager uiManager;

    [Header("Services")]
    [SerializeField] private GameplayServicesRef services;

    [Header("Client Systems")]
    [SerializeField] private ClientSystems clientSystems;

    public void SetClientSystems(ClientSystems systems) => clientSystems = systems;

    // ✅ runtime flags for gating input generation/sending
    public bool IsMovementGameplayEnabled { get; private set; } = true;
    public bool IsWeaponGameplayEnabled { get; private set; } = true;

    /// <summary>
    /// True only when gameplay is intended to be active (movement+weapon).
    /// Used by NetworkPlayerInput to decide whether to send real inputs.
    /// </summary>
    public bool IsGameplayInputAllowed => IsMovementGameplayEnabled && IsWeaponGameplayEnabled;

    private void Awake()
    {
        if (playerInput != null) playerInput.enabled = false;
        if (inputHandler != null) inputHandler.enabled = false;

        if (lookController != null) lookController.enabled = false;
        if (mainCamera != null) mainCamera.enabled = false;
        if (brain != null) brain.enabled = false;
        if (cineCam != null) cineCam.enabled = false;
        if (audioListener != null) audioListener.enabled = false;

        // ✅ MovementController must stay enabled (it replicates snapshots / handles teleports)
        if (moveController != null) moveController.enabled = true;
        if (movementFSM != null) movementFSM.enabled = false;

        if (weaponController != null) weaponController.enabled = false;
        if (shootingSystem != null) shootingSystem.enabled = false;

        if (healthSystem != null) healthSystem.enabled = false;

        if (services == null) services = FindFirstObjectByType<GameplayServicesRef>();
    }

    private void OnEnable()
    {
        RoundManager.OnMatchFlowStateChanged += HandleMatchFlowChange;
    }

    private void OnDisable()
    {
        RoundManager.OnMatchFlowStateChanged -= HandleMatchFlowChange;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        bool isOwner = IsOwner;
        bool isServer = IsServer;

        if (playerInput != null) playerInput.enabled = isOwner;
        if (inputHandler != null) inputHandler.enabled = isOwner;

        if (lookController != null) lookController.enabled = isOwner;

        if (mainCamera != null) mainCamera.enabled = isOwner;
        if (brain != null) brain.enabled = isOwner;
        if (cineCam != null) cineCam.enabled = isOwner;
        if (audioListener != null) audioListener.enabled = isOwner;

        // ✅ keep MC enabled always, but FSM only on (server || owner)
        if (moveController != null) moveController.enabled = true;
        if (movementFSM != null) movementFSM.enabled = isServer || isOwner;

        UpdateWeaponComponentState();

        if (healthSystem != null) healthSystem.enabled = isServer;

        if (isOwner)
        {
            ClientComponentSwitcherLocal.Local = this;
            clientSystems?.UI?.RegisterPlayer(gameObject);

            if (uiManager != null)
                uiManager.UIRegisterPlayer(gameObject);
        }

        EnsureGlobalMapEnabled();

        // initialize flags to "whatever components are currently enabled"
        IsMovementGameplayEnabled = (movementFSM != null) ? movementFSM.enabled : true;
        IsWeaponGameplayEnabled = (weaponController != null) ? weaponController.enabled : true;

        // ✅ force apply current flow state immediately (no waiting for event)
        var rm = SessionServices.Current != null ? SessionServices.Current.RoundManager : FindFirstObjectByType<RoundManager>();
        if (rm != null)
        {
            var state = rm.CurrentFlowState.Value;
            HandleMatchFlowChange(state, state); // call with same old/new to apply gates
        }
        else
        {
            // conservative default: block gameplay until flow arrives
            SetMovementGameplayEnabled(false);
            SetWeaponGameplayEnabled(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsOwner && ClientComponentSwitcherLocal.Local == this)
            ClientComponentSwitcherLocal.Local = null;
    }

    public static class ClientComponentSwitcherLocal
    {
        public static ClientComponentSwitcher Local;
    }

    public void RegisterWeapon(WeaponController wc, ShootingSystem ss, WeaponNetworkHandler wh)
    {
        weaponController = wc;
        shootingSystem = ss;
        weaponNetworkHandler = wh;

        if (services != null)
        {
            if (weaponController != null)
                weaponController.SetServices(services.weaponFx, services.WeaponAudio);

            if (shootingSystem != null)
                shootingSystem.SetServices(services.weaponFx, services.WeaponAudio);
        }

        UpdateWeaponComponentState();

        if (IsOwner)
        {
            clientSystems?.UI?.RegisterWeapon(wc);
            if (uiManager != null)
                uiManager.UIRegisterWeapon(wc);
        }
    }

    private void UpdateWeaponComponentState()
    {
        bool isOwner = IsOwner;
        bool isServer = IsServer;

        if (weaponController != null) weaponController.enabled = isOwner;
        if (shootingSystem != null) shootingSystem.enabled = isOwner;

        if (weaponNetworkHandler != null) weaponNetworkHandler.enabled = isServer || isOwner;
    }

    public void SetState_UIMenu()
    {
        if (playerInput != null && !playerInput.enabled)
            playerInput.enabled = true;

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap(uiMapName);

        EnsureGlobalMapEnabled();

        if (inputHandler != null)
        {
            inputHandler.ClearAllInputs();
            inputHandler.enabled = false;
        }

        if (lookController != null)
            lookController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SetState_Loadout()
    {
        if (playerInput != null && !playerInput.enabled)
            playerInput.enabled = true;

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap(uiMapName);

        EnsureGlobalMapEnabled();

        if (inputHandler != null)
        {
            inputHandler.ClearAllInputs();
            inputHandler.enabled = false;
        }

        if (lookController != null)
            lookController.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SetState_Gameplay()
    {
        if (playerInput != null && !playerInput.enabled)
            playerInput.enabled = true;

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap(gameplayMapName);

        EnsureGlobalMapEnabled();

        if (inputHandler != null)
        {
            inputHandler.ClearAllInputs();
            inputHandler.enabled = true;
        }

        if (lookController != null)
            lookController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void EnsureGlobalMapEnabled()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        var globalMap = playerInput.actions.FindActionMap("Global", throwIfNotFound: false);
        if (globalMap != null && !globalMap.enabled)
            globalMap.Enable();
    }

    public void SetWeaponGameplayEnabled(bool enabled)
    {
        IsWeaponGameplayEnabled = enabled;

        if (weaponController != null) weaponController.enabled = enabled && IsOwner;
        if (shootingSystem != null) shootingSystem.enabled = enabled && IsOwner;

        // never gate this by "enabled"
        if (weaponNetworkHandler != null)
            weaponNetworkHandler.enabled = (IsServer || IsOwner);
    }

    public void SetMovementGameplayEnabled(bool enabled)
    {
        IsMovementGameplayEnabled = enabled;

        // ✅ do NOT disable MovementController
        if (movementFSM != null) movementFSM.enabled = enabled && (IsServer || IsOwner);
        if (moveController != null) moveController.enabled = true;
    }

    private void HandleMatchFlowChange(MatchFlowState oldState, MatchFlowState newState)
    {
        if (newState == MatchFlowState.PreRoundFrozen ||
            newState == MatchFlowState.RoundEnd ||
            newState == MatchFlowState.MatchEnd)
        {
            SetMovementGameplayEnabled(false);
            SetWeaponGameplayEnabled(false);
        }
        else if (newState == MatchFlowState.InRound)
        {
            SetMovementGameplayEnabled(true);
            SetWeaponGameplayEnabled(true);
        }
    }
}