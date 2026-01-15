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

            return MovementState.None;
        }

        public override void EnterState()
        {
            base.EnterState();
        }

        private void HookPull()
        {
            Vector3 moveDir = HookController.transform.position - character.CenterPosition;
            moveDir.Normalize();

            character.TotalVelocity += (character.Stats.HookPull * Time.deltaTime * moveDir);
        }

    }
}