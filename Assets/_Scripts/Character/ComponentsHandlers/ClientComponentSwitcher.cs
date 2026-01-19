using SyncedRush.Character.Movement;
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

    private void Awake()
    {
        // INPUT components
        if (playerInput != null) playerInput.enabled = false;
        if (inputHandler != null) inputHandler.enabled = false;

        // LOOK components
        if (lookController != null) lookController.enabled = false;
        if (mainCamera != null) mainCamera.enabled = false;
        if (brain != null) brain.enabled = false;
        if (cineCam != null) cineCam.enabled = false;
        if (audioListener != null) audioListener.enabled = false;

        // MOVEMENT components
        if (moveController != null) moveController.enabled = true;
        if (movementFSM != null) movementFSM.enabled = false;

        // WEAPON components
        if (weaponController != null) weaponController.enabled = false;
        if (shootingSystem != null) shootingSystem.enabled = false;

        // HEALTH component
        if (healthSystem != null) healthSystem.enabled = false;

        // --- Enable GLOBAL MAP (always on) ---
        var globalMap = playerInput.actions.FindActionMap("Global");
        if (globalMap != null)
            globalMap.Enable();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        bool isOwner = IsOwner;
        bool isServer = IsServer;

        // INPUT components (owner only)
        if (playerInput != null) playerInput.enabled = isOwner;
        if (inputHandler != null) inputHandler.enabled = isOwner;

        // LOOK components (owner only)
        if (lookController != null) lookController.enabled = isOwner;

        // CAMERA components (owner only)
        if (mainCamera != null) mainCamera.enabled = isOwner;
        if (brain != null) brain.enabled = isOwner;
        if (cineCam != null) cineCam.enabled = isOwner;
        if (audioListener != null) audioListener.enabled = isOwner;

        // MOVEMENT components (server + owner)
        if (moveController != null) moveController.enabled = true;
        if (movementFSM != null) movementFSM.enabled = isServer || isOwner;

        // WEAPON components
        UpdateWeaponComponentState();

        // HEALTH component (server only)
        if (healthSystem != null) healthSystem.enabled = isServer;


        // Register the local player with the Gameplay UI once all components are initialized.
        if (IsOwner)
        {
            GameplayUIManager.Instance?.RegisterPlayer(gameObject);

            // IMPORTANT: store a static pointer so UI can always reach the local switcher
            ClientComponentSwitcherLocal.Local = this;
        }

        LogState("OnNetworkSpawn END");
    }
    public static class ClientComponentSwitcherLocal
    {
        public static ClientComponentSwitcher Local;
    }


    // Registers weapon components it's spawned + Registers it to the UI Manager
    public void RegisterWeapon(WeaponController wc, ShootingSystem ss, WeaponNetworkHandler wh)
    {
        weaponController = wc;
        shootingSystem = ss;
        weaponNetworkHandler = wh;

        UpdateWeaponComponentState();

        if (IsOwner)
        {
            var gameplayUI = GameplayUIManager.Instance;
            if (gameplayUI != null)
            {
                gameplayUI.RegisterWeapon(wc);
            }
        }
    }

    // Manages weapon component states based on ownership and server authority.
    private void UpdateWeaponComponentState()
    {
        bool isOwner = IsOwner;
        bool isServer = IsServer;
        if (weaponController != null) weaponController.enabled = isOwner;
        if (shootingSystem != null) shootingSystem.enabled = isOwner;
        if (weaponNetworkHandler != null) weaponNetworkHandler.enabled = isServer || isOwner;
    }

    /// <summary>
    /// SETS INPUT STATE TO UI MENU
    /// </summary>
    public void SetState_UIMenu()
    {
        playerInput.SwitchCurrentActionMap("UI");
        inputHandler.ClearAllInputs();
        inputHandler.enabled = false;
        lookController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        LogState("SetState_UIMenu");
    }

    public void SetState_Loadout()
    {
        // UI mode but keep ability to select weapons
        playerInput.SwitchCurrentActionMap("UI");
        inputHandler.ClearAllInputs();
        inputHandler.enabled = false;
        lookController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        LogState("SetState_Loadout");
    }

    public void SetState_Gameplay()
    {
        playerInput.SwitchCurrentActionMap("Player");
        inputHandler.enabled = true;
        lookController.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        LogState("SetState_Gameplay");
    }

    private void LogState(string from)
    {
        string map = playerInput != null && playerInput.currentActionMap != null
            ? playerInput.currentActionMap.name
            : "NULL";

        Debug.Log(
            $"[InputState] {from} | owner={IsOwner} server={IsServer} " +
            $"map={map} inputHandler={(inputHandler ? inputHandler.enabled : false)} " +
            $"look={(lookController ? lookController.enabled : false)} " +
            $"cursorLock={Cursor.lockState} cursorVisible={Cursor.visible}",
            this
        );
    }

}
