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

        public virtual void ProcessCollision(ControllerColliderHit hit)
        {
            if (character.Controller.isGrounded && hit.normal.y > 0.7f)
                return;

            Vector3 wallNormal = hit.normal;

            Vector3 currentVelocity = new(character.HorizontalVelocity.x, 0, character.HorizontalVelocity.y);

            Vector3 projectedVelocity = Vector3.ProjectOnPlane(currentVelocity, wallNormal);

            character.HorizontalVelocity = new Vector2(projectedVelocity.x, projectedVelocity.z);
        }

        protected virtual void ProcessMovement()
        {
            Vector3 _velocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);
            character.Controller.Move(_velocity * Time.fixedDeltaTime);
        }
    }
}