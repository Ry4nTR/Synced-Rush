using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterAirState : CharacterMovementState
    {
        private bool _canWallRun = false;
        private bool _previousJumpInput = true;
        private float _coyoteTimer = 0f;

        public CharacterAirState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "AirState"; }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            if (CheckGround())
                return MovementState.Move;

            AirMove();

            // Determine the jump button state.  On the server use the networked value; on the
            // client read from the PlayerInputHandler if available.
            bool jumpInput;
            if (character.IsServer || character.LocalInputHandler == null)
            {
                jumpInput = Input.Jump;
            }
            else
            {
                jumpInput = character.LocalInputHandler.Jump;
            }

            if (!jumpInput)
            {
                _previousJumpInput = false;
            }
            else if (!_previousJumpInput)
            {
                if (_coyoteTimer > 0f)
                {
                    RequestForcedStateEnter();
                    return MovementState.Jump;
                }
            }

            if (_canWallRun)
                return MovementState.WallRun;

            bool dashInput = (character.IsServer || character.LocalInputHandler == null)
                ? Input.Ability
                : character.LocalInputHandler.Ability;
            if (dashInput)
                return MovementState.Dash;

            Fall();

            ProcessMovement();

            _coyoteTimer = Mathf.MoveTowards(_coyoteTimer, 0f, Time.deltaTime);

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

            if (CheckWallRunCondition(hit))
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

        private bool CheckWallRunCondition(ControllerColliderHit hit)
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
                Debug.DrawRay(startPosition, _wallDir * rayLength, rayColor, Time.deltaTime);

                Vector2 hitN = new(rayHit.normal.x, rayHit.normal.z);
                Vector2 lookDir = new(character.Orientation.transform.forward.x, character.Orientation.transform.forward.z);

                float angle = Vector2.Angle(hitN, -lookDir);
                Debug.Log("AirState " + angle); //TODO da rimuovere

                Vector3 inputDir = (character.IsServer || character.LocalInputHandler == null)
                    ? character.MoveDirection
                    : character.LocalMoveDirection;

                bool moveInputToWall = Vector3.Dot(inputDir, rayHit.normal) < 0;

                if (hasHit
                    && rayHit.normal.y < 0.1f
                    && rayHit.normal.y > -0.1f
                    && angle < character.Stats.WallRunLookAngleLimit
                    && moveInputToWall)
                {
                    return true;
                }
            }
            return false;
        }

        private void AirMove()
        {
            if (character.IsServer || character.LocalInputHandler == null)
            {
                // On the server use the networked input to compute the move direction.
                Vector3 moveDir = character.MoveDirection;
                character.HorizontalVelocity = Vector2.MoveTowards(
                    character.HorizontalVelocity,
                    new Vector2(moveDir.x, moveDir.z) * character.Stats.RunSpeed,
                    Time.deltaTime * character.Stats.RunSpeed * 1);
            }
            else
            {
                // On the client compute the move direction from the local input for prediction.
                Vector2 move = character.LocalInputHandler.Move;
                Vector3 moveDir = character.Orientation.transform.forward * move.y + character.Orientation.transform.right * move.x;
                if (moveDir.magnitude > 1f)
                    moveDir.Normalize();
                character.HorizontalVelocity = Vector2.MoveTowards(
                    character.HorizontalVelocity,
                    new Vector2(moveDir.x, moveDir.z) * character.Stats.RunSpeed,
                    Time.deltaTime * character.Stats.RunSpeed * 1);
            }
        }

        private void Fall()
        {
            character.VerticalVelocity -= (character.Stats.Gravity * Time.deltaTime);
        }

        private void Jump()
        {
            float jumpSpeed = Mathf.Sqrt(2 * character.Stats.Gravity * character.Stats.JumpHeight);
            character.VerticalVelocity = jumpSpeed;
        }

        private void ResetFlags()
        {
            _canWallRun = false;
            _previousJumpInput = true;
        }

    }
}