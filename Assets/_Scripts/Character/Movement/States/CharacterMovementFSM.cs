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
	}

    [RequireComponent(typeof(MovementController))]
    public class CharacterMovementFSM : BaseStateMachine<MovementState, CharacterMovementState>
	{

        private MovementController _movementComponent;

        //private MovementState _previousState;

        private void Awake()
        {
            _movementComponent = GetComponent<MovementController>();

            CharacterMoveState moveState = new(_movementComponent);
            CharacterAirState airState = new(_movementComponent);
            CharacterSlideState slideState = new(_movementComponent);
            CharacterWallRunState wallRunState = new(_movementComponent);

            Dictionary<MovementState, CharacterMovementState> states = new()
            {
                { MovementState.Move, moveState },
                { MovementState.Air, airState },
                { MovementState.Jump, airState },
                { MovementState.Slide, slideState },
                { MovementState.WallRun, wallRunState }
            };

            Initialize(states, MovementState.Move);

            //_previousState = MovementState.Move;
        }

        /*
        FALLIMENTO MISERABILE NEL PROVARE A METTERE IL TRIGGER PER L'ANIMAZIONE DEL SALTO QUI.
        LO LASCIO COSI SAI CHE METODO VA USATO PER AVVIARE IL TRIGGER

        private void FixedUpdate()
        {
            MovementState oldState = CurrentStateEnum;

            ProcessFixedUpdate(); // Calls the BaseStateMachine logic

            MovementState newState = CurrentStateEnum;

            // Detect entering Jump
            if (newState == MovementState.Jump && oldState != MovementState.Jump)
            {
                _movementComponent.Anim.SetTrigger("JumpTrigger");
                _movementComponent.Anim.SetBool("IsJumping", true);
            }

            // Detect exiting Jump
            if (oldState == MovementState.Jump && newState != MovementState.Jump)
            {
                _movementComponent.Anim.SetBool("IsJumping", false);
            }

            _previousState = newState;
        }
        */


        public void ProcessCollision(ControllerColliderHit hit)
        {
            CurrentState.ProcessCollision(hit);
        }
    }
}