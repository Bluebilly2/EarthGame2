using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Server
{
    /// <summary>The instruments M1.B's numbers come from: the tick statistics, the mirror's sampling, and the digests.</summary>
    public sealed class MeasurementTests
    {
        [Test]
        public void TickStatsSummariseAWindowAndStartTheNext()
        {
            TickStats stats = new TickStats(0.05);
            for (int i = 1; i <= 100; i++) stats.Record(i / 1000.0);
            TickWindow w = stats.Snapshot();
            Assert.That(w.Count, Is.EqualTo(100));
            Assert.That(w.OverInterval, Is.EqualTo(50), "51 ms to 100 ms overran a 50 ms interval");
            Assert.That(w.MaxSeconds, Is.EqualTo(0.1).Within(1e-12));
            Assert.That(w.MeanSeconds, Is.EqualTo(0.0505).Within(1e-12));
            Assert.That(w.P95Seconds, Is.EqualTo(0.095).Within(1e-12));
            TickWindow empty = stats.Snapshot();
            Assert.That(empty.Count, Is.EqualTo(0));
            Assert.That(empty.P95Seconds, Is.EqualTo(0.0));
        }

        private static PlayerStateMessage StateAt(long tick, double east, float yaw)
        {
            PlayerStateMessage s;
            s.SessionId = 2;
            s.Sequence = (uint)tick;
            s.ServerTick = tick;
            s.YawDeg = yaw;
            s.PitchDeg = 0f;
            s.Body = MoverState.AtRest(east, 1.0, 0.0);
            return s;
        }

        [Test]
        public void TheMirrorInterpolatesBetweenStatesAndHoldsBeyondThem()
        {
            RemoteMirror m = new RemoteMirror(2);
            m.Push(StateAt(10, 0.0, 350f));
            m.Push(StateAt(20, 10.0, 10f));
            m.Push(StateAt(15, 99.0, 0f)); // late and older than the newest: dropped
            Assert.That(m.Count, Is.EqualTo(2));
            MirrorSample mid = m.Sample(15.0);
            Assert.That(mid.Interpolated, Is.True);
            Assert.That(mid.East, Is.EqualTo(5.0).Within(1e-9));
            Assert.That(mid.YawDeg, Is.EqualTo(0f).Within(1e-4f), "350 to 10 turns through north, not the long way");
            MirrorSample before = m.Sample(5.0);
            Assert.That(before.Interpolated, Is.False);
            Assert.That(before.East, Is.EqualTo(0.0));
            MirrorSample after = m.Sample(25.0);
            Assert.That(after.Interpolated, Is.False);
            Assert.That(after.East, Is.EqualTo(10.0));
            Assert.That(after.Latest.East, Is.EqualTo(10.0));
        }

        [Test]
        public void TheDigestIsOrderFreeAndSeesACentimetre()
        {
            MoverState a = MoverState.AtRest(1.0 / 3.0, 2.0, 3.0);
            MoverState b = MoverState.AtRest(-4.0, 0.5, 1.0);
            string one = WorldDigest.Bodies(new[] { new KeyValuePair<uint, MoverState>(1, a), new KeyValuePair<uint, MoverState>(2, b) });
            string two = WorldDigest.Bodies(new[] { new KeyValuePair<uint, MoverState>(2, b), new KeyValuePair<uint, MoverState>(1, a) });
            Assert.That(one, Is.EqualTo(two));
            Assert.That(one.Length, Is.EqualTo(16));
            MoverState moved = a;
            moved.East += 0.01;
            string three = WorldDigest.Bodies(new[] { new KeyValuePair<uint, MoverState>(1, moved), new KeyValuePair<uint, MoverState>(2, b) });
            Assert.That(three, Is.Not.EqualTo(one));
            Assert.That(WorldDigest.G12(1.0 / 3.0), Is.EqualTo("0.333333333333"));
            Assert.That(WorldDigest.Hex(WorldDigest.Fnv1a64("")), Is.EqualTo("cbf29ce484222325"), "the FNV-1a offset basis for the empty string");
        }

        [Test]
        public void TheWorldDigestNamesTheClockTheTickAndTheBodies()
        {
            WorldState world = new WorldState(1, Region.Bherwerre, Region.Bherwerre.WakeClock());
            List<KeyValuePair<string, MoverState>> bodies = new List<KeyValuePair<string, MoverState>>
            {
                new KeyValuePair<string, MoverState>("William", MoverState.AtRest(1.0, 2.0, 3.0)),
            };
            string first = WorldDigest.World(world, bodies);
            Assert.That(WorldDigest.World(world, bodies), Is.EqualTo(first));
            world.Step(0.05);
            Assert.That(WorldDigest.World(world, bodies), Is.Not.EqualTo(first), "a tick is part of the name");
        }
    }
}
