using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterAirState : CharacterMovementState
    {
        public CharacterAirState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override MovementState ProcessFixedUpdate()
        {
            base.ProcessFixedUpdate();

            if (CheckGround())
                return MovementState.Move;

            AirMove();
            Fall();

            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            if (character.State == MovementState.Jump)
                Jump();
        }

        private bool CheckGround()
        {
            if (character.Controller.isGrounded && character.VerticalVelocity <= 0f)
            {
                character.VerticalVelocity = -.1f;
                return true;
            }
            else
                return false;
        }

        private void AirMove()
        {
            Vector3 motion = character.Orientation.transform.forward * character.Input.move.y
                + character.Orientation.transform.right * character.Input.move.x;
            motion.y = 0f;
            motion.Normalize();

            character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                new Vector2(motion.x, motion.z) * character.Stats.RunSpeed,
                Time.fixedDeltaTime * character.Stats.RunSpeed * 1);

            //if (character.HorizontalVelocity.magnitude > character.Stats.RunSpeed)
            //{
            //    character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
            //        character.HorizontalVelocity.normalized * character.Stats.RunSpeed,
            //        Time.fixedDeltaTime * 10);
            //}
        }

        private void Fall()
        {
            character.VerticalVelocity -= (character.Stats.Gravity * Time.fixedDeltaTime);
        }

        private void Jump()
        {
            float jumpSpeed = Mathf.Sqrt(2 * character.Stats.Gravity * character.Stats.JumpHeight);
            character.VerticalVelocity = jumpSpeed;
        }

    }
}