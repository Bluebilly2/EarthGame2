using EarthGame.Engine;

namespace EarthGame.Server
{
    /// <summary>What a server is started with. Read once at start; changing it later is a restart.</summary>
    public sealed class ServerConfig
    {
        /// <summary>Fast ticks per second (plan §4.3: 20 Hz).</summary>
        public int TickRate = 20;

        /// <summary>Most steps the loop will release per update before dropping time (see FixedStepAccumulator).</summary>
        public int MaxStepsPerUpdate = 5;

        /// <summary>Players allowed at once; a Hello beyond this is refused with the reason stated.</summary>
        public int MaxPlayers = 8;

        /// <summary>Shared secret a Hello must carry; empty means open. Friends-only: the wire is not encrypted.</summary>
        public string Password = string.Empty;

        /// <summary>Longest a player name may be; longer names are refused, never truncated silently.</summary>
        public int MaxPlayerNameLength = 32;

        /// <summary>The mover's numbers, shared with every client so the validator's ceiling is the mover's own.</summary>
        public MoverConfig Mover = MoverConfig.Default;

        /// <summary>How far a client's reported movement may stray from what the mover allows before it is corrected.</summary>
        public MovementRules Movement = new MovementRules();

        /// <summary>
        /// A player's state is sent only to sessions within this distance of them (horizontal metres). A session
        /// without an accepted body yet is sent everything, since it has no position to measure from.
        /// </summary>
        public double InterestRadiusM = 1500.0;
    }
}
