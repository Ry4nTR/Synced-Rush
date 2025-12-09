using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterWallRunState : CharacterMovementState
    {
        public CharacterWallRunState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "WallRunState"; }

        public override MovementState ProcessFixedUpdate()
        {
            base.ProcessFixedUpdate();

            //if (!CheckGround())
            //    return MovementState.Air;

            //if (Input.Jump)
            //    return MovementState.Jump;

            //if (!Input.Crouch || character.HorizontalVelocity.magnitude < 1f)
            //    return MovementState.Move;

            //Slide();

            //ProcessMovement();
            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();


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

        protected override void ProcessMovement()
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
                return true;
            }
            else
                return false;
        }

        private void CheckWall()
        {
            float skinWidth = character.Controller.skinWidth;
            float rayLength = 0.1f + skinWidth;
            Vector3 startPosition = character.Controller.transform.position + character.Controller.center;

            RaycastHit hit;
            bool hasHit = Physics.Raycast(
                startPosition,
                //Vector3.down,
                character.Orientation.transform.forward,
                out hit,
                rayLength,
                character.LayerMask
            );

            //TODO da rimuovere quando non serve più
            Color rayColor = hasHit ? Color.green : Color.red;
            Debug.DrawRay(startPosition, Vector3.down * rayLength, rayColor, Time.fixedDeltaTime);

            //_groundInfo = hit;
            //IsOnGround = hasHit;
        }

        private void Slide()
        {
            Vector3 inputDir = character.MoveDirection;

            if (character.TryGetGroundInfo(out RaycastHit gndInfo))
            {
                if (Input.Move.y > 0f)
                    character.HorizontalVelocity += character.Stats.SlideMoveInfluence * Time.fixedDeltaTime * new Vector2(inputDir.x, inputDir.z);

                character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity, Vector2.zero, character.Stats.SlideDeceleration * Time.fixedDeltaTime);

                Vector2 slopeDir = new(gndInfo.normal.x, gndInfo.normal.z);
                if (!(Mathf.Approximately(slopeDir.x, 0f) && Mathf.Approximately(slopeDir.y, 0f)))
                {
                    slopeDir.Normalize();
                    float n = Mathf.Abs(gndInfo.normal.y - 1);
                    character.HorizontalVelocity += character.Stats.Gravity * n * 5 * Time.fixedDeltaTime * slopeDir; //TODO rimpiazzare l'operando 5 (è un valore hardcodato)
                }
            }
        }

    }
}
