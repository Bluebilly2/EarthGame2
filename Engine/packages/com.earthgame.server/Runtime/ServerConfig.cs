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
        /// The beta arc's bridge (the owner, 2026-09-16, CANON ruling 33): the first night is a night like any other, and a
        /// founder must do things to live through it; those things (fire, shelter) are not yet in the game, so while they are
        /// not, the cold does not kill. The body still cools and the words still come, down to severely hypothermic; the core
        /// is held just above the lethal and the sun brings it back at dawn. Thirst still kills, since water can be drunk. On
        /// for every game until the bridge is taken down (DEBTS); off for the scenario that proves death (<c>-eg-no-bridge</c>,
        /// <c>+server.bridge 0</c>).
        /// </summary>
        public bool BetaArcBridge = true;

        /// <summary>
        /// A player's state is sent only to sessions within this distance of them (horizontal metres). A session
        /// without an accepted body yet is sent everything, since it has no position to measure from.
        /// </summary>
        public double InterestRadiusM = 1500.0;

        /// <summary>An entity that entered a session's interest leaves it only this far beyond the radius, so one on the edge does not flap.</summary>
        public double InterestMarginM = 50.0;

        /// <summary>Bytes of entity spawns and states one session is sent per tick; what does not fit waits, the stamps holding it. A gone never waits.</summary>
        public int EntityBytesPerTick = 4096;
    }
}
