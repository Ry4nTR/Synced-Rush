using UnityEngine;
using UnityEngine.TextCore.Text;

namespace SyncedRush.Character.Movement
{
    public class CharacterGrappleHookState : CharacterMovementState
    {
        private readonly NetworkPlayerInput _netInput;

        private float _groundClearanceLiftRemaining = 0f;

        // tune these two values; start conservative
        private const float GroundClearanceLiftHeight = 2f; // metri
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
            
            if (s.Phase == GrapplePhase.Hooked)
            {
                HookPull(in s);
                AirMove();

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
            else
            {
                return MovementState.Air;
            }

            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _groundClearanceLiftRemaining = character.IsOnGround && character.VerticalVelocity <= 0f
                ? _groundClearanceLiftRemaining = GroundClearanceLiftHeight
                : 0f;

            _canWallRun = false;
        }

        public override void ExitState()
        {
            base.ExitState();

            character.Ability.DeactivateGrappleHook();

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