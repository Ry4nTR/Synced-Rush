using UnityEngine;

namespace SyncedRush.Character.Movement
{
    public sealed class GrappleAbilitySim
    {
        private int _lastAbilityCount = -1;

        public bool WantsToggleThisTick { get; private set; }
        public bool WantsEnterGrappleStateThisTick { get; private set; }

        public void Tick(MovementController character, AbilityProcessor ability, in SimulationTickData input)
        {
            WantsToggleThisTick = false;
            WantsEnterGrappleStateThisTick = false;

            if (ability.CurrentAbility != CharacterAbility.Grapple)
            {
                _lastAbilityCount = -1;
                return;
            }

            int ab = input.AbilityCount;

            if (_lastAbilityCount < 0)
            {
                _lastAbilityCount = ab;
                return;
            }

            if (ab == _lastAbilityCount)
                return;

            bool pressed = ab > _lastAbilityCount;
            _lastAbilityCount = ab;

            if (!pressed)
                return;

            WantsToggleThisTick = true;

            // “Enter grapple state” is derived from hook state after toggle is applied.
            // We set it after executing shoot/retreat in MovementController.
        }

        public void ResetRuntime()
        {
            _lastAbilityCount = -1;
            WantsToggleThisTick = false;
            WantsEnterGrappleStateThisTick = false;
        }
    }
}
