using EarthGame.Engine;

namespace EarthGame.Server
{
    /// <summary>
    /// A founder's work in progress (BF.2): what kind, on what, with the hand it began with, how many seconds it takes at
    /// full capacity and how many have been done, and where the founder stood when it began. Held by the session while the
    /// work runs, advanced every server step, and dropped — never saved — when it ends, when the founder moves, when the
    /// target goes, when the hand changes, or when the founder leaves.
    /// </summary>
    public sealed class WorkInProgress
    {
        public WorkKind Kind;
        /// <summary>The target's kind as the intent named it: an entity, a lying thing or a place of the hands.</summary>
        public byte Target;
        public ulong EntityId;
        public LyingThing Lying;
        public byte Place;
        /// <summary>The cell a standing target or a ground target names (BF.3), and the tuft's index on it.</summary>
        public int Row;
        public int Col;
        public int Index;
        /// <summary>How far a trunk's cut had gone when this work began, 0 to 1 (BF.3): the work's seconds are the rest of it.</summary>
        public double CutStart01;
        /// <summary>The hand's place when the work began; a change of hand stops it.</summary>
        public byte ToolPlace;
        /// <summary>Seconds the work takes at a body's full capacity.</summary>
        public double Seconds;
        /// <summary>Seconds of work done, at the capacity each step was done at.</summary>
        public double Progress;
        public Double3 StartedAt;
        /// <summary>The tick the client was last told the progress at.</summary>
        public long ToldTick;
        /// <summary>The offer's words, said while it runs.</summary>
        public string Words;
    }
}
