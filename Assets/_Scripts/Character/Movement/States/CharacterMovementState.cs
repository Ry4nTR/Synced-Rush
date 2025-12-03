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
        protected MovementInputData Input => character.InputData;

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
            character.Controller.Move(_velocity * Time.fixedDeltaTime);
        }
    }
}