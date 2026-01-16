using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterWallRunState : CharacterMovementState
    {
        private readonly float _stepDistance = 0.1f;
        private readonly float _wallSnapLength = 1.0f;

        private float _enterBoostTimer = 0.0f;

        /// <summary>
        /// Posizione del muro in world space
        /// </summary>
        private Vector3 _wallPosition = Vector3.zero;
        private Vector3 _wallDir = Vector3.zero;
        private Vector3 _expectedWallDir = Vector3.zero;

        private bool _isWallRunInvalid = false;

        public CharacterWallRunState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "WallRunState"; }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            if (_isWallRunInvalid)
                return MovementState.Air;

            if (CheckGround())
                return MovementState.Move;

            // Determine jump input based on context.  On the server use the authoritative
            // networked input; on the client use the local PlayerInputHandler when
            // available.  If the player presses jump while wall running, perform a
            // wall jump and return to the Air state.
            bool jumpInput;
            if (character.IsServer || character.LocalInputHandler == null)
            {
                jumpInput = Input.Jump;
            }
            else
            {
                jumpInput = character.LocalInputHandler.Jump;
            }

            if (jumpInput)
            {
                WallJump();
                return MovementState.Air;
            }

            if (character.CurrentAbility == CharacterAbility.Jetpack)
            {
                bool dashInput = (character.IsServer || character.LocalInputHandler == null)
                    ? Input.Ability
                    : character.LocalInputHandler.Ability;
                if (dashInput)
                    return MovementState.Dash;
            }

            bool crouchInput = (character.IsServer || character.LocalInputHandler == null)
                ? Input.Crouch
                : character.LocalInputHandler.Crouch;
            if (crouchInput)
                return MovementState.Air;

            EnterBoost();

            Slowdown();

            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            ResetValues();

            character.VerticalVelocity = 0f;

            if (character.WallRunStartInfo != null)
            {
                _wallPosition = character.WallRunStartInfo.point;
                _wallDir = _wallPosition - character.CenterPosition;
                _expectedWallDir = _wallDir;
                character.WallRunStartInfo = null;
            }
            else
                _isWallRunInvalid = true;

            _enterBoostTimer = character.Stats.WallRunInitialBoostDuration;
        }

        protected new void ProcessMovement()
        {
            Vector3 moveDir = new(character.HorizontalVelocity.x, 0, character.HorizontalVelocity.y);
            float speed = moveDir.magnitude;
            if (speed < character.Stats.WallRunMinSpeed)
            {
                _isWallRunInvalid = true;
                base.ProcessMovement();
                return;
            }
            moveDir.Normalize();

            float frameMovement = (character.HorizontalVelocity * Time.deltaTime).magnitude;

            int totalSteps = (int)(frameMovement / _stepDistance);
            float remaingDistance = frameMovement % _stepDistance;

            int stepCounter = 0;
            bool interrupt = false;
            while (stepCounter < totalSteps)
            {
                bool hasMoved = MoveCharacter(ref moveDir, _stepDistance);
                if (!hasMoved)
                {
                    interrupt = true;
                    break;
                }

                ++stepCounter;
            }

            if (remaingDistance > 0f && !interrupt)
                _isWallRunInvalid = !MoveCharacter(ref moveDir, remaingDistance);

            if (interrupt)
            {
                float totalDistance = (_stepDistance * (totalSteps - stepCounter)) + remaingDistance;
                character.Controller.Move(moveDir * totalDistance);
                _isWallRunInvalid = true;
            }

            moveDir *= speed;

            if (!Mathf.Approximately(character.HorizontalVelocity.magnitude, 0f))
                character.HorizontalVelocity = new(moveDir.x, moveDir.z);
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

        private bool MoveCharacter(ref Vector3 moveDirection, float distance)
        {
            if (CheckWall(out RaycastHit hit))
            {
                moveDirection = Vector3.ProjectOnPlane(moveDirection, hit.normal);
                moveDirection.Normalize();

                character.Controller.Move(moveDirection * distance);

                return true;
            }
            return false;
        }

        private bool CheckWall(out RaycastHit rayHit)
        {
            rayHit = new RaycastHit();

            float skinWidth = character.Controller.skinWidth;
            float rayLength = _wallSnapLength + skinWidth;

            _wallDir.y = 0f;

            if (Mathf.Approximately(_wallDir.magnitude, 0))
                return false;

            _wallDir.Normalize();

            Vector3 startPosition = character.CenterPosition + _wallDir * (character.Controller.radius / 2f);

            RaycastHit hit;
            bool hasHit = Physics.Raycast(
                startPosition,
                _wallDir,
                out hit,
                rayLength,
                character.LayerMask
            );

            rayHit = hit;

            //TODO da rimuovere quando non serve più
            Color rayColor = hasHit ? Color.green : Color.red;
            Debug.DrawRay(startPosition, _wallDir * rayLength, rayColor, Time.deltaTime);

            Vector2 hitN = new(rayHit.normal.x, rayHit.normal.z);
            Vector2 lookDir = new(character.Orientation.transform.forward.x, character.Orientation.transform.forward.z);

            float angle = Vector2.Angle(hitN, -lookDir);
            Debug.Log("WRunState " + angle); //TODO

            if (hasHit
                && hit.normal.y < 0.1f
                && hit.normal.y > -0.1f
                && angle < character.Stats.WallRunLookAngleLimit)
            {
                _expectedWallDir = -hit.normal;
                return true;
            }

            _expectedWallDir.y = 0f;

            if (Mathf.Approximately(_expectedWallDir.magnitude, 0))
                return false;

            _expectedWallDir.Normalize();

            startPosition = character.CenterPosition + _expectedWallDir * (character.Controller.radius / 2f);

            RaycastHit hit2;
            hasHit = Physics.Raycast(
                startPosition,
                _expectedWallDir,
                out hit2,
                rayLength,
                character.LayerMask
            );

            rayHit = hit2;

            //TODO da rimuovere quando non serve più
            rayColor = hasHit ? Color.green : Color.red;
            Debug.DrawRay(startPosition, _expectedWallDir * rayLength, rayColor, Time.deltaTime);

            hitN = new(rayHit.normal.x, rayHit.normal.z);
            lookDir = new(character.Orientation.transform.forward.x, character.Orientation.transform.forward.z);

            angle = Vector2.Angle(hitN, -lookDir);
            Debug.Log("WRunState " + angle); //TODO

            if (hasHit
                && hit2.normal.y < 0.1f
                && hit2.normal.y > -0.1f
                && angle < character.Stats.WallRunLookAngleLimit)
            {
                _wallPosition = hit2.point;
                _wallDir = _wallPosition - character.CenterPosition;
                return true;
            }

            return false;
        }

        private void WallJump()
        {
            CheckWall(out RaycastHit hit);

            float jumpSpeed = Mathf.Sqrt(2 * character.Stats.Gravity * character.Stats.JumpHeight);
            Vector3 jumpDir = new(hit.normal.x, 1f, hit.normal.z);

            jumpDir = jumpDir.normalized * jumpSpeed;

            character.TotalVelocity += jumpDir;
        }

        private void EnterBoost()
        {
            _enterBoostTimer = Mathf.MoveTowards(_enterBoostTimer, 0f, Time.deltaTime);

            if (_enterBoostTimer > 0f)
            {
                character.HorizontalVelocity +=
                        character.Stats.WallRunInitialBoostAcceleration
                        * Time.deltaTime
                        * character.HorizontalVelocity.normalized;
            }


        }

        private void Slowdown()
        {
            Vector3 move = (character.IsServer || character.LocalInputHandler == null)
                ? character.MoveDirection
                : character.LocalMoveDirection;

            Vector2 moveXY = new(move.x, move.z);
            Vector2 velocityDir = character.HorizontalVelocity.normalized;

            float totalDecel = 0f;

            if (moveXY != Vector2.zero)
            {
                float dot = Vector2.Dot(moveXY, velocityDir);
                if (dot < 0)
                    totalDecel += -dot * character.Stats.WallRunBrake;
            }

            totalDecel += character.Stats.WallRunDeceleration;

            {
                character.HorizontalVelocity = Vector2.MoveTowards(
                    character.HorizontalVelocity,
                    Vector2.zero,
                    Time.deltaTime * totalDecel);
            }
        }

        private void ResetValues()
        {
            _wallPosition = Vector3.zero;
            _wallDir = Vector3.zero;
            _expectedWallDir = Vector3.zero;
            _isWallRunInvalid = false;
        }

    }
}
