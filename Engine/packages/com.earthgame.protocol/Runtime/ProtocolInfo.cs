namespace EarthGame.Protocol
{
    /// <summary>
    /// The wire protocol's version and limits. A client whose <see cref="Version"/> differs from the server's
    /// is refused at Hello with the reason stated; nothing is negotiated. Bump the version whenever any message
    /// layout changes, in the same commit, and record the change in ARCHITECTURE.md's format log (run-log and
    /// wire formats are contracts: owner ruling 2026-09-07).
    /// </summary>
    public static class ProtocolInfo
    {
        /// <summary>Wire protocol version. History: 1 — M1.0 handshake (Hello, Welcome, Refused, Ping, Pong).</summary>
        public const ushort Version = 1;

        /// <summary>
        /// Largest payload a single message may carry. Well under the ~64 KB reliable-fragmentation ceiling of
        /// UDP transports; anything larger (a layer tile, a baseline) is streamed as a sequence of messages.
        /// </summary>
        public const int MaxMessageBytes = 32 * 1024;

        /// <summary>Longest string the writer will encode (UTF-8 bytes).</summary>
        public const int MaxStringBytes = 4096;
    }

    /// <summary>
    /// Raised by the reader when a message is shorter than its layout claims, or by the writer when a value
    /// exceeds the protocol's limits. A malformed packet from a peer is a refusal, never a crash.
    /// </summary>
    public sealed class ProtocolException : System.Exception
    {
        public ProtocolException(string message) : base(message) { }
    }
}
