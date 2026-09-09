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
        /// <summary>Client → server: send me these layer tiles, unless their checksum is the one I hold.</summary>
        TileRequest = 9,
        /// <summary>Server → client: a tile is coming in this many chunks, or it is the one you hold.</summary>
        TileHeader = 10,
        /// <summary>Server → client: one chunk of a tile's bytes.</summary>
        TileChunk = 11,
        /// <summary>Server → client: every player state that existed at your Welcome has been sent.</summary>
        SnapshotEnd = 12,
        /// <summary>Server → client: a session is gone; drop its mirror.</summary>
        PlayerLeft = 13,
        /// <summary>Server → client: an entity has entered your interest (or existed at your Welcome), in full.</summary>
        EntitySpawn = 14,
        /// <summary>Server → client: the fields of an entity in your interest that changed.</summary>
        EntityState = 15,
        /// <summary>Server → client: an entity you were shown died or left your interest.</summary>
        EntityGone = 16,
    }

    /// <summary>One tile the client wants, with the checksum of the copy it already holds (zero for none).</summary>
    public struct TileWant
    {
        public int Ix;
        public int Iz;
        public uint KnownCrc32;
    }

    /// <summary>Client → server, reliable: the tiles around the player, sent once the Welcome says where that is.</summary>
    public struct TileRequestMessage
    {
        public TileWant[] Wants;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.TileRequest);
            int count = Wants == null ? 0 : Wants.Length;
            if (count > 64) throw new ProtocolException("a tile request names at most 64 tiles, not " + count);
            w.WriteByte((byte)count);
            for (int i = 0; i < count; i++)
            {
                w.WriteInt32(Wants[i].Ix);
                w.WriteInt32(Wants[i].Iz);
                w.WriteUInt32(Wants[i].KnownCrc32);
            }
        }

        public static TileRequestMessage Read(PacketReader r)
        {
            TileRequestMessage m;
            int count = r.ReadByte();
            m.Wants = new TileWant[count];
            for (int i = 0; i < count; i++)
            {
                m.Wants[i].Ix = r.ReadInt32();
                m.Wants[i].Iz = r.ReadInt32();
                m.Wants[i].KnownCrc32 = r.ReadUInt32();
            }
            return m;
        }
    }

    /// <summary>
    /// Server → client, reliable, before a tile's chunks. <see cref="ByteLength"/> zero with a checksum means the
    /// client's copy is current and no chunks follow; <see cref="Posts"/> zero means the server has no ground to
    /// send (a world without region data), which the client logs and does not wait for.
    /// </summary>
    public struct TileHeaderMessage
    {
        public int Ix;
        public int Iz;
        public ushort Posts;
        public float CellM;
        public double OriginEast;
        public double OriginNorth;
        public int ByteLength;
        public uint Crc32;
        public ushort ChunkCount;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.TileHeader);
            w.WriteInt32(Ix);
            w.WriteInt32(Iz);
            w.WriteUInt16(Posts);
            w.WriteSingle(CellM);
            w.WriteDouble(OriginEast);
            w.WriteDouble(OriginNorth);
            w.WriteInt32(ByteLength);
            w.WriteUInt32(Crc32);
            w.WriteUInt16(ChunkCount);
        }

        public static TileHeaderMessage Read(PacketReader r)
        {
            TileHeaderMessage m;
            m.Ix = r.ReadInt32();
            m.Iz = r.ReadInt32();
            m.Posts = r.ReadUInt16();
            m.CellM = r.ReadSingle();
            m.OriginEast = r.ReadDouble();
            m.OriginNorth = r.ReadDouble();
            m.ByteLength = r.ReadInt32();
            m.Crc32 = r.ReadUInt32();
            m.ChunkCount = r.ReadUInt16();
            return m;
        }
    }

    /// <summary>Server → client, reliable and ordered: one chunk of a tile, in order after its header.</summary>
    public struct TileChunkMessage
    {
        public int Ix;
        public int Iz;
        public ushort Index;
        public byte[] Bytes;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.TileChunk);
            w.WriteInt32(Ix);
            w.WriteInt32(Iz);
            w.WriteUInt16(Index);
            w.WriteBytes(Bytes ?? System.Array.Empty<byte>());
        }

        public static TileChunkMessage Read(PacketReader r)
        {
            TileChunkMessage m;
            m.Ix = r.ReadInt32();
            m.Iz = r.ReadInt32();
            m.Index = r.ReadUInt16();
            m.Bytes = r.ReadBytes();
            return m;
        }
    }

    /// <summary>Server → client, reliable: the snapshot that followed the Welcome is complete (it may have been empty).</summary>
    public struct SnapshotEndMessage
    {
        public long ServerTick;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.SnapshotEnd);
            w.WriteInt64(ServerTick);
        }

        public static SnapshotEndMessage Read(PacketReader r)
        {
            SnapshotEndMessage m;
            m.ServerTick = r.ReadInt64();
            return m;
        }
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
        /// <summary>The region's side in metres, so the client lays out the tile grid without knowing the region.</summary>
        public double ExtentM;
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
            w.WriteDouble(ExtentM);
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
            m.ExtentM = r.ReadDouble();
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

    /// <summary>Server → client, reliable: a session ended (a leave, a drop, or a rejoin that superseded it).</summary>
    public struct PlayerLeftMessage
    {
        public uint SessionId;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.PlayerLeft);
            w.WriteUInt32(SessionId);
        }

        public static PlayerLeftMessage Read(PacketReader r)
        {
            PlayerLeftMessage m;
            m.SessionId = r.ReadUInt32();
            return m;
        }
    }

    /// <summary>An item's component on the wire: whether it rests, and its fall speed while it does not.</summary>
    public static class EntityWire
    {
        /// <summary>The component mask's one bit so far; the mask is a byte so seven more can follow without a version.</summary>
        public const byte ComponentItem = 1;

        public static void WriteItem(PacketWriter w, in ItemComponent item)
        {
            w.WriteBool(item.Resting);
            w.WriteSingle(item.FallSpeed);
        }

        public static ItemComponent ReadItem(PacketReader r)
        {
            ItemComponent item;
            item.Resting = r.ReadBool();
            item.FallSpeed = r.ReadSingle();
            return item;
        }
    }

    /// <summary>Server → client, reliable (protocol v4): an entity has entered your interest, or existed at your Welcome, in full.</summary>
    public struct EntitySpawnMessage
    {
        public ulong Id;
        public uint DefinitionId;
        public long ServerTick;
        public double East;
        public double Up;
        public double North;
        public float YawDeg;
        public bool HasItem;
        public ItemComponent Item;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.EntitySpawn);
            w.WriteUInt64(Id);
            w.WriteUInt32(DefinitionId);
            w.WriteInt64(ServerTick);
            w.WriteDouble(East);
            w.WriteDouble(Up);
            w.WriteDouble(North);
            w.WriteSingle(YawDeg);
            w.WriteByte(HasItem ? EntityWire.ComponentItem : (byte)0);
            if (HasItem) EntityWire.WriteItem(w, Item);
        }

        public static EntitySpawnMessage Read(PacketReader r)
        {
            EntitySpawnMessage m;
            m.Id = r.ReadUInt64();
            m.DefinitionId = r.ReadUInt32();
            m.ServerTick = r.ReadInt64();
            m.East = r.ReadDouble();
            m.Up = r.ReadDouble();
            m.North = r.ReadDouble();
            m.YawDeg = r.ReadSingle();
            byte components = r.ReadByte();
            m.HasItem = (components & EntityWire.ComponentItem) != 0;
            m.Item = m.HasItem ? EntityWire.ReadItem(r) : default;
            return m;
        }
    }

    /// <summary>
    /// Server → client, unreliable, reliable when it carries the item's rest (protocol v4): the fields of an entity
    /// in your interest that changed since you were last sent it, named by the mask.
    /// </summary>
    public struct EntityStateMessage
    {
        public ulong Id;
        public long ServerTick;
        public EntityFields Fields;
        public double East;
        public double Up;
        public double North;
        public float YawDeg;
        public ItemComponent Item;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.EntityState);
            w.WriteUInt64(Id);
            w.WriteInt64(ServerTick);
            w.WriteByte((byte)Fields);
            if ((Fields & EntityFields.Position) != 0)
            {
                w.WriteDouble(East);
                w.WriteDouble(Up);
                w.WriteDouble(North);
            }
            if ((Fields & EntityFields.Yaw) != 0) w.WriteSingle(YawDeg);
            if ((Fields & EntityFields.Item) != 0) EntityWire.WriteItem(w, Item);
        }

        public static EntityStateMessage Read(PacketReader r)
        {
            EntityStateMessage m = default;
            m.Id = r.ReadUInt64();
            m.ServerTick = r.ReadInt64();
            m.Fields = (EntityFields)r.ReadByte();
            if ((m.Fields & ~EntityFields.All) != 0) throw new ProtocolException("entity state names fields this build does not know: " + (byte)m.Fields);
            if ((m.Fields & EntityFields.Position) != 0)
            {
                m.East = r.ReadDouble();
                m.Up = r.ReadDouble();
                m.North = r.ReadDouble();
            }
            if ((m.Fields & EntityFields.Yaw) != 0) m.YawDeg = r.ReadSingle();
            if ((m.Fields & EntityFields.Item) != 0) m.Item = EntityWire.ReadItem(r);
            return m;
        }
    }

    /// <summary>Server → client, reliable (protocol v4): an entity you were shown is gone, because it died or left your interest.</summary>
    public struct EntityGoneMessage
    {
        public const byte Died = 1;
        public const byte Left = 2;

        public ulong Id;
        public byte Reason;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.EntityGone);
            w.WriteUInt64(Id);
            w.WriteByte(Reason);
        }

        public static EntityGoneMessage Read(PacketReader r)
        {
            EntityGoneMessage m;
            m.Id = r.ReadUInt64();
            m.Reason = r.ReadByte();
            return m;
        }
    }
}
