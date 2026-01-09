using SyncedRush.Generics;
using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public abstract class CharacterMovementState : BaseState<MovementState>
	{
		protected MovementController character;

        /// <summary>
        /// Shortcut to SERVER-SIDE input.
        /// </summary>
        protected GameplayInputData Input => character.InputData;

        private CharacterMovementFSM _parentStateMachine;
        protected CharacterMovementFSM ParentStateMachine => _parentStateMachine;

        protected CharacterMovementState(MovementController movementComponentReference)
        {
            character = movementComponentReference;
        }

        public override void ExitState()
        {
            base.ExitState();
            ProcessMovement();
        }

        public virtual void ProcessCollision(ControllerColliderHit hit) { }

        protected virtual void ProcessMovement()
        {
            Vector3 _velocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);
            character.Controller.Move(_velocity * Time.deltaTime);
        }

        public void SetParentStateMachine(CharacterMovementFSM parentStateMachine)
        {
            if (_parentStateMachine == null)
                _parentStateMachine = parentStateMachine;
        }
    }
}