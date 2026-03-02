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
        protected SimulationTickData Input => character.CurrentInput;

        private CharacterMovementFSM _parentStateMachine;
        protected CharacterMovementFSM ParentStateMachine => _parentStateMachine;

        private int _lastAbilityCount = -1;

        protected bool ConsumeAbilityPressed() => ConsumePress(ref _lastAbilityCount, Input.AbilityCount);

        protected void FlushAbility() => FlushPress(ref _lastAbilityCount, Input.AbilityCount);

        protected CharacterMovementState(MovementController movementComponentReference)
        {
            character = movementComponentReference;
        }

        public override MovementState ProcessUpdate()
        {
            ParentStateMachine.TickCooldowns();
            return MovementState.None;
        }

        public override void ExitState()
        {
            base.ExitState();
            ProcessMovement();
        }

        public virtual void ProcessCollision(ControllerColliderHit hit) { }

        protected virtual void ProcessMovement()
        {
            Vector3 attemptedVelocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);
            Vector3 startPos = character.transform.position;

            // 1. Execute the move
            character.Controller.Move(attemptedVelocity * Time.fixedDeltaTime);

            // 2. Kill residual momentum if we hit a wall/ceiling/floor
            if (character.Controller.collisionFlags != CollisionFlags.None)
            {
                // Calculate the exact velocity we actually achieved after Unity resolved the collision
                Vector3 actualVelocity = (character.transform.position - startPos) / Time.fixedDeltaTime;

                // If we hit a wall, bleed off the horizontal momentum we lost
                if ((character.Controller.collisionFlags & CollisionFlags.Sides) != 0)
                {
                    character.HorizontalVelocity = new Vector2(actualVelocity.x, actualVelocity.z);
                }

                // If we hit a ceiling, kill the upward momentum so we don't float
                if ((character.Controller.collisionFlags & CollisionFlags.Above) != 0 && character.VerticalVelocity > 0)
                {
                    character.VerticalVelocity = 0f;
                }
            }
        }

        protected bool ConsumePress(ref int lastCount, int currentCount)
        {
            if (lastCount < 0)
            {
                lastCount = currentCount;
                return false;
            }

            bool pressed = currentCount > lastCount;

            if (currentCount != lastCount)
                lastCount = currentCount;

            return pressed;
        }
        protected void FlushPress(ref int lastCount, int currentCount)
        {
            if (lastCount < 0) lastCount = currentCount;
            else lastCount = currentCount;
        }


        public void SetParentStateMachine(CharacterMovementFSM parentStateMachine)
        {
            if (_parentStateMachine == null)
                _parentStateMachine = parentStateMachine;
        }
    }
}