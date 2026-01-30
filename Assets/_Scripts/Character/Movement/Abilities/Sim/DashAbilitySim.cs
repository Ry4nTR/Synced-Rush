using SyncedRush.Character.Movement;
using UnityEngine;

public sealed class DashAbilitySim
{
    private int _lastAbilityCount = -1;
    public bool WantsDashThisTick { get; private set; }

    public void Tick(MovementController character, AbilityProcessor ability, in GameplayInputData input)
    {
        WantsDashThisTick = false;

        if (ability.CurrentAbility != CharacterAbility.Jetpack)
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
        

        if (character.State == MovementState.Dash)
            return;

        if (ability.UseDash())
            WantsDashThisTick = true;
    }
}
