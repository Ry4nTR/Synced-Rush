using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterJumpState : CharacterMovementState
    {
        public CharacterJumpState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "JumpState"; }

        public override void EnterState()
        {
            base.EnterState();

            Jump();
        }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            return MovementState.Air;
        }

        private void Jump()
        {
            float jumpSpeed = Mathf.Sqrt(2 * character.Stats.Gravity * character.Stats.JumpHeight);
            character.VerticalVelocity = jumpSpeed;
        }

    }
}