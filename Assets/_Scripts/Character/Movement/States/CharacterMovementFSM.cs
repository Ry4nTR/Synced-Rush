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

            Dictionary<MovementState, CharacterMovementState> states = new()
            {
                { MovementState.Move, moveState },
                { MovementState.Air, airState },
                { MovementState.Jump, airState }
            };

            Initialize(states, MovementState.Move);
        }

        public void ProcessCollision(ControllerColliderHit hit)
        {
            CurrentState.ProcessCollision(hit);
        }
    }
}