using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterGrappleHookState : CharacterMovementState
    {
        private readonly NetworkPlayerInput _netInput;

        private float _groundClearanceLiftRemaining = 0f;

        // tune these two values; start conservative
        private const float GroundClearanceLiftHeight = 1f; // metri
        private const float GroundClearanceLiftSpeed = 1.2f;  // m/s

        private bool _canWallRun = false;

        public CharacterGrappleHookState(MovementController movementComponentReference) : base(movementComponentReference)
        {
            _netInput = movementComponentReference.GetComponent<NetworkPlayerInput>();
        }

        public override string ToString() => "GrappleHookState";

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            var s = character.GrappleForSim;

            // 1. FORCE EXIT via Input
            if (Input.RequestDetach)
            {
                return MovementState.Air;
            }

            // 2. Process Physics (Shooting / Hooked)
            if (s.Phase == GrapplePhase.Shooting)
            {
                SimulateShooting(ref s);
                AirMove(); // Allow air control while shooting
            }
            else if (s.Phase == GrapplePhase.Hooked)
            {
                HookPull(in s);

                // Physics-based Detach Checks
                bool groundHit = CheckGround();
                bool wallHit = _canWallRun; // logic from ProcessCollision or separate check
                bool minDistance = Vector3.Distance(s.HookPoint, character.CenterPosition) < character.Stats.HookMinDistance;

                if (groundHit || wallHit || minDistance)
                {
                    // Important: Tell the Input system to notify server next tick
                    _netInput.QueueDetachRequest();

                    if (wallHit) return MovementState.WallRun;
                    if (groundHit) return MovementState.Move;
                    return MovementState.Air;
                }
            }

            // Save the updated simulation state
            _netInput.UpdateGrappleState(s);
            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _groundClearanceLiftRemaining = 0f;
            _canWallRun = false;

            Vector3 origin = character.CurrentInput.GrappleOrigin;

            // This is now authoritative from the input tick (owner computed it)
            Vector3 aimPoint = character.CurrentInput.GrappleAimPoint;

            // Aim from origin to aimPoint
            Vector3 dir = (aimPoint - origin).normalized;

            GrappleNetState s = new GrappleNetState
            {
                Phase = GrapplePhase.Shooting,
                Origin = origin,
                TipPosition = origin,
                Direction = dir,
                CurrentDistance = 0f,
                HookPoint = Vector3.zero
            };

            // Apply Anti-Cheat (Server Only)
            if (character.IsServer)
            {
                // Clamp origin
                float distOrigin = Vector3.Distance(origin, character.CenterPosition);
                if (distOrigin > 2.0f)
                {
                    origin = character.CenterPosition;
                    s.Origin = origin;
                    s.TipPosition = origin;
                }

                // Clamp aim point to max distance from origin (prevents fake far target)
                Vector3 toAim = character.CurrentInput.GrappleAimPoint - origin;
                float max = character.Stats.HookMaxDistance;
                if (toAim.sqrMagnitude > max * max)
                {
                    aimPoint = origin + toAim.normalized * max;
                    s.Direction = (aimPoint - origin).normalized;
                }
            }

            // Save to Simulation
            _netInput.UpdateGrappleState(s);
        }

        public override void ExitState()
        {
            base.ExitState();

            // Reset state to None on exit so visuals turn off
            GrappleNetState s = default;
            s.Phase = GrapplePhase.None;
            _netInput.UpdateGrappleState(s);

            // Safety: Dampen upward velocity slightly on detach to prevent "Moon Jumps"
            if (character.VerticalVelocity > 0f)
            {
                character.VerticalVelocity *= 0.8f;
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
        }

        protected override void ProcessMovement()
        {
            Vector3 v = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);
            v *= Time.fixedDeltaTime;

            // Apply lift only when Hooked (not while Shooting) and only on ground
            GrappleNetState s = character.GrappleForSim;
            if (s.Phase == GrapplePhase.Hooked &&
                character.IsOnGround &&
                _groundClearanceLiftRemaining > 0f)
            {
                float liftStep = GroundClearanceLiftSpeed * Time.fixedDeltaTime;
                liftStep = Mathf.Min(liftStep, _groundClearanceLiftRemaining);

                v.y += liftStep;
                _groundClearanceLiftRemaining -= liftStep;
            }

            character.Controller.Move(v);
        }

        private void SimulateShooting(ref GrappleNetState s)
        {
            float step = character.Stats.HookSpeed * Time.fixedDeltaTime;
            float remaining = character.Stats.HookMaxDistance - s.CurrentDistance;

            if (remaining <= 0f)
            {
                _netInput.QueueDetachRequest();
                return; // Will exit next frame via input check or state change
            }

            if (step > remaining) step = remaining;

            // Raycast for hit
            if (Physics.Raycast(s.TipPosition, s.Direction, out RaycastHit hit, step, character.LayerMask))
            {
                s.TipPosition = hit.point;
                s.HookPoint = hit.point;
                s.CurrentDistance += hit.distance;
                s.Phase = GrapplePhase.Hooked;

                if (character.IsOnGround && character.VerticalVelocity <= 0f)
                {
                    _groundClearanceLiftRemaining = GroundClearanceLiftHeight;
                }
            }
            else
            {
                s.TipPosition += s.Direction * step;
                s.CurrentDistance += step;
                if (s.CurrentDistance >= character.Stats.HookMaxDistance)
                {
                    // Missed, trigger exit
                    _netInput.QueueDetachRequest();
                }
            }
        }

        private void HookPull(in GrappleNetState s)
        {
            Vector3 dir = (s.HookPoint - character.CenterPosition).normalized;
            character.TotalVelocity += character.Stats.HookPull * Time.fixedDeltaTime * dir;
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

        private bool CheckGround()
        {
            return _groundClearanceLiftRemaining <= 0f && character.IsOnGround && character.VerticalVelocity <= 0f;
        }
    }
}