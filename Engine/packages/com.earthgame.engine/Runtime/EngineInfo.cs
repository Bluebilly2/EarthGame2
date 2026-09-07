namespace EarthGame.Engine
{
    /// <summary>
    /// Identity of the engine library. The version is bumped by hand with the changelog; the wire protocol
    /// has its own version in EarthGame.Protocol because the two change for different reasons.
    /// </summary>
    public static class EngineInfo
    {
        /// <summary>Semantic version of the engine-free library.</summary>
        public const string Version = "0.1.0";
    }
}
