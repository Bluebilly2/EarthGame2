using System;
using System.Collections.Generic;
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
        /// <summary>
        /// The height of the last body this player reported standing on the ground, m (M1.5f): the first report's, then every
        /// report on their feet, and a saved founder's from their save; NaN before any. A body off its feet is believed to
        /// cross the ground as fast as a run and the height lost since allow.
        /// </summary>
        public double StoodUp = double.NaN;
        /// <summary>True from the Welcome until the snapshot has gone out, which happens after the next step (or at once while paused).</summary>
        public bool SnapshotPending;
        /// <summary>Where the joiner was told it stands, for the interest radius of its snapshot.</summary>
        public double SnapshotEast;
        public double SnapshotNorth;
        /// <summary>Counts for the N2 budget: every correction on a legal walk is a false positive.</summary>
        public int MovesAccepted;
        public int Corrections;
        /// <summary>The entities this session has been shown and not told are gone, each with the tick of the newest state sent for it.</summary>
        public readonly Dictionary<ulong, long> Interest = new Dictionary<ulong, long>();
        /// <summary>What this founder carries and which place is the hand (M1.5a), taken over from the saved player at the join.</summary>
        public readonly Hands Hands = new Hands();
        /// <summary>The water in this founder's body (FP.1), lost on the world's clock, restored from their save at the join.</summary>
        public readonly Hydration Hydration = new Hydration();
        /// <summary>The warmth of this founder's body (FP.2): its core and the heat balance, run every step in the surroundings below.</summary>
        public readonly Warmth Warmth = new Warmth();
        /// <summary>The air, sky and sun at this founder's body, read once a second: the weather at a point is cheap, the ground's openness less so.</summary>
        public Surroundings Surroundings;
        /// <summary>The world tick the surroundings were last read at; negative before any (see <see cref="FounderStateTick"/>).</summary>
        public long SurroundingsTick = -1;
        /// <summary>
        /// The world tick the founder's state was last sent at: once a second, and at every change that matters. Negative
        /// before any: a sentinel, not the smallest long, since a tick less the smallest long overflows and reads as "recent"
        /// (FP.2's first cooling test found the surroundings never read for that reason).
        /// </summary>
        public long FounderStateTick = -1;
        /// <summary>
        /// The work capacity this founder's client was last told, and the one before it. The validator's ceiling is the
        /// greater (<see cref="CeilingCapacity"/>), so a client walking on the capacity it was told a moment ago is never
        /// corrected for a number it has not yet received.
        /// </summary>
        public double ToldCapacity = 1.0, ToldCapacityBefore = 1.0;
        public double CeilingCapacity => Math.Max(ToldCapacity, ToldCapacityBefore);
    }
}
