using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterMoveState : CharacterMovementState
	{
        public CharacterMoveState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString()
        {
            return "MoveState";
        }

        public override MovementState ProcessFixedUpdate()
        {
            base.ProcessFixedUpdate();

            if (!CheckGround())
                return MovementState.Air;

            if (Input.Jump)
                return MovementState.Jump;

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

        //protected override void ProcessMovement()
        //{
        //    //bool hasHit = Physics.Raycast(character.transform.position, Vector3.down, out RaycastHit hit, 5f);
        //    //if (hasHit)
        //    //{
        //    //    Debug.Log("Info raycast"
        //    //        + "\nPunto colpito: " + hit.point.ToString()
        //    //        + " Normale: " + hit.normal.ToString()
        //    //        + " Oggetto colpito: " + hit.transform.name);
        //    //}

        //    Vector3 _velocity = new(character.HorizontalVelocity.x, character.VerticalVelocity, character.HorizontalVelocity.y);

        //    //if (hasHit)
        //    //    _velocity = Vector3.ProjectOnPlane(_velocity, hit.normal);

        //    character.Controller.Move(_velocity * Time.fixedDeltaTime);
        //}

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

            if (Input.Sprint && Input.Move.y > 0f )
                character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                    new Vector2(inputDir.x, inputDir.z) * character.Stats.RunSpeed,
                    Time.fixedDeltaTime * character.Stats.RunSpeed * 10);
            else
                character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity,
                    new Vector2(inputDir.x, inputDir.z) * character.Stats.WalkSpeed,
                    Time.fixedDeltaTime * character.Stats.RunSpeed * 10);

            if (character.TryGetGroundInfo(out RaycastHit gndInfo))
            {
                character.TotalVelocity = Vector3.ProjectOnPlane(character.TotalVelocity, gndInfo.normal);
            }

            //character.HorizontalVelocity = Vector2.MoveTowards(character.HorizontalVelocity, 
            //    new Vector2(motion.x, motion.z) * character.Stats.WalkSpeed, 
            //    Time.fixedDeltaTime * 10f);
        }

	}
}