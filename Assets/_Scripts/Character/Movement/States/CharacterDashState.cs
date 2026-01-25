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
            if (_dashTimer > 0f)
            {
                var pos = character.transform.position;
                var vel = character.TotalVelocity;
                Debug.Log(
                    $"[DASH TICK] netId={character.NetworkObjectId} ownerId={character.OwnerClientId} " +
                    $"isServer={character.IsServer} isOwner={character.IsOwner} seq={Input.Sequence} " +
                    $"timer={_dashTimer:F3} onGround={character.IsOnGround} vVel={character.VerticalVelocity:F2} " +
                    $"pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) " +
                    $"vel=({vel.x:F2},{vel.y:F2},{vel.z:F2}) speed={vel.magnitude:F2}"
                );
            }
#endif


            
            if (CheckGround())
            {
#if UNITY_EDITOR
                Debug.Log($"[DASH END] reason=Ground netId={character.NetworkObjectId} ownerId={character.OwnerClientId} isServer={character.IsServer} isOwner={character.IsOwner} seq={Input.Sequence}");
#endif
                return MovementState.Move;
            }
            

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

#if UNITY_EDITOR
            {
                Vector3 dashDir_dbg = GetDashDirection();

                Vector3 preVel_dbg = character.TotalVelocity;
                float preSpeed_dbg = preVel_dbg.magnitude;
                Vector3 preVelDir_dbg = preVel_dbg.sqrMagnitude > 0.0001f ? preVel_dbg.normalized : Vector3.zero;

                Vector3 aimFwd_dbg = character.AimDirection.sqrMagnitude > 0.0001f
                    ? character.AimDirection.normalized
                    : Vector3.forward;

                Transform camT = null;

                // pick the real camera used by this player
                if (character.IsOwner)
                {
                    if (Camera.main != null) camT = Camera.main.transform;
                }

                // If you have a dedicated camera pivot reference on the character, use that instead.
                // camT = character.CameraPivot; // example

                Vector3 camFwd = camT != null ? camT.forward : Vector3.zero;

                Debug.Log(
                    $"[DASH AIM COMPARE] netId={character.NetworkObjectId} ownerId={character.OwnerClientId} " +
                    $"isServer={character.IsServer} isOwner={character.IsOwner} seq={Input.Sequence} " +
                    $"aimFwd=({aimFwd_dbg.x:F2},{aimFwd_dbg.y:F2},{aimFwd_dbg.z:F2}) " +
                    $"camFwd=({camFwd.x:F2},{camFwd.y:F2},{camFwd.z:F2}) " +
                    $"dot={Vector3.Dot(aimFwd_dbg, camFwd):F3}"
                );

                Vector3 pos = character.transform.position;

                // Try to print a state string without risking compile errors.
                // If character has a "State" property, use it; otherwise just print this state's name.
                string stateStr = ToString();
                // If you DO have a state property and it compiles, replace the above line with:
                // string stateStr = character.State.ToString();

                Debug.Log(
                    $"[DASH SIG] netId={character.NetworkObjectId} ownerId={character.OwnerClientId} " +
                    $"isServer={character.IsServer} isOwner={character.IsOwner} seq={Input.Sequence} " +
                    $"state={stateStr} onGround={character.IsOnGround} vVel={character.VerticalVelocity:F2} " +
                    $"pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) " +
                    $"move={Input.Move} yaw={Input.AimYaw:F1} pitch={Input.AimPitch:F1} " +
                    $"aimFwd=({aimFwd_dbg.x:F2},{aimFwd_dbg.y:F2},{aimFwd_dbg.z:F2}) " +
                    $"preVel=({preVel_dbg.x:F2},{preVel_dbg.y:F2},{preVel_dbg.z:F2}) speed={preSpeed_dbg:F2} " +
                    $"velDir=({preVelDir_dbg.x:F2},{preVelDir_dbg.y:F2},{preVelDir_dbg.z:F2}) " +
                    $"dashDir=({dashDir_dbg.x:F2},{dashDir_dbg.y:F2},{dashDir_dbg.z:F2})"
                );
            }
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
            Vector2 move = Input.Move;

            // yaw-only right basis
            Vector3 yawRight = Quaternion.Euler(0f, Input.AimYaw, 0f) * Vector3.right;

            Vector3 aimForward = character.AimDirection.normalized;

            // If we have meaningful input, use it
            if (move.sqrMagnitude > 0.0005f)
            {
                Vector3 dir = aimForward * move.y + yawRight * move.x;
                return dir.sqrMagnitude > 0.0001f ? dir.normalized : aimForward;
            }

            // No input: ALWAYS dash forward where aiming
            return aimForward;
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