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


        if (moveController != null) moveController.enabled = false;
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
        if (moveController != null) moveController.enabled = isServer;
        if (movementFSM != null) movementFSM.enabled = isServer;

        // Enable weapon systems for owner and server appropriately
        if (weaponController != null) weaponController.enabled = isOwner;
        if (shootingSystem != null) shootingSystem.enabled = isOwner;
        if (weaponNetworkHandler != null) weaponNetworkHandler.enabled = isServer || isOwner;
        if (healthSystem != null) healthSystem.enabled = isServer;
    }
}
