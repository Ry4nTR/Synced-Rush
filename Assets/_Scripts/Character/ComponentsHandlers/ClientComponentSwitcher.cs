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

    [Header("Action Map Names")]
    [Tooltip("Name of the action map used for gameplay (movement/shooting). This should match the map name in the Input Action asset.")]
    [SerializeField] private string gameplayMapName = "Player";
    [Tooltip("Name of the action map used for UI interactions (menus/loadout). This should match the map name in the Input Action asset.")]
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

        // SERVICES component
        if (services == null) services = FindFirstObjectByType<GameplayServicesRef>();
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


        if (isOwner)
        {
            ClientComponentSwitcherLocal.Local = this;

            // Prefer the new GameplayUIManager if present; fallback to the legacy UIManager.
            clientSystems?.UI?.RegisterPlayer(gameObject);

            if (uiManager != null)
            {
                uiManager.UIRegisterPlayer(gameObject);
            }
        }

        // --- Enable GLOBAL MAP (always on) ---
        EnsureGlobalMapEnabled();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Clear the local pointer when the object despawns on the owner.
        if (IsOwner)
        {
            if (ClientComponentSwitcherLocal.Local == this)
                ClientComponentSwitcherLocal.Local = null;
        }
    }

    // Holds a static reference to the local client's component switcher.
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
            // When owned, register the weapon with whichever UI manager is available.
            clientSystems?.UI?.RegisterWeapon(wc);
            if (uiManager != null)
            {
                // Fallback: register with the old UIManager so ammo displays update
                uiManager.UIRegisterWeapon(wc);
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
        // Ensure input is enabled before switching maps
        if (playerInput != null && !playerInput.enabled)
            playerInput.enabled = true;
        // Switch to UI map
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
        // UI mode but keep ability to select weapons/abilities
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
        // Ensure input is enabled before switching maps
        if (playerInput != null && !playerInput.enabled)
            playerInput.enabled = true;
        // Switch to gameplay map
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

    /// <summary>
    /// Helpers
    /// </summary>
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
        if (weaponController != null) weaponController.enabled = enabled && IsOwner;
        if (shootingSystem != null) shootingSystem.enabled = enabled && IsOwner;

        // Keep server weapon handler running even if local owner paused (important for host)
        if (weaponNetworkHandler != null)
            weaponNetworkHandler.enabled = (IsServer || IsOwner); // don't gate it by "enabled"
    }

    public void SetMovementGameplayEnabled(bool enabled)
    {
        // inputHandler and look are already toggled by SetState_Gameplay/Loadout,
        // but this is a hard kill-switch for gameplay simulation.
        if (movementFSM != null) movementFSM.enabled = enabled && (IsServer || IsOwner);
        if (moveController != null) moveController.enabled = enabled; // your MC is always enabled; keep if you want
    }
}
