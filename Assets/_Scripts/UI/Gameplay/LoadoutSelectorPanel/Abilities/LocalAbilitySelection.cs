using SyncedRush.Character.Movement;

/// <summary>
/// Holds the locally selected ability for the current player.  This value is
/// set by the UI when the player picks an ability during the loadout phase.
/// It is not networked by itself; the MovementController reads this value
/// upon spawn and sets its AbilityProcessor accordingly.
/// </summary>
public static class LocalAbilitySelection
{
    public static CharacterAbility SelectedAbility = CharacterAbility.None;
}