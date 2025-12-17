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
            {
                if (CheckSlideConditions())
                    return MovementState.Slide;
                else
                    return MovementState.Move;
            }

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

            if (CheckWallRunConditions(hit))
            {
                character.WallRunStartInfo = hit;
                _canWallRun = true;
            }
        }

        private bool CheckGround()
        {
            if (character.IsOnGround && character.VerticalVelocity <= 0f)
            {
                //character.VerticalVelocity = -.1f;
                return true;
            }
            else
                return false;
        }

        private bool CheckWallRunConditions(ControllerColliderHit hit)
        {
            if (
                hit.normal.y < 0.1f
                && hit.normal.y > -0.1f
                )
            {

                float skinWidth = character.Controller.skinWidth;
                float rayLength = 1f + skinWidth;

                Vector3 _wallDir = hit.point - character.CenterPosition;

                _wallDir.y = 0f;

                if (Mathf.Approximately(_wallDir.magnitude, 0))
                    return false;

                _wallDir.Normalize();

                Vector3 startPosition = character.CenterPosition + _wallDir * (character.Controller.radius / 2f);

                RaycastHit rayHit;
                bool hasHit = Physics.Raycast(
                    startPosition,
                    _wallDir,
                    out rayHit,
                    rayLength,
                    character.LayerMask
                );

                //TODO da rimuovere quando non serve più
                Color rayColor = hasHit ? Color.green : Color.red;
                Debug.DrawRay(startPosition, _wallDir * rayLength, rayColor, Time.fixedDeltaTime);

                Vector2 hitN = new(rayHit.normal.x, rayHit.normal.z);
                Vector2 lookDir = new(character.Orientation.transform.forward.x, character.Orientation.transform.forward.z);

                float angle = Vector2.Angle(hitN, -lookDir);
                Debug.Log("AirState " + angle); //TODO

                if (hasHit
                    && rayHit.normal.y < 0.1f
                    && rayHit.normal.y > -0.1f
                    && angle < character.Stats.WallRunLookAngleLimit)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckSlideConditions()
        {
            Vector3 inputDir = character.MoveDirection;
            float dot = Vector3.Dot(inputDir, character.Orientation.transform.forward);

            return Input.Crouch && Input.Sprint && dot > -0.1f;
        }

        private void AirMove()
        {
            Vector3 moveDir = character.MoveDirection;

            if (character.HorizontalVelocity.magnitude > character.Stats.AirAcceleration)
            {
                character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                    new Vector2(moveDir.x, moveDir.z) * character.Stats.AirAcceleration,
                    Time.fixedDeltaTime * -character.Stats.AirAcceleration * 1);
            }

            character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                new Vector2(moveDir.x, moveDir.z) * character.Stats.AirAcceleration,
                Time.fixedDeltaTime * character.Stats.AirAcceleration * 1);

            //if (Input.Move.magnitude > 0f)
            //    character.HorizontalVelocity += character.Stats.AirAcceleration * Time.fixedDeltaTime * new Vector2(moveDir.x, moveDir.z);

            //character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity, Vector2.zero, character.Stats.AirDeceleration * Time.fixedDeltaTime);
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