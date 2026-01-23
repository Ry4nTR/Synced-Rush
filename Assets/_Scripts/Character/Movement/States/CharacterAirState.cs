using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterAirState : CharacterMovementState
    {
        private bool _canWallRun = false;
        private bool _blockJetpackInput = false;
        private int _lastProcessedAbilityCount = -1;

        public CharacterAirState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "AirState"; }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            // Optional server-side debug
            if (Input.Ability && character.IsServer)
            {
                Debug.Log($"[SERVER DASH CHECK] Ability={character.Ability.CurrentAbility} (Expected Jetpack) | Charge={character.Ability.DashCharge} | Input={Input.Ability}");
            }

            if (CheckGround())
                return MovementState.Move;

            AirMove();

            bool crouchInput = Input.Crouch;

            if (_canWallRun && !crouchInput)
                return MovementState.WallRun;

            if (character.Ability.CurrentAbility == CharacterAbility.Jetpack)
            {
                // ---------------------------
                // DASH (counter-based edge)
                // ---------------------------
                // We treat AbilityCount as the authoritative "pressed" indicator.
                bool dashRequested = Input.AbilityCount > _lastProcessedAbilityCount;

                if (Input.AbilityCount != _lastProcessedAbilityCount)
                {
                    _lastProcessedAbilityCount = Input.AbilityCount;

                    if (dashRequested && character.Ability.UseDash())
                    {
                        #if UNITY_EDITOR
                        Debug.Log($"[DASH REQUEST] from={ToString()} isServer={character.IsServer} seq={Input.Sequence} abilityCount={Input.AbilityCount} dashCharge={character.Ability.DashCharge:F2}");
                        #endif
                        return MovementState.Dash;
                    }
                }

                // ---------------------------
                // JETPACK (unchanged logic)
                // ---------------------------
                bool jetpackInput = Input.Jetpack;
                if (jetpackInput)
                {
                    if (character.Ability.UseJetpack())
                    {
                        if (!_blockJetpackInput)
                            JetpackFly();
                    }
                    else
                        _blockJetpackInput = true;
                }
                else
                    _blockJetpackInput = false;
            }

            Fall();

            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            ResetFlags();

            bool hasJumped = ParentStateMachine.PreviousStateEnum is MovementState.Jump
                                                           or MovementState.WallRun;

            bool jetpackInput = Input.Jetpack;
            if (jetpackInput && hasJumped)
            {
                _blockJetpackInput = true;
            }
        }

        public override void ProcessCollision(ControllerColliderHit hit)
        {
            Vector3 currentVelocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);

            Vector3 projectedVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);

            character.HorizontalVelocity = new Vector2(projectedVelocity.x, projectedVelocity.z);

            if (hit.normal.y > 0.999f)
                return;

            if (hit.normal.y <= -0.95f && character.VerticalVelocity > 0f)
            {
                character.VerticalVelocity = -.1f;
                return;
            }

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
            if (character.HorizontalVelocity.magnitude < character.Stats.WallRunMinSpeed)
                return false;

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
                Debug.Log("AirState " + angle); //TODO da rimuovere

                Vector3 inputDir = character.MoveDirection;

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
            Vector3 moveDir = character.MoveDirection;

            Vector2 moveDirXY = new(moveDir.x, moveDir.z);

            bool isOverSpeed = character.HorizontalVelocity.magnitude > character.Stats.AirTargetSpeed;

            float targetDeceleration = isOverSpeed
                ? character.Stats.AirOverspeedDeceleration
                : character.Stats.AirDeceleration;

            character.HorizontalVelocity += character.Stats.AirAcceleration * Time.fixedDeltaTime * moveDirXY;

            character.HorizontalVelocity = Vector2.MoveTowards(
                character.HorizontalVelocity,
                Vector2.zero,
                Time.fixedDeltaTime * targetDeceleration);
        }

        private void Fall()
        {
            character.VerticalVelocity -= (character.Stats.Gravity * Time.fixedDeltaTime);
        }

        private void JetpackFly()
        {
            if (character.VerticalVelocity > character.Stats.Gravity)
                character.VerticalVelocity += (character.Stats.Gravity * Time.fixedDeltaTime);
            else
                character.VerticalVelocity += (character.Stats.JetpackAcceleration * Time.fixedDeltaTime);
        }

        private void ResetFlags()
        {
            _canWallRun = false;
            _blockJetpackInput = false;
        }

    }
}