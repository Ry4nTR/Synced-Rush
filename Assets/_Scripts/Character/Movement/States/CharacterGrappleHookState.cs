using SyncedRush.Character.Movement;
using UnityEngine;

namespace SyncedRush.Character.Movement
{
	public class CharacterGrappleHookState : CharacterMovementState
	{
        public CharacterGrappleHookState(MovementController movementComponentReference) : base(movementComponentReference)
        {
        }

        public override string ToString() { return "GrappleHookState"; }


    }
}