using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterAirState : CharacterMovementState
    {
        private bool _usedDoubleJump = false;
        private bool _canWallRun = false;
        private bool _previousJumpInput = true;
        private float _coyoteTimer = 0f;

        public CharacterAirState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "AirState"; }

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

            if (_canWallRun)
                return MovementState.WallRun;

            Fall();

            ProcessMovement();

            _coyoteTimer = Mathf.MoveTowards(_coyoteTimer, 0f, Time.fixedDeltaTime);

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            ResetFlags();

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

            Vector3 currentVelocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);

            Vector3 projectedVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);

            character.HorizontalVelocity = new Vector2(projectedVelocity.x, projectedVelocity.z);

            if (
                hit.normal.y < 0.1f
                && hit.normal.y > -0.1f
                && hit.point.y > character.CenterPosition.y
                )
            {
                character.WallRunStartInfo = hit;
                _canWallRun = true;
            }
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

        private void ResetFlags()
        {
            _usedDoubleJump = false;
            _canWallRun = false;
            _previousJumpInput = true;
        }

    }
}