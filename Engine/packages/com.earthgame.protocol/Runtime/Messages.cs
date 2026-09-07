using EarthGame.Engine;

namespace EarthGame.Protocol
{
    /// <summary>
    /// The first byte of every message. Values are wire-visible and never renumbered; retired kinds keep their
    /// number (Rust's BUTTON enum has gaps for exactly this reason).
    /// </summary>
    public enum MessageKind : byte
    {
        None = 0,
        /// <summary>Client → server: who I am and which protocol I speak.</summary>
        Hello = 1,
        /// <summary>Server → client: you are in; here is the world's identity, time and your spawn point.</summary>
        Welcome = 2,
        /// <summary>Server → client: you are not in, and why. The connection closes after it.</summary>
        Refused = 3,
        /// <summary>Client → server: echo this back with your tick.</summary>
        Ping = 4,
        /// <summary>Server → client: the echo.</summary>
        Pong = 5,
        /// <summary>Client → server: the input I applied and the body state it produced.</summary>
        PlayerMove = 6,
        /// <summary>Server → client: another player's body, as the server accepted it.</summary>
        PlayerState = 7,
        /// <summary>Server → client: your reported move was impossible; here is where you are.</summary>
        Correction = 8,
    }

    /// <summary>Client → server. The first message on a connection; anything else first is a refusal.</summary>
    public struct HelloMessage
    {
        public ushort ProtocolVersion;
        public string PlayerName;
        public string Password;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Hello);
            w.WriteUInt16(ProtocolVersion);
            w.WriteString(PlayerName);
            w.WriteString(Password);
        }

        public static HelloMessage Read(PacketReader r)
        {
            HelloMessage m;
            m.ProtocolVersion = r.ReadUInt16();
            m.PlayerName = r.ReadString();
            m.Password = r.ReadString();
            return m;
        }
    }

    /// <summary>Server → client. Identity and time of the world just joined, and where the player's feet start.</summary>
    public struct WelcomeMessage
    {
        public uint SessionId;
        public ulong Seed;
        public string RegionId;
        public double TotalHours;
        public long Tick;
        public byte TickRate;
        public double SpawnEast;
        public double SpawnUp;
        public double SpawnNorth;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Welcome);
            w.WriteUInt32(SessionId);
            w.WriteUInt64(Seed);
            w.WriteString(RegionId);
            w.WriteDouble(TotalHours);
            w.WriteInt64(Tick);
            w.WriteByte(TickRate);
            w.WriteDouble(SpawnEast);
            w.WriteDouble(SpawnUp);
            w.WriteDouble(SpawnNorth);
        }

        public static WelcomeMessage Read(PacketReader r)
        {
            WelcomeMessage m;
            m.SessionId = r.ReadUInt32();
            m.Seed = r.ReadUInt64();
            m.RegionId = r.ReadString();
            m.TotalHours = r.ReadDouble();
            m.Tick = r.ReadInt64();
            m.TickRate = r.ReadByte();
            m.SpawnEast = r.ReadDouble();
            m.SpawnUp = r.ReadDouble();
            m.SpawnNorth = r.ReadDouble();
            return m;
        }
    }

    /// <summary>Server → client. Why the server said no; stated, never inferred from a silent close.</summary>
    public struct RefusedMessage
    {
        public string Reason;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Refused);
            w.WriteString(Reason);
        }

        public static RefusedMessage Read(PacketReader r)
        {
            RefusedMessage m;
            m.Reason = r.ReadString();
            return m;
        }
    }

    /// <summary>Client → server. Carries the client's own send time so it can compute the round trip.</summary>
    public struct PingMessage
    {
        public long ClientTimeMs;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Ping);
            w.WriteInt64(ClientTimeMs);
        }

        public static PingMessage Read(PacketReader r)
        {
            PingMessage m;
            m.ClientTimeMs = r.ReadInt64();
            return m;
        }
    }

    /// <summary>Server → client. The ping echoed, with the server's tick so the client can see time move.</summary>
    public struct PongMessage
    {
        public long ClientTimeMs;
        public long ServerTick;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Pong);
            w.WriteInt64(ClientTimeMs);
            w.WriteInt64(ServerTick);
        }

        public static PongMessage Read(PacketReader r)
        {
            PongMessage m;
            m.ClientTimeMs = r.ReadInt64();
            m.ServerTick = r.ReadInt64();
            return m;
        }
    }

    /// <summary>The mover's flags as one wire byte, and the buttons as another.</summary>
    public static class BodyWire
    {
        public const byte FlagGrounded = 1;
        public const byte FlagWading = 2;
        public const byte FlagCrouching = 4;
        public const byte ButtonJump = 1;
        public const byte ButtonSprint = 2;
        public const byte ButtonCrouch = 4;

        public static byte FlagsOf(in MoverState s)
            => (byte)((s.Grounded ? FlagGrounded : 0) | (s.Wading ? FlagWading : 0) | (s.Stance == Stance.Crouching ? FlagCrouching : 0));

        public static void ApplyFlags(byte flags, ref MoverState s)
        {
            s.Grounded = (flags & FlagGrounded) != 0;
            s.Wading = (flags & FlagWading) != 0;
            s.Stance = (flags & FlagCrouching) != 0 ? Stance.Crouching : Stance.Standing;
        }

        public static byte ButtonsOf(in MoverInput i)
            => (byte)((i.Jump ? ButtonJump : 0) | (i.Sprint ? ButtonSprint : 0) | (i.Crouch ? ButtonCrouch : 0));

        public static void ApplyButtons(byte buttons, ref MoverInput i)
        {
            i.Jump = (buttons & ButtonJump) != 0;
            i.Sprint = (buttons & ButtonSprint) != 0;
            i.Crouch = (buttons & ButtonCrouch) != 0;
        }

        /// <summary>The eleven numbers of a body, written as the wire carries them (positions double, velocities float).</summary>
        public static void WriteBody(PacketWriter w, in MoverState s)
        {
            w.WriteDouble(s.East);
            w.WriteDouble(s.Up);
            w.WriteDouble(s.North);
            w.WriteSingle((float)s.VelEast);
            w.WriteSingle((float)s.VelUp);
            w.WriteSingle((float)s.VelNorth);
            w.WriteByte(FlagsOf(s));
        }

        public static MoverState ReadBody(PacketReader r)
        {
            MoverState s = default;
            s.East = r.ReadDouble();
            s.Up = r.ReadDouble();
            s.North = r.ReadDouble();
            s.VelEast = r.ReadSingle();
            s.VelUp = r.ReadSingle();
            s.VelNorth = r.ReadSingle();
            ApplyFlags(r.ReadByte(), ref s);
            return s;
        }
    }

    /// <summary>
    /// Client → server, unreliable, at the server's tick rate: the input the client applied over the interval and
    /// the body state its mover produced. Movement is client-authoritative and server-validated (ARCHITECTURE §7).
    /// </summary>
    public struct PlayerMoveMessage
    {
        public uint Sequence;
        public MoverInput Input;
        public float YawDeg;
        public float PitchDeg;
        public MoverState Body;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.PlayerMove);
            w.WriteUInt32(Sequence);
            w.WriteSingle((float)Input.WishEast);
            w.WriteSingle((float)Input.WishNorth);
            w.WriteByte(BodyWire.ButtonsOf(Input));
            w.WriteSingle(YawDeg);
            w.WriteSingle(PitchDeg);
            BodyWire.WriteBody(w, Body);
        }

        public static PlayerMoveMessage Read(PacketReader r)
        {
            PlayerMoveMessage m;
            m.Sequence = r.ReadUInt32();
            m.Input = default;
            m.Input.WishEast = r.ReadSingle();
            m.Input.WishNorth = r.ReadSingle();
            BodyWire.ApplyButtons(r.ReadByte(), ref m.Input);
            m.YawDeg = r.ReadSingle();
            m.PitchDeg = r.ReadSingle();
            m.Body = BodyWire.ReadBody(r);
            return m;
        }
    }

    /// <summary>Server → client, unreliable: another player's body as last accepted, for the mirror to draw.</summary>
    public struct PlayerStateMessage
    {
        public uint SessionId;
        public uint Sequence;
        public long ServerTick;
        public float YawDeg;
        public float PitchDeg;
        public MoverState Body;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.PlayerState);
            w.WriteUInt32(SessionId);
            w.WriteUInt32(Sequence);
            w.WriteInt64(ServerTick);
            w.WriteSingle(YawDeg);
            w.WriteSingle(PitchDeg);
            BodyWire.WriteBody(w, Body);
        }

        public static PlayerStateMessage Read(PacketReader r)
        {
            PlayerStateMessage m;
            m.SessionId = r.ReadUInt32();
            m.Sequence = r.ReadUInt32();
            m.ServerTick = r.ReadInt64();
            m.YawDeg = r.ReadSingle();
            m.PitchDeg = r.ReadSingle();
            m.Body = BodyWire.ReadBody(r);
            return m;
        }
    }

    /// <summary>
    /// Server → client, reliable: the move named by <see cref="Sequence"/> was impossible for the reason stated;
    /// the body is where the server holds the player, which the client must adopt.
    /// </summary>
    public struct CorrectionMessage
    {
        public uint Sequence;
        public long ServerTick;
        public MoverState Body;
        public string Reason;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Correction);
            w.WriteUInt32(Sequence);
            w.WriteInt64(ServerTick);
            BodyWire.WriteBody(w, Body);
            w.WriteString(Reason);
        }

        public static CorrectionMessage Read(PacketReader r)
        {
            CorrectionMessage m;
            m.Sequence = r.ReadUInt32();
            m.ServerTick = r.ReadInt64();
            m.Body = BodyWire.ReadBody(r);
            m.Reason = r.ReadString();
            return m;
        }
    }

    /// <summary>Reads the kind byte off a received payload without consuming anything else.</summary>
    public static class MessageHeader
    {
        public static MessageKind PeekKind(byte[] payload, int offset, int count)
        {
            if (payload == null || count < 1 || offset < 0 || offset >= payload.Length) return MessageKind.None;
            return (MessageKind)payload[offset];
        }
    }
}
