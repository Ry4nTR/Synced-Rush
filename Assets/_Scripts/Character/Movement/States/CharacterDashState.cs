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
     
            /*
            if (CheckGround())
            {
                return MovementState.Move;
            }
            */
            

            if (_dashTimer <= 0f)
            {
#if UNITY_EDITOR
                Debug.Log($"[DASH END] reason=Timer netId={character.NetworkObjectId} ownerId={character.OwnerClientId} isServer={character.IsServer} isOwner={character.IsOwner} seq={Input.Sequence}");
#endif
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
            // OLD BEHAVIOR:
            // - Server uses the server-simulated input direction
            // - Owner client uses the local input direction (what the player actually pressed)
            Vector2 inputDir = (character.IsServer || character.LocalInputHandler == null)
                ? character.MoveInputDirection
                : character.LocalMoveInputDirection;

            inputDir.Normalize();

            Vector3 dir = Vector3.zero;

            // Left/Right (relative to orientation)
            if (!Mathf.Approximately(inputDir.x, 0f))
            {
                dir += character.Orientation.transform.right * inputDir.x;
            }

            // Forward/Back
            // If there's no forward/back input, use LookDirection to preserve "dash where you're looking"
            if (!Mathf.Approximately(inputDir.y, 0f))
            {
                dir += character.LookDirection.normalized * inputDir.y;
            }
            else
            {
                dir += character.LookDirection.normalized;
            }

            return dir.sqrMagnitude > 0.0001f ? dir.normalized : character.LookDirection.normalized;
        }
    }
}