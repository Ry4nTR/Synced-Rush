using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public class CharacterSlideState : CharacterMovementState
    {
        private float _currentEndSpeed = 0f;
        private bool _isEnding = false;
        private bool _blockCrouchInput = false;
        private Vector3 _previousGroundNormal = Vector3.zero;

        private float EndSpeed
        {
            get
            {
                return (character.HorizontalVelocity.magnitude / 100) * character.Stats.SlideIncreasedDecelerationThreshold;
            }
        }

        public CharacterSlideState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "SlideState"; }

        public override MovementState ProcessUpdate()
        {
            base.ProcessUpdate();

            if (!CheckGround())
                return MovementState.Air;

            // Determine jump and crouch inputs based on context.  Use networked input on
            // the server or when the local input handler is unavailable.  Otherwise use the
            // values from the PlayerInputHandler.
            bool jumpInput = Input.Jump;
            bool crouchInput = Input.Crouch;

            if (!crouchInput)
                _blockCrouchInput = false;

            if (jumpInput)
                return MovementState.Jump;

            if (character.HorizontalVelocity.magnitude < 1f)
                return MovementState.Move;

            if (crouchInput && !_blockCrouchInput)
                return MovementState.Move;

            if (character.Ability.CurrentAbility == CharacterAbility.Jetpack)
            {
                bool dashInput = Input.Ability;
                if (dashInput && character.Ability.UseDash())
                    return MovementState.Dash;
            }

            Slide();

            if (EndSpeed > _currentEndSpeed)
                _currentEndSpeed = EndSpeed;

            if (character.HorizontalVelocity.magnitude <= _currentEndSpeed)
                _isEnding = true;

            ProcessMovement();
            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();

            character.AnimController.SetSliding(true);

            _blockCrouchInput = true;

            // Use predicted/authoritative MoveDirection (already based on CurrentInput)
            Vector3 worldDir = character.MoveDirection;
            worldDir.y = 0f;

            if (!Mathf.Approximately(worldDir.magnitude, 0f))
            {
                worldDir.Normalize();
                character.HorizontalVelocity += new Vector2(worldDir.x, worldDir.z) * character.Stats.SlideStartBoost;
            }

            _isEnding = false;
            _previousGroundNormal = Vector3.zero;
            _currentEndSpeed = EndSpeed;
        }

        public override void ExitState()
        {
            character.AnimController.SetSliding(false);

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

        private void Slide()
        {
            // Apply movement influence and deceleration while sliding.  The player can
            // influence the slide direction by holding movement input.  On the server
            // use the networked input; on the client use the local input handler.
            if (character.TryGetGroundInfo(out RaycastHit gndInfo))
            {
                if (character.IsServer || character.LocalInputHandler == null)
                {
                    // Use authoritative input.  Add slide influence only if there is non-zero
                    // movement input.
                    if (Input.Move.magnitude > 0f)
                    {
                        Vector3 inputDir = character.MoveDirection;
                        character.HorizontalVelocity += character.Stats.SlideMoveInfluence * Time.fixedDeltaTime * new Vector2(inputDir.x, inputDir.z);
                    }
                }
                else
                {
                    // On the client, compute the input direction from the local input and
                    // orientation.  Normalize the direction to avoid magnitude scaling.
                    Vector2 localMove = character.LocalInputHandler.Move;
                    if (localMove.magnitude > 0f)
                    {
                        Vector3 dir = character.Orientation.transform.forward * localMove.y + character.Orientation.transform.right * localMove.x;
                        if (dir.magnitude > 1f)
                            dir.Normalize();
                        character.HorizontalVelocity += character.Stats.SlideMoveInfluence * Time.fixedDeltaTime * new Vector2(dir.x, dir.z);
                    }
                }

                // Apply deceleration while sliding.  When the slide is ending we use
                // increased deceleration.
                if (!_isEnding)
                {
                    character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity, Vector2.zero, character.Stats.SlideDeceleration * Time.fixedDeltaTime);
                }
                else
                {
                    character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity, Vector2.zero, character.Stats.SlideIncreasedDeceleration * Time.fixedDeltaTime);
                }

                // Add additional velocity in the direction of the slope when sliding down.
                Vector2 slopeDir = new(gndInfo.normal.x, gndInfo.normal.z);
                if (!(Mathf.Approximately(slopeDir.x, 0f) && Mathf.Approximately(slopeDir.y, 0f)))
                {
                    slopeDir.Normalize();
                    float n = Mathf.Abs(gndInfo.normal.y - 1);
                    character.HorizontalVelocity += character.Stats.Gravity * n * 15 * Time.fixedDeltaTime * slopeDir; // TODO: replace 15 with a configurable multiplier if needed
                }
            }
        }

    }
}
