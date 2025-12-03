using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterAirState : CharacterMovementState
    {
        private bool _usedDoubleJump = false;
        private bool _previousJumpInput = true;
        private float _coyoteTimer = 0f;

        public CharacterAirState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString()
        {
            return "AirState";
        }

        public override MovementState ProcessFixedUpdate()
        {
            base.ProcessFixedUpdate();

            if (CheckGround())
                return MovementState.Move;

            AirMove();

            if (!Input.Jump)
            {
                _previousJumpInput = false;
            }
            else if (!_previousJumpInput && !_usedDoubleJump)
            {
                if (_coyoteTimer > 0f)
                {
                    RequestForcedStateEnter();
                    return MovementState.Jump;
                }

                _usedDoubleJump = true;
                _previousJumpInput = true;
                Jump();
            }

            Fall();

            ProcessMovement();

            _coyoteTimer = Mathf.MoveTowards(_coyoteTimer, 0f, Time.fixedDeltaTime);

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _previousJumpInput = true;
            _usedDoubleJump = false;

            if (character.State == MovementState.Jump)
            {
                _coyoteTimer = 0f;
                Jump();
            }
            else
                _coyoteTimer = character.Stats.JumpCoyoteTime;
        }

        public override void ProcessCollision(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.999f)
                return;

            if (hit.normal.y <= -0.95f && character.VerticalVelocity > 0f)
            {
                character.VerticalVelocity = -.1f;
                return;
            }

            Vector3 wallNormal = hit.normal;

            Vector3 currentVelocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);

            Vector3 projectedVelocity = Vector3.ProjectOnPlane(currentVelocity, wallNormal);

            character.HorizontalVelocity = new Vector2(projectedVelocity.x, projectedVelocity.z);
        }

        private bool CheckGround()
        {
            if (character.IsOnGround && character.VerticalVelocity <= 0f)
            {
                character.VerticalVelocity = -.1f;
                return true;
            }
            else
                return false;
        }

        private void AirMove()
        {
            Vector3 moveDir = character.MoveDirection;

            character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                new Vector2(moveDir.x, moveDir.z) * character.Stats.RunSpeed,
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