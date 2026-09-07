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
        /// <summary>Server → client: you are in; here is the world's identity and time.</summary>
        Welcome = 2,
        /// <summary>Server → client: you are not in, and why. The connection closes after it.</summary>
        Refused = 3,
        /// <summary>Client → server: echo this back with your tick.</summary>
        Ping = 4,
        /// <summary>Server → client: the echo.</summary>
        Pong = 5,
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

    /// <summary>Server → client. Identity and time of the world just joined.</summary>
    public struct WelcomeMessage
    {
        public uint SessionId;
        public ulong Seed;
        public string RegionId;
        public double TotalHours;
        public long Tick;
        public byte TickRate;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Welcome);
            w.WriteUInt32(SessionId);
            w.WriteUInt64(Seed);
            w.WriteString(RegionId);
            w.WriteDouble(TotalHours);
            w.WriteInt64(Tick);
            w.WriteByte(TickRate);
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
