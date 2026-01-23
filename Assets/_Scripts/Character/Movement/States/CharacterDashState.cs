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

#if UNITY_EDITOR
            Debug.Log($"[DASH TICK] isServer={character.IsServer} timer={_dashTimer:F3} seq={Input.Sequence}");
#endif

            if (CheckGround())
                return MovementState.Move;

            if (_dashTimer == 0f)
                return MovementState.Air;

            ProcessMovement();

            _dashTimer = Mathf.MoveTowards(_dashTimer, 0f, Time.fixedDeltaTime);

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _dashTimer = character.Stats.DashDuration;

#if UNITY_EDITOR
            Debug.Log(
                $"[DASH ENTER] t={Time.time:F2} fixed={Time.fixedTime:F2} " +
                $"isServer={character.IsServer} isOwner={character.IsOwner} " +
                $"seq={Input.Sequence} ability={Input.Ability}" +
                $"vel={character.TotalVelocity.magnitude:F2}"
            );
#endif

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
            Vector3 dashDir = Vector3.zero;

            Vector3 lookDir = character.Orientation.transform.forward;
            lookDir.y = 0f;
            lookDir.Normalize();
            Vector3 rightDir = character.Orientation.transform.right;
            rightDir.y = 0f;
            rightDir.Normalize();

            Vector2 inputDir = Input.Move;

            if (!Mathf.Approximately(inputDir.x, 0f))
            {
                Vector3 lateralMotion = inputDir.x < 0f
                    ? -rightDir
                    : rightDir;

                dashDir += lateralMotion;
            }

            if (!Mathf.Approximately(inputDir.y, 0f))
            {
                Vector3 longitudinalMotion = inputDir.y < 0f
                    ? -lookDir
                    : lookDir;

                dashDir += longitudinalMotion;
            }

            dashDir = dashDir == Vector3.zero
                ? lookDir
                : dashDir.normalized;

            return dashDir;
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

    }
}