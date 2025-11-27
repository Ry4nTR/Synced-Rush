using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attiva/disattiva componenti all'interno del player controllando se il player è locale (IsOwner) o server (IsServer).
/// <!--/summary>-->
public class ClientComponentSwitcher : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("Character Components")]
    [SerializeField] private LookController lookController;
    [SerializeField] private MovementController characterController;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera CinemachineCamera;


    private void Awake()
    {
        playerInput.enabled = false; // PlayerInput
        inputHandler.enabled = false; // PlayerInputHandler
        lookController.enabled = false; // CharacterLookController
        characterController.enabled = false; // MovementController
        CinemachineCamera.enabled = false; // Camera
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            playerInput.enabled = true;
            inputHandler.enabled = true;

            CinemachineCamera.enabled = true;
        }

        if (IsServer)
        {
            lookController.enabled = true;
            characterController.enabled = true;
        }
    }

    [Rpc(SendTo.Server)]
    private void UpdateInputServerRpc(Vector2 move, Vector2 look, bool jump, bool sprint, bool fire, bool aim)
    {
        inputHandler.MoveInput(move);
        inputHandler.LookInput(look);
        inputHandler.JumpInput(jump);
        inputHandler.SprintInput(sprint);
        inputHandler.FireInput(fire);
        inputHandler.AimInput(aim);
    }

    private void LateUpdate()
    {
        if(!IsOwner)
            return;

        UpdateInputServerRpc(inputHandler.move, inputHandler.look, inputHandler.jump, inputHandler.sprint, inputHandler.fire, inputHandler.aim);
    }

}
