using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterGrappleHookState : CharacterMovementState
    {
        private float _groundClearanceLiftRemaining = 0f;

        // tune these two values; start conservative
        private const float GroundClearanceLiftHeight = 0.08f; // 8 cm
        private const float GroundClearanceLiftSpeed = 1.2f;  // m/s

        private bool _canWallRun = false;

        public CharacterGrappleHookState(MovementController movementComponentReference) : base(movementComponentReference) { }

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
                    character.QueueDetachRequest();

                    if (wallHit) return MovementState.WallRun;
                    if (groundHit) return MovementState.Move;
                    return MovementState.Air;
                }
            }

            // Save the updated simulation state
            character.UpdateGrappleState(s);
            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _groundClearanceLiftRemaining = 0f;
            _canWallRun = false;

            // 1. Determine Origin
            Vector3 origin = character.CurrentInput.GrappleOrigin;

            // 2. Initialize the NetState
            GrappleNetState s = new GrappleNetState
            {
                Phase = GrapplePhase.Shooting, // Start shooting immediately
                Origin = origin,
                TipPosition = origin,
                Direction = character.AimDirection.normalized, // Use the current aim
                CurrentDistance = 0f,
                HookPoint = Vector3.zero
            };

            // 3. Apply Anti-Cheat (Server Only)
            // If the client claims an origin too far from reality, clamp it.
            if (character.IsServer)
            {
                float dist = Vector3.Distance(origin, character.CenterPosition);
                if (dist > 2.0f)
                {
                    s.Origin = character.CenterPosition;
                    s.TipPosition = character.CenterPosition;
                }
            }

            // 4. Save to Simulation
            character.UpdateGrappleState(s);
        }

        public override void ExitState()
        {
            base.ExitState();

            // Reset state to None on exit so visuals turn off
            GrappleNetState s = default;
            s.Phase = GrapplePhase.None;
            character.UpdateGrappleState(s);

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
                character.QueueDetachRequest(); // Max range miss
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
                    character.QueueDetachRequest();
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
            return character.IsOnGround && character.VerticalVelocity <= 0f;
        }
    }
}