using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterDashState : CharacterMovementState
    {
        private float _dashTimer = 0f;

        public CharacterDashState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "DashState"; }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            if (_dashTimer <= 0f)
            {
                return MovementState.Air;
            }

            ProcessMovement();

            _dashTimer = Mathf.MoveTowards(_dashTimer, 0f, Time.fixedDeltaTime);

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _dashTimer = character.Stats.DashDuration;

            Vector3 dashDir = GetDashDirection();

            // --- DASH DIR SIGNATURE (one-shot, deterministic) ---
            float inputYaw = Input.AimYaw;
            float inputPitch = Input.AimPitch;

            float bodyYaw = character.Orientation != null
                ? character.Orientation.transform.eulerAngles.y
                : float.NaN;

            float deltaYaw = float.IsNaN(bodyYaw) ? float.NaN : Mathf.DeltaAngle(inputYaw, bodyYaw);

            Vector3 look = (Quaternion.Euler(inputPitch, inputYaw, 0f) * Vector3.forward).normalized;
            Vector3 lookFlat = Vector3.ProjectOnPlane(look, Vector3.up).normalized;

            Vector3 bodyFwd = character.Orientation != null
                ? Vector3.ProjectOnPlane(character.Orientation.transform.forward, Vector3.up).normalized
                : Vector3.zero;

            float dotLookVsBody = (lookFlat == Vector3.zero || bodyFwd == Vector3.zero) ? 0f : Vector3.Dot(lookFlat, bodyFwd);

            Vector3 velocityDir = character.TotalVelocity.normalized;
            float speed = character.TotalVelocity.magnitude;

            if (dashDir != Vector3.zero)
            {
                if (Vector3.Dot(dashDir, velocityDir) >= 0
                    && speed > character.Stats.DashSpeed)
                    character.TotalVelocity = dashDir * speed;
                else
                    character.TotalVelocity = dashDir * character.Stats.DashSpeed;
            }
            else
                Debug.LogError("DashDir è 0 mentre non dovrebbe esserelo");

        }

        public override void ExitState()
        {
            base.ExitState();
        }

        private Vector3 GetDashDirection()
        {
            Vector2 move = Input.Move;

            float ax = Mathf.Abs(move.x);
            float ay = Mathf.Abs(move.y);

            float yaw = Input.AimYaw;
            float pitch = Input.AimPitch;

            Vector3 look = (Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward).normalized;
            Vector3 right = (Quaternion.Euler(0f, yaw, 0f) * Vector3.right).normalized;

            const float dead = 0.2f;

            if (ax < dead && ay < dead)
                return look;

            if (ax > ay)
                return (move.x >= 0f ? right : -right);

            return (move.y >= 0f ? look : -look);
        }
    }
}