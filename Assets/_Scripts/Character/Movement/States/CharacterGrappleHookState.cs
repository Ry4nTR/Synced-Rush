using SyncedRush.Character.Movement;
using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterGrappleHookState : CharacterMovementState
	{
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

            HookPull();

            ProcessMovement();

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();
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

            //if (CheckWallRunCondition(hit))
            //{
            //    character.WallRunStartInfo = hit;
            //    _canWallRun = true;
            //}
        }

        private void HookPull()
        {
            Vector3 moveDir = HookController.transform.position - character.CenterPosition;
            moveDir.Normalize();

            character.TotalVelocity += (character.Stats.HookPull * Time.deltaTime * moveDir);
        }

    }
}