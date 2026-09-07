using EarthGame.Engine;
using EarthGame.Transport;

namespace EarthGame.Server
{
    /// <summary>One connected player. Created by a valid Hello, destroyed by the connection closing.</summary>
    public sealed class PlayerSession
    {
        public PlayerSession(uint sessionId, string name, IConnection connection, long joinedTick)
        {
            SessionId = sessionId;
            Name = name;
            Connection = connection;
            JoinedTick = joinedTick;
        }

        /// <summary>Unique for the life of the server; never reused, so a stale reference cannot alias a new player.</summary>
        public uint SessionId { get; }
        public string Name { get; }
        public IConnection Connection { get; }
        public long JoinedTick { get; }

        /// <summary>The last body state the server accepted from this player; meaningful once <see cref="HasBody"/>.</summary>
        public MoverState Body;
        public float YawDeg;
        public float PitchDeg;
        public bool HasBody;
        /// <summary>Sequence number of the last accepted move, so a Correction can name the move it answers.</summary>
        public uint LastSequence;
        /// <summary>World tick at which the last move was accepted; the interval the next report is measured over.</summary>
        public long LastMoveTick;
        /// <summary>Counts for the N2 budget: every correction on a legal walk is a false positive.</summary>
        public int MovesAccepted;
        public int Corrections;
    }
}
