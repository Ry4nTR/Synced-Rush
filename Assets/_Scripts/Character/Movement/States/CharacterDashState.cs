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

            if (CheckGround())
                return MovementState.Move;

            if (_dashTimer == 0f)
                return MovementState.Air;

            ProcessMovement();

            _dashTimer = Mathf.MoveTowards(_dashTimer, 0f, Time.deltaTime);

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _dashTimer = character.Stats.DashDuration;

            Vector3 dashDir = Vector3.zero;

            Vector3 lookDir = character.LookDirection.normalized;
            Vector3 rightDir = character.Orientation.transform.right;

            Vector2 inputDir = (character.IsServer || character.LocalInputHandler == null)
                ? character.MoveInputDirection
                : character.LocalMoveInputDirection;

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

            if (dashDir != Vector3.zero)
            {
                character.TotalVelocity = dashDir * character.Stats.DashSpeed;
            }
            else
                Debug.LogError("DashDir è 0 mentre non dovrebbe esserelo");

        }

        public override void ExitState()
        {
            base.ExitState();
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