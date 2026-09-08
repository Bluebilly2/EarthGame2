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
        /// <summary>World tick at which the last move was accepted.</summary>
        public long LastMoveTick;
        /// <summary>Real seconds this session may still claim for movement (MovementRules.MoveCreditCapSeconds); accrued by the host's elapsed time, spent by accepted reports.</summary>
        public double MoveCredit;
        /// <summary>True from the Welcome until the snapshot has gone out, which happens after the next step (or at once while paused).</summary>
        public bool SnapshotPending;
        /// <summary>Where the joiner was told it stands, for the interest radius of its snapshot.</summary>
        public double SnapshotEast;
        public double SnapshotNorth;
        /// <summary>Counts for the N2 budget: every correction on a legal walk is a false positive.</summary>
        public int MovesAccepted;
        public int Corrections;
    }
}
