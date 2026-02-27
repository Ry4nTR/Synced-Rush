using Unity.Netcode;

namespace SyncedRush.Gamemode
{
    /// <summary>
    /// Centralised match flow state for the game.  Use this enumeration as the single
    /// source of truth for whether the game is loading, waiting for players, in the
    /// countdown, playing a round, etc.  This replaces the previous MatchState
    /// enumeration which only represented a subset of the flow.
    /// </summary>
    public enum MatchFlowState
    {
        Lobby = 0,
        /// <summary>
        /// A new match has been requested and the server is loading the map scene.
        /// Clients should show a loading screen during this state.
        /// </summary>
        Loading = 1,
        /// <summary>
        /// The map has finished loading and the server is spawning players.  Players
        /// should not move during this state.
        /// </summary>
        Spawning = 2,
        /// <summary>
        /// All players have been spawned and are frozen while the pre‑round
        /// countdown runs.  Gameplay input is disabled.
        /// </summary>
        PreRoundFrozen = 3,
        /// <summary>
        /// The round is active and gameplay input is enabled.  This is the only
        /// state where movement, shooting and abilities should run.
        /// </summary>
        InRound = 4,
        /// <summary>
        /// The round has ended and players are frozen while the scoreboard is
        /// displayed.
        /// </summary>
        RoundEnd = 5,
        /// <summary>
        /// The entire match has ended and a victor has been determined.  Clients
        /// should return to the lobby after this state.
        /// </summary>
        MatchEnd = 6
    }
}