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
    }
}
