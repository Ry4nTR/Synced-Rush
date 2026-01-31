using SyncedRush.Character.Movement;
using UnityEngine;

public sealed class JetpackAbilitySim
{
    private MovementState _lastState = MovementState.Move;

    private int _lastJumpCount;
    private bool _jumpedSinceGround;

    private bool _eligible;
    private bool _blockUntilRelease;
    private bool _active;
    private int _lastPressCount;

    public void Tick(MovementController character, AbilityProcessor ability, in SimulationTickData input, in SimContext ctx)
    {
        if (ability.CurrentAbility != CharacterAbility.Jetpack)
        {
            _lastState = ctx.CurrentState;
            _lastJumpCount = input.JumpCount;
            return;
        }

        // Detect jump input (edge)
        if (input.JumpCount > _lastJumpCount)
            _jumpedSinceGround = true;

        bool enteredAirThisTick =
            ctx.CurrentState == MovementState.Air &&
            _lastState != MovementState.Air;

        if (enteredAirThisTick)
        {
            _eligible = _jumpedSinceGround || _lastState == MovementState.WallRun;

            _active = false;
            _lastPressCount = input.JetpackCount;
            _blockUntilRelease = input.JetHeld;
        }

        // Landing: reset jump history
        if (ctx.IsOnGround && _lastState == MovementState.Air)
        {
            _jumpedSinceGround = false;
            _eligible = false;
            _active = false;
            _blockUntilRelease = false;
            ability.StopJetpack();
        }

        if (!input.JetHeld)
        {
            _blockUntilRelease = false;
            _active = false;
            ability.StopJetpack();

            _lastState = ctx.CurrentState;
            _lastJumpCount = input.JumpCount;
            return;
        }

        if (!_eligible || _blockUntilRelease)
        {
            ability.StopJetpack();

            _lastState = ctx.CurrentState;
            _lastJumpCount = input.JumpCount;
            return;
        }

        if (input.JetpackCount > _lastPressCount)
        {
            _lastPressCount = input.JetpackCount;
            _active = true;
        }

        if (!_active)
        {
            ability.StopJetpack();

            _lastState = ctx.CurrentState;
            _lastJumpCount = input.JumpCount;
            return;
        }

        if (!ability.UseJetpack())
        {
            _active = false;
            ability.StopJetpack();
        }

        _lastState = ctx.CurrentState;
        _lastJumpCount = input.JumpCount;
    }

    public bool ShouldApplyThrust(
        MovementController character,
        AbilityProcessor ability,
        in SimulationTickData input,
        in SimContext ctx)
    {
        return ability.CurrentAbility == CharacterAbility.Jetpack
            && _eligible
            && _active
            && !_blockUntilRelease
            && input.JetHeld
            && !ctx.IsOnGround
            && ability.UsingJetpack;
    }
}
