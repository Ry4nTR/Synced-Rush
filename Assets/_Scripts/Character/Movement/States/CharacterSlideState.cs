using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterSlideState : CharacterMovementState
    {
        private float _currentEndSpeed = 0f;
        private bool _isEnding = false;
        private Vector3 _previousGroundNormal = Vector3.zero;

        private float EndSpeed {
            get
            {
                return (character.HorizontalVelocity.magnitude / 100) * character.Stats.SlideIncreasedDecelerationThreshold;
            }
        }

        public CharacterSlideState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "SlideState"; }

        public override MovementState ProcessFixedUpdate()
        {
            base.ProcessFixedUpdate();

            if (!CheckGround())
                return MovementState.Air;

            if (Input.Jump)
                return MovementState.Jump;

            if (!Input.Crouch || character.HorizontalVelocity.magnitude < 1f)
                return MovementState.Move;

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

            Vector2 inputDir = new(character.MoveDirection.x, character.MoveDirection.z);

            if (!Mathf.Approximately(inputDir.magnitude, 0f))
            {
                character.HorizontalVelocity += inputDir.normalized * character.Stats.SlideStartBoost;
            }

            _isEnding = false;

            _previousGroundNormal = Vector3.zero;

            _currentEndSpeed = EndSpeed;
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
            Vector3 inputDir = character.MoveDirection;

            if (character.TryGetGroundInfo(out RaycastHit gndInfo))
            {
                if (Input.Move.magnitude > 0f)
                    character.HorizontalVelocity += character.Stats.SlideMoveInfluence * Time.fixedDeltaTime * new Vector2(inputDir.x, inputDir.z);

                if (!_isEnding)
                    character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity, Vector2.zero, character.Stats.SlideDeceleration * Time.fixedDeltaTime);
                else
                    character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity, Vector2.zero, character.Stats.SlideIncreasedDeceleration * Time.fixedDeltaTime);

                Vector2 slopeDir = new(gndInfo.normal.x, gndInfo.normal.z);
                if (!(Mathf.Approximately(slopeDir.x, 0f) && Mathf.Approximately(slopeDir.y, 0f)))
                {
                    slopeDir.Normalize();
                    float n = Mathf.Abs(gndInfo.normal.y - 1);
                    character.HorizontalVelocity += character.Stats.Gravity * n * 15 * Time.fixedDeltaTime * slopeDir; //TODO rimpiazzare l'operando 15 (è un valore hardcodato)
                }
            }
        }

    }
}
