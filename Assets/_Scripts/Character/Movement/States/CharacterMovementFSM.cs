using UnityEngine;
using SyncedRush.Generics;
using System.Collections.Generic;

namespace SyncedRush.Character.Movement
{
	public enum MovementState
	{
		None = 0,
		Move = 1,
		Air = 2,
        Jump = 3,
        Slide = 4,
        WallRun = 5,
        Dash = 6,
        GrappleHook = 7,
	}

    [RequireComponent(typeof(MovementController))]
    public class CharacterMovementFSM : BaseStateMachine<MovementState, CharacterMovementState>
	{
        private MovementController _movementComponent;

        private void Awake()
        {
            _movementComponent = GetComponent<MovementController>();

            CharacterMoveState moveState = new(_movementComponent);
            CharacterAirState airState = new(_movementComponent);
            CharacterJumpState jumpState = new(_movementComponent);
            CharacterSlideState slideState = new(_movementComponent);
            CharacterWallRunState wallRunState = new(_movementComponent);
            CharacterDashState dashState = new(_movementComponent);
            CharacterGrappleHookState grappleHookState = new(_movementComponent);

            Dictionary<MovementState, CharacterMovementState> states = new()
            {
                { MovementState.Move, moveState },
                { MovementState.Air, airState },
                { MovementState.Jump, jumpState },
                { MovementState.Slide, slideState },
                { MovementState.WallRun, wallRunState },
                { MovementState.Dash, dashState },
                { MovementState.GrappleHook, grappleHookState }
            };

            foreach (CharacterMovementState state in states.Values)
            {
                state.SetParentStateMachine(this);
            }

            Initialize(states, MovementState.Move);
        }
    }
}