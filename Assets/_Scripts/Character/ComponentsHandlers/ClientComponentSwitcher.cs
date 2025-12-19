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

    [Header("Camera System")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private CinemachineCamera cineCam;
    [SerializeField] private AudioListener audioListener;

    [Header("Weapon Components (Owner Only)")]
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private ShootingSystem shootingSystem;

    [Header("Weapon Components (Server Only)")]
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private WeaponNetworkHandler weaponNetworkHandler;

    // Flag to indicate whether weapon components were registered dynamically
    private bool weaponComponentsInitialized;


    private void Awake()
    {
        // Disable everything at startup, OnNetworkSpawn decides what to enable
        if (playerInput != null) playerInput.enabled = false;
        if (inputHandler != null) inputHandler.enabled = false;

        if (lookController != null) lookController.enabled = false;
        if (mainCamera != null) mainCamera.enabled = false;
        if (brain != null) brain.enabled = false;
        if (cineCam != null) cineCam.enabled = false;
        if (audioListener != null) audioListener.enabled = false;


        if (moveController != null) moveController.enabled = true;
        if (movementFSM != null) movementFSM.enabled = false;

        // Weapon components
        if (weaponController != null) weaponController.enabled = false;
        if (shootingSystem != null) shootingSystem.enabled = false;
        if (healthSystem != null) healthSystem.enabled = false;
        if (weaponNetworkHandler != null) weaponNetworkHandler.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        bool isOwner = IsOwner;
        bool isServer = IsServer;

        // OWNER-ONLY components (camera, input, look)
        if (playerInput != null) playerInput.enabled = isOwner;
        if (inputHandler != null) inputHandler.enabled = isOwner;
        if (lookController != null) lookController.enabled = isOwner;

        if (mainCamera != null) mainCamera.enabled = isOwner;
        if (brain != null) brain.enabled = isOwner;
        if (cineCam != null) cineCam.enabled = isOwner;
        if (audioListener != null) audioListener.enabled = isOwner;

        // SERVER-ONLY components (movement simulation)
        if (moveController != null) moveController.enabled = isServer || isOwner;
        if (movementFSM != null) movementFSM.enabled = isServer || isOwner;

        // Enable weapon systems for owner and server appropriately. If weapon
        // components have not been registered yet (e.g. before a weapon is
        // equipped), this call will not have any effect. When RegisterWeapon
        // is invoked, these components will be enabled based on authority.
        UpdateWeaponComponentState();
        if (healthSystem != null) healthSystem.enabled = isServer;
    }

    /// <summary>
    /// Registers weapon-related components when a weapon is spawned. The
    /// inventory system should call this to wire up the runtime weapon scripts
    /// to the component switcher. This allows the switcher to enable or
    /// disable the weapon controller, shooting system and network handler
    /// according to network authority.
    /// </summary>
    /// <param name="weaponCtrl">The weapon controller on the spawned view model.</param>
    /// <param name="shootSys">The shooting system on the spawned view model.</param>
    /// <param name="netHandler">The network handler on the spawned view model.</param>
    public void RegisterWeapon(WeaponController weaponCtrl, ShootingSystem shootSys, WeaponNetworkHandler netHandler)
    {
        weaponController = weaponCtrl;
        shootingSystem = shootSys;
        weaponNetworkHandler = netHandler;
        weaponComponentsInitialized = true;
        UpdateWeaponComponentState();
    }

    /// <summary>
    /// Enables or disables weapon components based on current ownership and
    /// server status. Called from OnNetworkSpawn and after a weapon is
    /// registered to ensure the correct state.
    /// </summary>
    private void UpdateWeaponComponentState()
    {
        if (!weaponComponentsInitialized)
            return; // Skip updating if weapon components haven't been registered yet

        bool isOwner = IsOwner;
        bool isServer = IsServer;
        if (weaponController != null) weaponController.enabled = isOwner;
        if (shootingSystem != null) shootingSystem.enabled = isOwner;
        if (weaponNetworkHandler != null) weaponNetworkHandler.enabled = isServer || isOwner;
    }
}
