using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterMoveState : CharacterMovementState
	{
        public CharacterMoveState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override MovementState ProcessFixedUpdate()
        {
            base.ProcessFixedUpdate();

            if (!CheckGround())
                return MovementState.Air;

            if (character.Input.Jump)
                return MovementState.Jump;

            Walk();

            ProcessMovement();
            return MovementState.None;
        }

        private bool CheckGround()
        {
            if (character.IsOnGround)
            {
                character.VerticalVelocity = -.1f;
                return true;
            }
            else
                return false;
        }

        private void Walk()
        {
            Vector3 moveDir = character.MoveDirection;

            if (character.Input.Sprint && character.Input.Move.y > 0f )
                character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                    new Vector2(moveDir.x, moveDir.z) * character.Stats.RunSpeed,
                    Time.fixedDeltaTime * character.Stats.RunSpeed * 10);
            else
                character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                    new Vector2(moveDir.x, moveDir.z) * character.Stats.WalkSpeed,
                    Time.fixedDeltaTime * character.Stats.RunSpeed * 10);
            //character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity, 
            //    new Vector2(motion.x, motion.z) * character.Stats.WalkSpeed, 
            //    Time.fixedDeltaTime * 10f);
        }

	}
}