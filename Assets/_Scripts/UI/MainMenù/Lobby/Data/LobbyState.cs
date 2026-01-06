public enum LobbyState
{
    Inactive,   // No lobby exists
    Open,       // Lobby created, players can join
    Locked,      // Match starting, no changes allowed
    InGame      // Match in progress
}
