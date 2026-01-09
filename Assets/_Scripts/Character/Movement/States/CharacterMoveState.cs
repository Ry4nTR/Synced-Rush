using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterMoveState : CharacterMovementState
    {
        private Vector3 _previousGroundNormal = Vector3.zero;

        public CharacterMoveState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "MoveState"; }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();


            if (!CheckGround())
                return MovementState.Air;

            // Determine jump input based on simulation context.  On the server use the
            // networked input; on the client read from the PlayerInputHandler.  If no
            // handler is available (e.g. remote clients) default to false.
            bool jumpInput = (character.IsServer || character.LocalInputHandler == null)
                ? Input.Jump
                : character.LocalInputHandler.Jump;

            if (jumpInput)
                return MovementState.Jump;

            if (CheckSlideConditions())
                return MovementState.Slide;

            bool dashInput = (character.IsServer || character.LocalInputHandler == null)
                ? Input.Ability
                : character.LocalInputHandler.Ability;
            if (dashInput)
                return MovementState.Dash;

            Walk();
            SnapToGround();

            ProcessMovement();
            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            SnapToGround();
            _previousGroundNormal = Vector3.zero;
        }
        public override void ExitState()
        {
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

            character.Controller.Move(finalMoveVector * Time.deltaTime);
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
            // On the server (or when no local input handler is available) use the networked
            // input to decide if the player is crouching and sprinting.  On the client
            // prediction path, read these values from the PlayerInputHandler and compute the
            // input direction from the local move vector.
            if (character.IsServer || character.LocalInputHandler == null)
            {
                Vector3 inputDir = character.MoveDirection;
                float dot = Vector3.Dot(inputDir, character.Orientation.transform.forward);
                return Input.Crouch && dot > -0.1f;
            }
            else
            {
                Vector2 move = character.LocalInputHandler.Move;
                Vector3 inputDir = character.Orientation.transform.forward * move.y + character.Orientation.transform.right * move.x;
                if (inputDir.magnitude > 1f)
                    inputDir.Normalize();
                float dot = Vector3.Dot(inputDir, character.Orientation.transform.forward);
                bool crouchInput = character.LocalInputHandler.Crouch;
                return crouchInput && dot > -0.1f;
            }
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
            // On the server use networked input; on the client use local input from the
            // PlayerInputHandler.  In both cases compute an input direction in world space
            // and choose run or walk speed based on the sprint key.
            if (character.IsServer || character.LocalInputHandler == null)
            {
                Vector3 inputDir = character.MoveDirection;
                bool sprint = Input.Sprint;
                Vector2 move = Input.Move;
                bool movingBackwardsOnly = Mathf.Approximately(move.y, -1f) && Mathf.Approximately(move.x, 0f);
                float targetSpeed = sprint && !movingBackwardsOnly ? character.Stats.RunSpeed : character.Stats.WalkSpeed;
                character.HorizontalVelocity = Vector2.MoveTowards(
                    character.HorizontalVelocity,
                    new Vector2(inputDir.x, inputDir.z) * targetSpeed,
                    Time.deltaTime * character.Stats.RunSpeed * 10);
            }
            else
            {
                Vector2 move = character.LocalInputHandler.Move;
                // Compute a direction from the local move input relative to character orientation.
                Vector3 inputDir = character.Orientation.transform.forward * move.y + character.Orientation.transform.right * move.x;
                if (inputDir.magnitude > 1f)
                    inputDir.Normalize();
                bool sprintInput = character.LocalInputHandler.Sprint;
                bool movingBackwardsOnly = Mathf.Approximately(move.y, -1f) && Mathf.Approximately(move.x, 0f);
                float targetSpeed = sprintInput && !movingBackwardsOnly ? character.Stats.RunSpeed : character.Stats.WalkSpeed;
                character.HorizontalVelocity = Vector2.MoveTowards(
                    character.HorizontalVelocity,
                    new Vector2(inputDir.x, inputDir.z) * targetSpeed,
                    Time.deltaTime * character.Stats.RunSpeed * 10);
            }
        }
    }
}