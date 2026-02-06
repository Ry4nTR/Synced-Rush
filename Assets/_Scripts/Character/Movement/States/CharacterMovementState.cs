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

        public override void ProcessLateUpdate()
        {
            CheckCollision();
        }

        public override void ExitState()
        {
            base.ExitState();
            ProcessMovement();
        }

        private void CheckCollision()
        {
            var collision = character.LastCollision;
            if (collision.hasCollision)
            {
                Debug.Log("YES YES YES");
                HandleCollision(collision);
            }
        }

        protected virtual void HandleCollision(CollisionInfo collision) { }

        protected virtual void ProcessMovement()
        {
            Vector3 _velocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);
            character.Controller.Move(_velocity * Time.fixedDeltaTime);
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