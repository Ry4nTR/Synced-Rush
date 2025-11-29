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

    [Header("Camera")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    //[SerializeField] private AudioListener audioListener;

    private void Awake()
    {
        // Disable everything at startup, OnNetworkSpawn decides what to enable
        if (playerInput != null) playerInput.enabled = false;
        if (inputHandler != null) inputHandler.enabled = false;

        if (lookController != null) lookController.enabled = false;
        if (cinemachineCamera != null) cinemachineCamera.enabled = false;
        //if (audioListener != null) audioListener.enabled = false;

        if (moveController != null) moveController.enabled = false;
        if (movementFSM != null) movementFSM.enabled = false;
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

        if (cinemachineCamera != null) cinemachineCamera.enabled = isOwner;
        //if (audioListener != null) audioListener.enabled = isOwner;

        // SERVER-ONLY components (movement simulation)
        if (moveController != null) moveController.enabled = isServer;
        if (movementFSM != null) movementFSM.enabled = isServer;
    }
}
