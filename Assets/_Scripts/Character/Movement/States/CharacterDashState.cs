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
            Vector2 inputDir = character.MoveInputDirection;
            inputDir.Normalize();

            float yaw = Input.AimYaw;
            float pitch = Input.AimPitch;

            Vector3 look = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

            Vector3 dir = Vector3.zero;

            if (!Mathf.Approximately(inputDir.x, 0f))
                dir += character.Orientation.transform.right * inputDir.x;

            if (!Mathf.Approximately(inputDir.y, 0f))
                dir += look.normalized * inputDir.y;
            else
                dir += look.normalized;

            return dir.sqrMagnitude > 0.0001f ? dir.normalized : look.normalized;
        }
    }
}