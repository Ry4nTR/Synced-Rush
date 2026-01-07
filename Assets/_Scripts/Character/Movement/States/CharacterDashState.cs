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

            Vector3 worldDir = character.LookDirection;

            if (!Mathf.Approximately(worldDir.magnitude, 0f))
            {
                worldDir.Normalize();
                character.TotalVelocity = worldDir * character.Stats.DashSpeed;
            }

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