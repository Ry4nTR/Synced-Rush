using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterMoveState : CharacterMovementState
    {
        private Vector3 _previousGroundNormal = Vector3.zero;
        private bool _blockCrouchInput = false;

        public CharacterMoveState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "MoveState"; }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            if (!CheckGround())
                return MovementState.Air;

            if (character.ConsumeJumpPressedIfAllowed())
                return MovementState.Jump;

            if (CheckSlideConditions())
                return MovementState.Slide;

            Walk();
            SnapToGround();

            character.AnimController.SetMoveSpeed(character.HorizontalVelocity.magnitude,
                character.Stats.WalkSpeed,
                character.Stats.RunSpeed);

            ProcessMovement();
            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            character.AnimController.SetGrounded(true);

            if (!(ParentStateMachine.PreviousStateEnum == MovementState.Air))
                _blockCrouchInput = true;

            SnapToGround();
            _previousGroundNormal = Vector3.zero;
        }
        public override void ExitState()
        {
            character.AnimController.SetGrounded(false);

            if (_previousGroundNormal != Vector3.zero)
            {
                character.TotalVelocity = Vector3.ProjectOnPlane(character.TotalVelocity, _previousGroundNormal);
            }

            base.ExitState();
        }

        public override void ProcessCollision(ControllerColliderHit hit)
        {
            base.ProcessCollision(hit);

            if (hit.normal.y >= 0.5f)
                return;

            Vector3 wallNormal = hit.normal;

            Vector3 currentVelocity = new(character.HorizontalVelocity.x, 0, character.HorizontalVelocity.y);

            Vector3 projectedVelocity = Vector3.ProjectOnPlane(currentVelocity, wallNormal);

            character.HorizontalVelocity = new Vector2(projectedVelocity.x, projectedVelocity.z);
        }

        protected new void ProcessMovement()
        {
            Vector3 desiredHorizontalMove = new(character.HorizontalVelocity.x, 0, character.HorizontalVelocity.y);

            Vector3 finalMoveVector = desiredHorizontalMove;

            if (character.TryGetGroundInfo(out RaycastHit gndInfo))
            {
                Vector3 projectedHorizontalMove = Vector3.ProjectOnPlane(desiredHorizontalMove, gndInfo.normal);

                finalMoveVector = projectedHorizontalMove + Vector3.up * character.VerticalVelocity;
            }

            character.Controller.Move(finalMoveVector * Time.fixedDeltaTime);
        }

        private bool CheckGround()
        {
            if (character.IsOnGround)
            {
                character.VerticalVelocity = -.1f;

                character.TryGetGroundInfo(out RaycastHit gndInfo);
                _previousGroundNormal = gndInfo.normal;
                return true;
            }
            else
                return false;
        }

        private bool CheckSlideConditions()
        {
            bool crouchIn = Input.Crouch;

            if (!crouchIn)
            {
                _blockCrouchInput = false;
                return false;
            }

            if (_blockCrouchInput)
                return false;

            Vector3 inputDir = character.MoveDirection;
            float dot = Vector3.Dot(inputDir, character.Orientation.transform.forward);
            return crouchIn && dot > -0.1f;
        }

        private void SnapToGround()
        {
            if (character.TryGetGroundInfo(out RaycastHit gndInfo))
            {
                character.Controller.Move(Vector3.down * gndInfo.distance);
            }
        }

        private void Walk()
        {
            // Always use CurrentInput through Input + MoveDirection (server/owner prediction will match)
            Vector2 move = Input.Move;
            bool sprintInput = Input.Sprint;

            Vector3 inputDir = character.MoveDirection;
            Vector2 moveDirXY = new(inputDir.x, inputDir.z);

            bool movingBackwardsOnly = Mathf.Approximately(move.y, -1f) && Mathf.Approximately(move.x, 0f);

            float targetSpeed = sprintInput && !movingBackwardsOnly ? character.Stats.RunSpeed : character.Stats.WalkSpeed;

            bool isOverSpeed = character.HorizontalVelocity.magnitude > targetSpeed;

            if (isOverSpeed)
            {
                character.HorizontalVelocity = Vector2.MoveTowards(
                    character.HorizontalVelocity,
                    moveDirXY * targetSpeed,
                    Time.fixedDeltaTime * character.Stats.GroundOverspeedDeceleration);
            }
            else
            {
                character.HorizontalVelocity = Vector2.MoveTowards(
                    character.HorizontalVelocity,
                    moveDirXY * targetSpeed,
                    Time.fixedDeltaTime * targetSpeed * character.Stats.GroundAccelDecel);
            }
        }
    }
}