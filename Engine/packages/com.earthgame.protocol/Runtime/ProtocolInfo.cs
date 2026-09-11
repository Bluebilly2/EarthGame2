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
        /// <summary>
        /// Wire protocol version. History: 1 — M1.0 handshake (Hello, Welcome, Refused, Ping, Pong);
        /// 2 — M1.A movement (Welcome carries the spawn point; PlayerMove, PlayerState, Correction);
        /// 3 — M1.B streaming, the snapshot and leaving (TileRequest, TileHeader, TileChunk, SnapshotEnd, PlayerLeft);
        /// 4 — M1.3 entities inside the interest radius (EntitySpawn, EntityState, EntityGone);
        /// 5 — M1.4b a layer on each tile message, so the water travels beside the ground;
        /// 6 — M1.5a verbs and carrying (Intent, IntentResult, Carrying; an entity taken up is gone for a reason of its own);
        /// 7 — M1.5b taking what lies (a pick-up names a thing of the loose layer by its cell, kind and index; LooseTaken).
        /// </summary>
        public const ushort Version = 7;

        /// <summary>Bytes of tile data per TileChunk; well under <see cref="MaxMessageBytes"/> with the header.</summary>
        public const int TileChunkBytes = 16 * 1024;

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
