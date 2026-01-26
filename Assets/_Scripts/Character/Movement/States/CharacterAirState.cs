using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterAirState : CharacterMovementState
    {
        private int _wallCandidateStableTicks;
        private RaycastHit _wallCandidateHit;

        private bool _blockJetpackInput = false;
        private bool _jetpackEligible;          // true only if we entered air from a jump/wallrun jump
        private bool _jetpackActive;            // jetpack currently allowed to apply thrust while held
        private int _lastJetpackPressJumpCount; // we use JumpCount as the "re-press" edge

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

            if (TryFindWallCandidate(out RaycastHit wh))
            {
                _wallCandidateHit = wh;
                _wallCandidateStableTicks++;
            }
            else
            {
                _wallCandidateStableTicks = 0;
            }

            bool wallStable = _wallCandidateStableTicks >= 2;

            if (character.HorizontalVelocity.magnitude < character.Stats.WallRunMinSpeed)
                wallStable = false;

            if (wallStable && !Input.Crouch)
            {
                character.WallRunStartInfo = _wallCandidateHit;
                character.HasWallRunStartInfo = true;
                return MovementState.WallRun;
            }

            // ---------------------------
            // JETPACK (deterministic hold + "jump then re-press")
            // ---------------------------
            if (character.Ability.CurrentAbility == CharacterAbility.Jetpack)
            {
                // DASH (unchanged)
                if (ConsumeAbilityPressed() && character.Ability.UseDash())
                    return MovementState.Dash;

                // Not held => reset and stop
                if (!Input.JetHeld)
                {
                    _blockJetpackInput = false;
                    _jetpackActive = false;
                    character.Ability.StopJetpack();
                }
                else
                {
                    // Held but not allowed (didn't jump OR still blocked because you never released)
                    if (!_jetpackEligible || _blockJetpackInput)
                    {
                        // IMPORTANT: do NOT drain charge while blocked
                        character.Ability.StopJetpack();
                    }
                    else
                    {
                        // Require a NEW press in air to activate jetpack.
                        // JumpCount increments on button-down -> use it as edge.
                        if (Input.JumpCount > _lastJetpackPressJumpCount)
                        {
                            _lastJetpackPressJumpCount = Input.JumpCount;
                            _jetpackActive = true;
                        }

                        if (_jetpackActive)
                        {
                            if (character.Ability.UseJetpack())
                            {
                                JetpackFly();
                            }
                            else
                            {
                                _jetpackActive = false;
                                character.Ability.StopJetpack();
                            }
                        }
                        else
                        {
                            // Held but not re-pressed yet -> no jetpack
                            character.Ability.StopJetpack();
                        }
                    }
                }
            }

            bool jetThrustThisTick =
                character.Ability.CurrentAbility == CharacterAbility.Jetpack &&
                _jetpackEligible &&
                _jetpackActive &&
                Input.JetHeld &&
                !_blockJetpackInput &&
                character.Ability.UsingJetpack;

            if (!jetThrustThisTick)
                Fall();

            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            ResetFlags();

            _wallCandidateStableTicks = 0;
            _wallCandidateHit = default;

            // Jetpack rule: only after a real jump/wallrun entry to air
            bool cameFromJump = ParentStateMachine.PreviousStateEnum is MovementState.Jump
                                                             or MovementState.WallRun;

            _jetpackEligible = cameFromJump;
            _jetpackActive = false;

            // Must "re-press" in air to activate jetpack
            _lastJetpackPressJumpCount = Input.JumpCount;

            // If holding space on air entry (jump hold), require release first
            _blockJetpackInput = Input.JetHeld;
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
        }

        private bool CheckGround()
        {
            return character.IsOnGround && character.VerticalVelocity <= 0f;
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
            _blockJetpackInput = false;
        }

        private bool TryFindWallCandidate(out RaycastHit hit)
        {
            float skin = character.Controller.skinWidth;
            float radius = character.Controller.radius;

            // Use your wall snap length if you want consistency with WallRunState.
            // If you don’t have a stat for this yet, use 1.0f like WallRunState.
            float probeDist = 1.0f + skin;

            Vector3 forward = character.Orientation.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = character.Orientation.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            bool hitR = Physics.SphereCast(character.CenterPosition, radius * 0.5f, right, out RaycastHit hr, probeDist, character.LayerMask);
            bool hitL = Physics.SphereCast(character.CenterPosition, radius * 0.5f, -right, out RaycastHit hl, probeDist, character.LayerMask);

            bool Valid(RaycastHit h)
            {
                if (h.collider == null) return false;

                // wall is vertical-ish
                if (h.normal.y > 0.1f || h.normal.y < -0.1f) return false;

                // look angle limit (same idea as WallRunState)
                Vector2 hitN = new Vector2(h.normal.x, h.normal.z);
                Vector2 look = new Vector2(forward.x, forward.z);
                float angle = Vector2.Angle(hitN, -look);
                return angle < character.Stats.WallRunLookAngleLimit;
            }

            bool vr = hitR && Valid(hr);
            bool vl = hitL && Valid(hl);

            hit = default;
            if (!vr && !vl) return false;

            hit = (vr && vl) ? (hr.distance <= hl.distance ? hr : hl) : (vr ? hr : hl);
            return true;
        }
    }
}