using SyncedRush.Character.Movement;

public readonly struct SimContext
{
    // Ground truth for this simulation tick
    public readonly bool IsOnGround;

    // State machine info (lets abilities apply "jump-enter-air" rules cleanly)
    public readonly MovementState CurrentState;
    public readonly MovementState PreviousState;

    public SimContext(bool isOnGround, MovementState currentState, MovementState previousState)
    {
        IsOnGround = isOnGround;
        CurrentState = currentState;
        PreviousState = previousState;
    }

    public bool EnteredAirThisTick =>
        CurrentState == MovementState.Air &&
        PreviousState != MovementState.Air;

    public bool EnteredAirFromEligibleState =>
        EnteredAirThisTick &&
        (PreviousState == MovementState.Jump || PreviousState == MovementState.WallRun);
}
