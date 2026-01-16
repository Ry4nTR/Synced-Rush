using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterGrappleHookState : CharacterMovementState
	{
        private float _headBodyDistance = 0f;

        private bool _canWallRun = false;

        private HookController HookController { get { return character.HookController; } }

        public CharacterGrappleHookState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "GrappleHookState"; }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            if (!HookController.IsHooked)
                return MovementState.Air;

            if (_canWallRun)
            {
                HookController.Retreat();
                return MovementState.WallRun;
            }

            HookPull();

            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            _headBodyDistance = character.CameraPosition.y - character.CenterPosition.y;

            _canWallRun = false;
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

            if (CheckWallRunCondition(hit))
            {
                character.WallRunStartInfo = hit;
                _canWallRun = true;
            }
        }

        protected override void ProcessMovement()
        {
            Vector3 _velocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);
            _velocity *= Time.deltaTime;

            float center = Mathf.MoveTowards(0f, _headBodyDistance, Time.deltaTime * 2.5f);
            _headBodyDistance -= center;

            _velocity.y += center;

            character.Controller.Move(_velocity);
        }

        private void HookPull()
        {
            Vector3 moveDir = HookController.transform.position - character.CenterPosition;
            moveDir.Normalize();

            character.TotalVelocity += (character.Stats.HookPull * Time.deltaTime * moveDir);
        }

        private bool CheckWallRunCondition(ControllerColliderHit hit)
        {
            if (character.HorizontalVelocity.magnitude < character.Stats.WallRunMinSpeed)
                return false;

            if (
                hit.normal.y < 0.1f
                && hit.normal.y > -0.1f
                )
            {

                float skinWidth = character.Controller.skinWidth;
                float rayLength = 1f + skinWidth;

                Vector3 _wallDir = hit.point - character.CenterPosition;

                _wallDir.y = 0f;

                if (Mathf.Approximately(_wallDir.magnitude, 0))
                    return false;

                _wallDir.Normalize();

                Vector3 startPosition = character.CenterPosition + _wallDir * (character.Controller.radius / 2f);

                RaycastHit rayHit;
                bool hasHit = Physics.Raycast(
                    startPosition,
                    _wallDir,
                    out rayHit,
                    rayLength,
                    character.LayerMask
                );

                //TODO da rimuovere quando non serve più
                Color rayColor = hasHit ? Color.green : Color.red;
                Debug.DrawRay(startPosition, _wallDir * rayLength, rayColor, Time.deltaTime);

                Vector2 hitN = new(rayHit.normal.x, rayHit.normal.z);
                Vector2 lookDir = new(character.Orientation.transform.forward.x, character.Orientation.transform.forward.z);

                float angle = Vector2.Angle(hitN, -lookDir);
                Debug.Log("AirState " + angle); //TODO da rimuovere

                Vector3 inputDir = (character.IsServer || character.LocalInputHandler == null)
                    ? character.MoveDirection
                    : character.LocalMoveDirection;

                bool moveInputToWall = Vector3.Dot(inputDir, rayHit.normal) < 0;

                if (hasHit
                    && rayHit.normal.y < 0.1f
                    && rayHit.normal.y > -0.1f
                    && angle < character.Stats.WallRunLookAngleLimit
                    && moveInputToWall)
                {
                    return true;
                }
            }
            return false;
        }

    }
}