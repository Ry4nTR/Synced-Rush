using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterMoveState : CharacterMovementState
	{
        public CharacterMoveState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "MoveState"; }

        public override MovementState ProcessFixedUpdate()
        {
            base.ProcessFixedUpdate();

            if (!CheckGround())
                return MovementState.Air;

            if (Input.Jump)
                return MovementState.Jump;

            if (Input.Crouch && Input.Sprint && character.HorizontalVelocity.magnitude > 1f)
                return MovementState.Slide;

            Walk();

            ProcessMovement();
            return MovementState.None;
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

        private void Walk()
        {
            Vector3 inputDir = character.MoveDirection;

            if (Input.Sprint
                && !(Mathf.Approximately(Input.Move.y, -1) && Mathf.Approximately(Input.Move.x, 0))
                )
                character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                    new Vector2(inputDir.x, inputDir.z) * character.Stats.RunSpeed,
                    Time.fixedDeltaTime * character.Stats.RunSpeed * 10);
            else
                character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                    new Vector2(inputDir.x, inputDir.z) * character.Stats.WalkSpeed,
                    Time.fixedDeltaTime * character.Stats.RunSpeed * 10);
        }
	}
}