using EarthGame.Engine;
using EarthGame.Protocol;
using NUnit.Framework;

namespace EarthGame.Tests.Protocol
{
    /// <summary>The protocol-2 messages go out through the writer and come back through the reader unchanged.</summary>
    public sealed class MovementMessageTests
    {
        private static PacketReader ReaderOver(PacketWriter w)
        {
            byte[] bytes = w.ToArray();
            PacketReader r = new PacketReader(bytes);
            r.ReadByte();
            return r;
        }

        private static MoverState Body()
        {
            MoverState s = MoverState.AtRest(-2410.125, 3.5, -2113.75);
            s.VelEast = 1.25;
            s.VelUp = -0.5;
            s.VelNorth = 2.0;
            s.Grounded = true;
            s.Wading = true;
            s.Stance = Stance.Crouching;
            return s;
        }

        [Test]
        public void PlayerMoveRoundTrips()
        {
            PlayerMoveMessage m;
            m.Sequence = 77;
            m.Input = MoverInput.Walk(0.5, -0.25, sprint: true);
            m.Input.Jump = true;
            m.YawDeg = 123.5f;
            m.PitchDeg = -10.25f;
            m.Body = Body();
            PacketWriter w = new PacketWriter();
            m.Write(w);
            Assert.That(MessageHeader.PeekKind(w.ToArray(), 0, w.Length), Is.EqualTo(MessageKind.PlayerMove));
            PacketReader r = ReaderOver(w);
            PlayerMoveMessage back = PlayerMoveMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Sequence, Is.EqualTo(77));
            Assert.That(back.Input.WishEast, Is.EqualTo(0.5));
            Assert.That(back.Input.WishNorth, Is.EqualTo(-0.25));
            Assert.That(back.Input.Sprint, Is.True);
            Assert.That(back.Input.Jump, Is.True);
            Assert.That(back.Input.Crouch, Is.False);
            Assert.That(back.YawDeg, Is.EqualTo(123.5f));
            Assert.That(back.PitchDeg, Is.EqualTo(-10.25f));
            AssertBody(back.Body);
        }

        /// <summary>A move whose wish or facing is not a number is refused by the reader (M1.5g): a NaN passes every comparison after it.</summary>
        [Test]
        public void AWishOrAFacingThatIsNotANumberIsARefusal()
        {
            foreach ((float wishEast, float wishNorth, float yaw, float pitch, string what) in new[]
            {
                (float.NaN, 0f, 0f, 0f, "a wish east of NaN"),
                (0f, float.PositiveInfinity, 0f, 0f, "a wish north of infinity"),
                (0f, 0f, float.NaN, 0f, "a yaw of NaN"),
                (0f, 0f, 0f, float.NegativeInfinity, "a pitch of minus infinity"),
            })
            {
                PacketWriter w = new PacketWriter();
                w.WriteByte((byte)MessageKind.PlayerMove);
                w.WriteUInt32(1);
                w.WriteSingle(wishEast);
                w.WriteSingle(wishNorth);
                w.WriteByte(0);
                w.WriteSingle(yaw);
                w.WriteSingle(pitch);
                BodyWire.WriteBody(w, Body());
                Assert.Throws<ProtocolException>(() => PlayerMoveMessage.Read(ReaderOver(w)), what);
            }
        }

        [Test]
        public void PlayerStateRoundTrips()
        {
            PlayerStateMessage m;
            m.SessionId = 9;
            m.Sequence = 4000000000;
            m.ServerTick = 123456789012;
            m.YawDeg = 1f;
            m.PitchDeg = 2f;
            m.Body = Body();
            PacketWriter w = new PacketWriter();
            m.Write(w);
            PacketReader r = ReaderOver(w);
            PlayerStateMessage back = PlayerStateMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.SessionId, Is.EqualTo(9));
            Assert.That(back.Sequence, Is.EqualTo(4000000000));
            Assert.That(back.ServerTick, Is.EqualTo(123456789012));
            AssertBody(back.Body);
        }

        [Test]
        public void CorrectionRoundTripsWithItsReason()
        {
            CorrectionMessage m;
            m.Sequence = 5;
            m.ServerTick = 60;
            m.Body = Body();
            m.Reason = "speed 300.00 m/s exceeds the ceiling 7.05";
            PacketWriter w = new PacketWriter();
            m.Write(w);
            PacketReader r = ReaderOver(w);
            CorrectionMessage back = CorrectionMessage.Read(r);
            r.ExpectEnd();
            Assert.That(back.Sequence, Is.EqualTo(5));
            Assert.That(back.ServerTick, Is.EqualTo(60));
            Assert.That(back.Reason, Is.EqualTo(m.Reason));
            AssertBody(back.Body);
        }

        [Test]
        public void FlagsAndButtonsAreOneByteEach()
        {
            MoverState s = Body();
            Assert.That(BodyWire.FlagsOf(s), Is.EqualTo(BodyWire.FlagGrounded | BodyWire.FlagWading | BodyWire.FlagCrouching));
            MoverState back = default;
            BodyWire.ApplyFlags(BodyWire.FlagWading, ref back);
            Assert.That(back.Wading, Is.True);
            Assert.That(back.Grounded, Is.False);
            Assert.That(back.Stance, Is.EqualTo(Stance.Standing));
            MoverInput i = MoverInput.None;
            i.Crouch = true;
            Assert.That(BodyWire.ButtonsOf(i), Is.EqualTo(BodyWire.ButtonCrouch));
        }

        private static void AssertBody(MoverState b)
        {
            Assert.That(b.East, Is.EqualTo(-2410.125), "positions travel as doubles, exactly");
            Assert.That(b.Up, Is.EqualTo(3.5));
            Assert.That(b.North, Is.EqualTo(-2113.75));
            Assert.That(b.VelEast, Is.EqualTo(1.25), "velocities travel as floats; these are exact in float");
            Assert.That(b.VelUp, Is.EqualTo(-0.5));
            Assert.That(b.VelNorth, Is.EqualTo(2.0));
            Assert.That(b.Grounded, Is.True);
            Assert.That(b.Wading, Is.True);
            Assert.That(b.Stance, Is.EqualTo(Stance.Crouching));
        }
    }
}
