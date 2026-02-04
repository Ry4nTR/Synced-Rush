using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterWallRunState : CharacterMovementState
    {
        private readonly float _stepDistance = 0.1f;
        private readonly float _wallSnapLength = 1.0f;

        private float _enterBoostTimer = 0.0f;

        private RaycastHit _lastWallHit;
        private bool _hasLastWallHit;

        private int _lastProcessedJumpCount = -1;

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

            if (character.IsOnGround)
                return MovementState.Move;


            int prevJump = _lastProcessedJumpCount;
            bool jumpRequested = Input.JumpCount > prevJump;

            if (Input.JumpCount != prevJump)
                _lastProcessedJumpCount = Input.JumpCount;

            if (jumpRequested)
            {
                WallJump();
                return MovementState.Air;
            }

            bool crouchInput = Input.Crouch;
            if (crouchInput)
            {
                WallDetach();
                return MovementState.Air;
            }

            EnterBoost();

            Slowdown();
            VerticalSlowdown();

            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _lastProcessedJumpCount = Input.JumpCount;

            ResetValues();

            //character.VerticalVelocity = 0f;

            if (character.HasWallRunStartInfo)
            {
                // Use hit normal only as a hint direction (stable), then lock using CheckWall().
                Vector3 startDir = character.WallRunStartInfo.normal;
                startDir.y = 0f;

                if (startDir.sqrMagnitude < 0.0001f)
                {
                    _isWallRunInvalid = true;
                }
                else
                {
                    _wallDir = -startDir.normalized;
                    _expectedWallDir = _wallDir;

                    if (!CheckWall(out RaycastHit rayHit))
                        _isWallRunInvalid = true;
                }

                character.HasWallRunStartInfo = false;
            }
            else
            {
                _isWallRunInvalid = true;
            }

            if (character.HorizontalVelocity.magnitude < character.Stats.WallRunTargetSpeed)
                _enterBoostTimer = character.Stats.WallRunInitialBoostDuration;
        }

        public override void ExitState()
        {
            base.ExitState();

            ParentStateMachine.StartWallRunCooldown();
        }

        protected new void ProcessMovement()
        {
            Vector3 moveDir = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);
            float speed = moveDir.magnitude;
            if (speed < character.Stats.WallRunMinSpeed)
            {
                _isWallRunInvalid = true;
                base.ProcessMovement();
                return;
            }
            moveDir.Normalize();

            float frameMovement = (character.HorizontalVelocity * Time.fixedDeltaTime).magnitude;

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

            if (Mathf.Approximately(_wallDir.magnitude, 0f))
                return false;

            _wallDir.Normalize();

            Vector3 startPosition = character.CenterPosition + _wallDir * (character.Controller.radius / 2f);

            // ---------- First ray (using current wallDir) ----------
            RaycastHit hit;
            bool hasHit = Physics.Raycast(
                startPosition,
                _wallDir,
                out hit,
                rayLength,
                character.LayerMask
            );

            rayHit = hit;

            Vector2 hitN = new(rayHit.normal.x, rayHit.normal.z);
            Vector2 lookDir = new(character.Orientation.transform.forward.x, character.Orientation.transform.forward.z);

            float angle = Vector2.Angle(hitN, -lookDir);

            if (hasHit
                && hit.normal.y < 0.1f
                && hit.normal.y > -0.1f
                && angle < character.Stats.WallRunLookAngleLimit)
            {
                _expectedWallDir = -hit.normal;

                // Keep wall reference consistent
                _wallPosition = hit.point;
                _wallDir = _wallPosition - character.CenterPosition;

                _lastWallHit = hit;
                _hasLastWallHit = true;

                return true;
            }

            // ---------- Second ray (using expected wall dir) ----------
            _expectedWallDir.y = 0f;

            if (Mathf.Approximately(_expectedWallDir.magnitude, 0f))
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

            hitN = new(rayHit.normal.x, rayHit.normal.z);
            lookDir = new(character.Orientation.transform.forward.x, character.Orientation.transform.forward.z);

            angle = Vector2.Angle(hitN, -lookDir);
            // Debug.Log("WRunState " + angle); // keep this OFF while testing determinism

            if (hasHit
                && hit2.normal.y < 0.1f
                && hit2.normal.y > -0.1f
                && angle < character.Stats.WallRunLookAngleLimit)
            {
                _expectedWallDir = -hit2.normal;

                // Update wall reference from the actual hit2
                _wallPosition = hit2.point;
                _wallDir = _wallPosition - character.CenterPosition;

                _lastWallHit = hit2;
                _hasLastWallHit = true;

                return true;
            }

            return false;
        }

        private void WallJump()
        {
            RaycastHit hit;

            if (_hasLastWallHit)
            {
                hit = _lastWallHit;
            }
            else
            {
                CheckWall(out hit);
                if (hit.collider == null)
                    return;
            }

            float jumpSpeed = Mathf.Sqrt(2 * character.Stats.Gravity * character.Stats.WallJumpHeight);

            // Push away from wall + upward
            Vector3 jumpDir = new Vector3(hit.normal.x, 1f, hit.normal.z).normalized;
            character.TotalVelocity += jumpDir * jumpSpeed;
        }

        private void WallDetach()
        {
            RaycastHit hit;

            if (_hasLastWallHit)
            {
                hit = _lastWallHit;
            }
            else
            {
                CheckWall(out hit);
                if (hit.collider == null)
                    return;
            }

            float detachStrength = 2f;

            Vector3 detachDir = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;

            character.TotalVelocity += detachDir * detachStrength;
        }

        private void EnterBoost()
        {
            if (character.Stats.WallRunTargetSpeed < character.HorizontalVelocity.magnitude)
                _enterBoostTimer = 0f;

            _enterBoostTimer = Mathf.MoveTowards(_enterBoostTimer, 0f, Time.fixedDeltaTime);

            if (_enterBoostTimer > 0f)
            {
                character.HorizontalVelocity +=
                        character.Stats.WallRunInitialBoostAcceleration
                        * Time.fixedDeltaTime
                        * character.HorizontalVelocity.normalized;
            }
        }

        private void Slowdown()
        {
            Vector3 move = character.MoveDirection;

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
                    Time.fixedDeltaTime * totalDecel);
            }
        }

        private void VerticalSlowdown()
        {
            character.VerticalVelocity = Mathf.MoveTowards(
                character.VerticalVelocity,
                0f,
                character.Stats.WallRunVerticalDeceleration * Time.fixedDeltaTime
                );
        }

        private void ResetValues()
        {
            _wallPosition = Vector3.zero;
            _wallDir = Vector3.zero;
            _expectedWallDir = Vector3.zero;
            _isWallRunInvalid = false;
            _hasLastWallHit = false;
            _enterBoostTimer = 0f;
        }
    }
}
