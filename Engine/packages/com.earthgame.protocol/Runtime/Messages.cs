using System;
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
        /// <summary>Server → client: an entity you were shown died, left your interest, or was taken up.</summary>
        EntityGone = 16,
        /// <summary>Client → server: do this verb (M1.5a): pick a thing up, put the thing in hand down, or hold a place.</summary>
        Intent = 17,
        /// <summary>Server → client: what came of an intent, by its sequence.</summary>
        IntentResult = 18,
        /// <summary>Server → client: what you carry in each place, and which place is the hand.</summary>
        Carrying = 19,
        /// <summary>Server → client: what has been taken from the loose layer, cell by cell (M1.5b).</summary>
        LooseTaken = 20,
        /// <summary>Client → server, reliable: a developer's setting by name and number (M1.D), taken by a development server alone.</summary>
        DevSetting = 21,
        /// <summary>Server → its own client, reliable: the water in the founder's body (FP.1, protocol v13) and its core (FP.2, v14).</summary>
        FounderState = 22,
        /// <summary>Server → its own client, reliable: the founder died, what killed them and the numbers of it (FP.2, protocol v14).</summary>
        Died = 23,
        /// <summary>
        /// Both ways, reliable (M1.E, protocol v16): the client asks for developer mode on or off (F2, CANON ruling 39), and
        /// the server answers with what it granted and whether it refused.
        /// </summary>
        DeveloperMode = 24,
        /// <summary>Server → client (BF.2, protocol 18): a work's progress once a second, and its end, done or stopped, with the words.</summary>
        WorkState = 25,
    }

    /// <summary>One tile of one layer the client wants, with the checksum of the copy it already holds (zero for none).</summary>
    public struct TileWant
    {
        public int Ix;
        public int Iz;
        public TileLayer Layer;
        public uint KnownCrc32;
    }

    /// <summary>Client → server, reliable: the tiles around the player, sent once the Welcome says where that is.</summary>
    public struct TileRequestMessage
    {
        /// <summary>The most tiles one request names; the whole region's far layers go in as many requests as they need (M1.6d).</summary>
        public const int MostTiles = 64;

        public TileWant[] Wants;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.TileRequest);
            int count = Wants == null ? 0 : Wants.Length;
            if (count > MostTiles) throw new ProtocolException("a tile request names at most " + MostTiles + " tiles, not " + count);
            w.WriteByte((byte)count);
            for (int i = 0; i < count; i++)
            {
                w.WriteInt32(Wants[i].Ix);
                w.WriteInt32(Wants[i].Iz);
                w.WriteByte((byte)Wants[i].Layer);
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
                m.Wants[i].Layer = TileWire.Read(r);
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
        public TileLayer Layer;
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
            w.WriteByte((byte)Layer);
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
            m.Layer = TileWire.Read(r);
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
        public TileLayer Layer;
        public ushort Index;
        public byte[] Bytes;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.TileChunk);
            w.WriteInt32(Ix);
            w.WriteInt32(Iz);
            w.WriteByte((byte)Layer);
            w.WriteUInt16(Index);
            w.WriteBytes(Bytes ?? System.Array.Empty<byte>());
        }

        public static TileChunkMessage Read(PacketReader r)
        {
            TileChunkMessage m;
            m.Ix = r.ReadInt32();
            m.Iz = r.ReadInt32();
            m.Layer = TileWire.Read(r);
            m.Index = r.ReadUInt16();
            m.Bytes = r.ReadBytes();
            return m;
        }
    }

    /// <summary>The layer byte of a tile message, refused rather than guessed when this build does not know it.</summary>
    internal static class TileWire
    {
        internal static TileLayer Read(PacketReader r)
        {
            byte value = r.ReadByte();
            // Asked of the set itself, not of the last layer's number: the highest layer written here and the
            // set written there was one fact in two places, and adding the ground cover refused every join
            // (2026-09-10).
            if (!TileLayers.IsKnown(value)) throw new ProtocolException("tile layer " + value + " is not one this build knows");
            return (TileLayer)value;
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
        /// <summary>
        /// How far from its founder the server sends a client the other players and the entities, metres (protocol 9): a
        /// client counts what it and another player both hold by it.
        /// </summary>
        public double InterestRadiusM;

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
            w.WriteDouble(InterestRadiusM);
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
            m.InterestRadiusM = r.ReadDouble();
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
        /// <summary>
        /// The server's clock, hours since its epoch, when it answered (M1.D, protocol 10): a client's own clock runs from its
        /// Welcome and follows this when it slips, so a clock a developer has moved moves every client's sky.
        /// </summary>
        public double ServerTotalHours;
        /// <summary>How many times faster than the game's rate the server's clock runs (protocol 11): a client's runs at the same rate.</summary>
        public double ClockScale;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Pong);
            w.WriteInt64(ClientTimeMs);
            w.WriteInt64(ServerTick);
            w.WriteDouble(ServerTotalHours);
            w.WriteDouble(ClockScale);
        }

        public static PongMessage Read(PacketReader r)
        {
            PongMessage m;
            m.ClientTimeMs = r.ReadInt64();
            m.ServerTick = r.ReadInt64();
            m.ServerTotalHours = r.ReadDouble();
            m.ClockScale = r.ReadDouble();
            if (!BodyWire.Finite(m.ServerTotalHours) || !BodyWire.Finite(m.ClockScale) || m.ClockScale < 0.0)
                throw new ProtocolException("a pong's clock is not a number");
            return m;
        }
    }

    /// <summary>
    /// Client → server, reliable: a developer's setting, by the name <see cref="DevSettings"/> gives it and a number (M1.D,
    /// protocol 10). A server started for development applies one it knows and refuses one it does not; any other server
    /// refuses it and closes, as it does a malformed message. A setting with no name, or a number that is not one, is refused
    /// where it is read.
    /// </summary>
    public struct DevSettingMessage
    {
        public string Name;
        public double Value;

        public void Write(PacketWriter w)
        {
            if (string.IsNullOrEmpty(Name)) throw new ProtocolException("a developer's setting has a name");
            w.WriteByte((byte)MessageKind.DevSetting);
            w.WriteString(Name);
            w.WriteDouble(Value);
        }

        public static DevSettingMessage Read(PacketReader r)
        {
            DevSettingMessage m;
            m.Name = r.ReadString();
            m.Value = r.ReadDouble();
            if (string.IsNullOrEmpty(m.Name)) throw new ProtocolException("a developer's setting has a name");
            if (!BodyWire.Finite(m.Value)) throw new ProtocolException("a developer's setting of " + m.Name + " is not a number");
            return m;
        }
    }

    /// <summary>
    /// Developer mode, asked and answered (M1.E, protocol v16; CANON ruling 39, "make it so that dev mode is toggleable in
    /// game, not a restart with the devmode flag"). Client to server it carries what the player asks for; server to client
    /// what the session now has, and whether the asking was refused — a server started without development refuses, and
    /// keeps the player, because F2 is a key every game answers. One byte of flags, so a byte beyond the two is refused
    /// where it is read.
    /// </summary>
    public struct DeveloperModeMessage
    {
        public bool On;
        public bool Refused;

        private const byte OnBit = 1, RefusedBit = 2;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.DeveloperMode);
            w.WriteByte((byte)((On ? OnBit : 0) | (Refused ? RefusedBit : 0)));
        }

        public static DeveloperModeMessage Read(PacketReader r)
        {
            byte flags = r.ReadByte();
            if ((flags & ~(OnBit | RefusedBit)) != 0) throw new ProtocolException("developer mode's flags carry a bit it does not have: " + flags);
            DeveloperModeMessage m;
            m.On = (flags & OnBit) != 0;
            m.Refused = (flags & RefusedBit) != 0;
            return m;
        }
    }

    /// <summary>
    /// Server → its own client, reliable (protocol v13, FP.1): the water in the founder's body against normal, 1 full.
    /// Sent once a second and at every change that matters (a drink, a developer's setting, the join). The client takes
    /// the word and the work capacity from it by the engine's own tables (<see cref="Hydration"/>), so the wire carries
    /// the one number and no reader can hold a different threshold.
    /// </summary>
    public struct FounderStateMessage
    {
        public double Water01;
        /// <summary>The core's temperature, °C (FP.2, protocol v14): the client takes the cold's word from it by the engine's table.</summary>
        public double CoreC;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.FounderState);
            w.WriteDouble(Water01);
            w.WriteDouble(CoreC);
        }

        public static FounderStateMessage Read(PacketReader r)
        {
            FounderStateMessage m;
            m.Water01 = r.ReadDouble();
            m.CoreC = r.ReadDouble();
            if (!BodyWire.Finite(m.Water01) || m.Water01 < 0.0 || m.Water01 > 1.0)
                throw new ProtocolException("a founder's water of " + m.Water01 + " is no fraction of a body");
            if (!BodyWire.Finite(m.CoreC) || m.CoreC < 20.0 || m.CoreC > 41.0)
                throw new ProtocolException("a founder's core of " + m.CoreC + " is no temperature a body has");
            return m;
        }
    }

    /// <summary>
    /// Server → its own client, reliable (protocol v14, FP.2): the founder died. What killed them, when by the local clock,
    /// the air and the wind, the body's last balance, the core and the water, and where they fell: the numbers the one
    /// sentence (<see cref="Death.Explain"/>) is made of, so the screen and the log tell the same story.
    /// </summary>
    public struct DiedMessage
    {
        public Death Death;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Died);
            w.WriteByte((byte)Death.Cause);
            w.WriteDouble(Death.LocalHour);
            w.WriteDouble(Death.AirC);
            w.WriteDouble(Death.WindMs);
            w.WriteDouble(Death.LossW);
            w.WriteDouble(Death.ProductionW);
            w.WriteDouble(Death.CoreC);
            w.WriteDouble(Death.WaterLoss);
            w.WriteDouble(Death.East);
            w.WriteDouble(Death.North);
        }

        public static DiedMessage Read(PacketReader r)
        {
            byte cause = r.ReadByte();
            if (cause != (byte)CauseOfDeath.Cold && cause != (byte)CauseOfDeath.Thirst)
                throw new ProtocolException("a death of cause " + cause + " is not one this build knows");
            double hour = r.ReadDouble(), air = r.ReadDouble(), wind = r.ReadDouble(), loss = r.ReadDouble(), production = r.ReadDouble();
            double core = r.ReadDouble(), water = r.ReadDouble(), east = r.ReadDouble(), north = r.ReadDouble();
            if (!BodyWire.Finite(hour) || !BodyWire.Finite(air) || !BodyWire.Finite(wind) || !BodyWire.Finite(loss) || !BodyWire.Finite(production)
                || !BodyWire.Finite(core) || !BodyWire.Finite(water) || !BodyWire.Finite(east) || !BodyWire.Finite(north))
                throw new ProtocolException("a death names a number that is not one");
            if (hour < 0.0 || hour >= 24.0) throw new ProtocolException("a death at hour " + hour + " is at no hour of a day");
            DiedMessage m;
            m.Death = new Death((CauseOfDeath)cause, hour, air, wind, loss, production, core, water, east, north);
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

        /// <summary>
        /// Whether a number read off the wire is one (M1.5g, 2026-09-14): a NaN or an infinity passes every comparison a
        /// server makes, so a put-down at height NaN cleared the reach and the region checks and re-entered the world as a
        /// thing that never lands, re-sent to everyone in reach every tick, and a facing of NaN was kept and passed on.
        /// The reader refuses such a message, as it refuses any other it cannot make sense of.
        /// </summary>
        public static bool Finite(double v) => !(double.IsNaN(v) || double.IsInfinity(v));

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
            if (!BodyWire.Finite(m.Input.WishEast) || !BodyWire.Finite(m.Input.WishNorth))
                throw new ProtocolException("a move's wish is not a number");
            if (!BodyWire.Finite(m.YawDeg) || !BodyWire.Finite(m.PitchDeg))
                throw new ProtocolException("a move's facing is not a number");
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

    /// <summary>The components of an entity on the wire: an item's rest and fall, and (protocol 9) an animal's pose.</summary>
    public static class EntityWire
    {
        /// <summary>The component mask's item bit.</summary>
        public const byte ComponentItem = 1;

        /// <summary>The component mask's animal bit (M1.7a, protocol 9). A bit this build does not know is refused, never skipped, since its bytes would be read as something else.</summary>
        public const byte ComponentAnimal = 2;

        /// <summary>Every component bit this build reads.</summary>
        public const byte ComponentsKnown = ComponentItem | ComponentAnimal;

        public static void WriteAnimal(PacketWriter w, in AnimalComponent animal)
        {
            w.WriteByte(animal.Pose);
        }

        public static AnimalComponent ReadAnimal(PacketReader r)
        {
            AnimalComponent animal;
            animal.Pose = r.ReadByte();
            return animal;
        }

        /// <summary>An item's rest and fall, then what it has of its own (BF.1, protocol 17): <see cref="ThingWire"/>'s one layout.</summary>
        public static void WriteItem(PacketWriter w, in ItemComponent item)
        {
            w.WriteBool(item.Resting);
            w.WriteSingle(item.FallSpeed);
            ThingWire.Write(w, item.State);
        }

        public static ItemComponent ReadItem(PacketReader r)
        {
            ItemComponent item = ReadRestAndFall(r);
            item.State = ThingWire.Read(r);
            return item;
        }

        /// <summary>An item's rest and fall alone: the whole of an item before protocol 15, which region files 1 and 2 still carry.</summary>
        public static ItemComponent ReadRestAndFall(PacketReader r)
        {
            ItemComponent item = default;
            item.Resting = r.ReadBool();
            item.FallSpeed = r.ReadSingle();
            return item;
        }

        /// <summary>An item as protocol 15 and region file 3 wrote it (FP.3): its rest and fall, then the four fields of a struck stone.</summary>
        public static ItemComponent ReadItemOfStruckStone(PacketReader r)
        {
            ItemComponent item = ReadRestAndFall(r);
            item.State = ThingWire.ReadStruckStone(r);
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
        /// <summary>Whether it is an animal, and its pose (protocol 9).</summary>
        public bool HasAnimal;
        public AnimalComponent Animal;

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
            w.WriteByte((byte)((HasItem ? EntityWire.ComponentItem : 0) | (HasAnimal ? EntityWire.ComponentAnimal : 0)));
            if (HasItem) EntityWire.WriteItem(w, Item);
            if (HasAnimal) EntityWire.WriteAnimal(w, Animal);
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
            if ((components & ~EntityWire.ComponentsKnown) != 0) throw new ProtocolException("entity spawn names components this build does not know: " + components);
            m.HasItem = (components & EntityWire.ComponentItem) != 0;
            m.Item = m.HasItem ? EntityWire.ReadItem(r) : default;
            m.HasAnimal = (components & EntityWire.ComponentAnimal) != 0;
            m.Animal = m.HasAnimal ? EntityWire.ReadAnimal(r) : default;
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
        public AnimalComponent Animal;

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
            if ((Fields & EntityFields.Pose) != 0) EntityWire.WriteAnimal(w, Animal);
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
            if ((m.Fields & EntityFields.Pose) != 0) m.Animal = EntityWire.ReadAnimal(r);
            return m;
        }
    }

    /// <summary>
    /// Server → client, reliable (protocol v4): an entity you were shown is gone, because it died, left your interest,
    /// or a founder took it up (protocol v6).
    /// </summary>
    public struct EntityGoneMessage
    {
        public const byte Died = 1;
        public const byte Left = 2;
        public const byte TakenUp = 3;

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

    /// <summary>
    /// Client → server, reliable (protocol v6): a verb and its target. A pick-up names an entity (target kind 1: u64 id)
    /// or, since protocol v7 (M1.5b), a thing lying in the loose layer by its place (target kind 2: u16 row, u16 column,
    /// u8 kind, 2 a stick or 3 a cobble, u8 index); a put-down names the point on the ground the founder is looking at;
    /// a hold names the place, 0 for an empty hand. A knap (protocol v15, FP.3) names the core as a pick-up names its
    /// target, or as a place of the hands (target kind 3: u8 place), and then the wind-up as a byte, 0 a tap and 255 a
    /// full swing; the hammer is whatever is in the hand. The server answers with an <see cref="IntentResultMessage"/> of
    /// the same sequence.
    /// </summary>
    public struct IntentMessage
    {
        public const byte TargetEntity = 1;
        /// <summary>A thing lying in the loose layer, named by its place (protocol v7, M1.5b).</summary>
        public const byte TargetLying = 2;
        /// <summary>A thing held in one of the hands' places (protocol v15, FP.3): a core struck while held.</summary>
        public const byte TargetPlace = 3;

        public uint Sequence;
        public Verb Verb;
        /// <summary>What a pick-up or a knap names: <see cref="TargetEntity"/>, <see cref="TargetLying"/> or, for a knap, <see cref="TargetPlace"/>.</summary>
        public byte Target;
        public ulong EntityId;
        public LyingThing Lying;
        public double East;
        public double Up;
        public double North;
        /// <summary>A hold's place, 0 for an empty hand; a knap's when its target is a place of the hands.</summary>
        public byte Place;
        /// <summary>How far a knap's swing was wound up (FP.3): 0 a tap, 255 the arm's full swing.</summary>
        public byte WindUp;
        /// <summary>The kind of work a work intent begins (BF.2).</summary>
        public WorkKind Kind;

        /// <summary>The wind-up as the physics takes it, 0 to 1.</summary>
        public double WindUp01 => WindUp / 255.0;

        /// <summary>The byte a wind-up travels as: a fraction of a full swing, held to 0..1 and rounded to the nearest of 255 steps.</summary>
        public static byte WindUpOf(double windUp01) => (byte)Math.Round(SimMath.Clamp01(windUp01) * 255.0);

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Intent);
            w.WriteUInt32(Sequence);
            w.WriteByte((byte)Verb);
            switch (Verb)
            {
                case Verb.PickUp:
                    WriteTarget(w, "a pick-up", allowPlace: false);
                    break;
                case Verb.PutDown:
                case Verb.Drink:
                    w.WriteDouble(East);
                    w.WriteDouble(Up);
                    w.WriteDouble(North);
                    break;
                case Verb.Hold:
                    w.WriteByte(Place);
                    break;
                case Verb.Knap:
                    WriteTarget(w, "a knap", allowPlace: true);
                    w.WriteByte(WindUp);
                    break;
                case Verb.Work:
                    WriteTarget(w, "a work", allowPlace: true);
                    w.WriteByte((byte)Kind);
                    break;
                case Verb.StopWork:
                    break;
                default:
                    throw new ProtocolException("verb " + (byte)Verb + " has no layout");
            }
        }

        private void WriteTarget(PacketWriter w, string what, bool allowPlace)
        {
            w.WriteByte(Target);
            if (Target == TargetEntity) w.WriteUInt64(EntityId);
            else if (Target == TargetLying)
            {
                if (Lying.Row < 0 || Lying.Row > ushort.MaxValue || Lying.Col < 0 || Lying.Col > ushort.MaxValue || Lying.Index < 0 || Lying.Index > byte.MaxValue)
                    throw new ProtocolException("the " + Lying + " does not fit the wire");
                w.WriteUInt16((ushort)Lying.Row);
                w.WriteUInt16((ushort)Lying.Col);
                w.WriteByte((byte)Lying.Kind);
                w.WriteByte((byte)Lying.Index);
            }
            else if (Target == TargetPlace && allowPlace) w.WriteByte(Place);
            else throw new ProtocolException(what + " names a target of kind " + Target + ", which has no layout");
        }

        public static IntentMessage Read(PacketReader r)
        {
            IntentMessage m = default;
            m.Sequence = r.ReadUInt32();
            m.Verb = (Verb)r.ReadByte();
            switch (m.Verb)
            {
                case Verb.PickUp:
                    m.ReadTarget(r, "a pick-up", allowPlace: false);
                    break;
                case Verb.PutDown:
                case Verb.Drink:
                    m.East = r.ReadDouble();
                    m.Up = r.ReadDouble();
                    m.North = r.ReadDouble();
                    if (!BodyWire.Finite(m.East) || !BodyWire.Finite(m.Up) || !BodyWire.Finite(m.North))
                        throw new ProtocolException("a put-down or a drink names a point that is not a number");
                    break;
                case Verb.Hold:
                    m.Place = r.ReadByte();
                    break;
                case Verb.Knap:
                    m.ReadTarget(r, "a knap", allowPlace: true);
                    m.WindUp = r.ReadByte();
                    break;
                case Verb.Work:
                    m.ReadTarget(r, "a work", allowPlace: true);
                    m.Kind = (WorkKind)r.ReadByte();
                    if (m.Kind == WorkKind.None || (byte)m.Kind > (byte)WorkKind.Twist) throw new ProtocolException("a work of kind " + (byte)m.Kind + " is not one this build knows");
                    break;
                case Verb.StopWork:
                    break;
                default:
                    throw new ProtocolException("verb " + (byte)m.Verb + " is not one this build knows");
            }
            return m;
        }

        private void ReadTarget(PacketReader r, string what, bool allowPlace)
        {
            Target = r.ReadByte();
            if (Target == TargetEntity) EntityId = r.ReadUInt64();
            else if (Target == TargetLying)
            {
                int row = r.ReadUInt16();
                int col = r.ReadUInt16();
                byte kind = r.ReadByte();
                int index = r.ReadByte();
                if (kind != (byte)StandLayout.Kind.Stick && kind != (byte)StandLayout.Kind.Cobble)
                    throw new ProtocolException(what + " names a lying thing of kind " + kind + ", neither a stick nor a cobble");
                Lying = new LyingThing(row, col, (StandLayout.Kind)kind, index);
            }
            else if (Target == TargetPlace && allowPlace) Place = r.ReadByte();
            else throw new ProtocolException(what + " names a target of kind " + Target + ", which this build does not know");
        }
    }

    /// <summary>
    /// Server → client, reliable (protocol v6): what came of the intent with this sequence, and since protocol v15 (FP.3)
    /// the words for it when the world has some: a blow on stone is answered in the words the physics gives
    /// (<see cref="KnapResult.Note"/>), made in one place and carried here so the screen says what the server did; empty
    /// for every other verb.
    /// </summary>
    public struct IntentResultMessage
    {
        public uint Sequence;
        public VerbOutcome Outcome;
        public string Note;
        /// <summary>A started work's seconds at full capacity (BF.2, protocol 18); zero for every other answer.</summary>
        public float Seconds;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.IntentResult);
            w.WriteUInt32(Sequence);
            w.WriteByte((byte)Outcome);
            w.WriteString(Note ?? string.Empty);
            w.WriteSingle(Seconds);
        }

        public static IntentResultMessage Read(PacketReader r)
        {
            IntentResultMessage m;
            m.Sequence = r.ReadUInt32();
            m.Outcome = (VerbOutcome)r.ReadByte();
            if ((byte)m.Outcome > (byte)VerbOutcome.WontWork) throw new ProtocolException("intent outcome " + (byte)m.Outcome + " is not one this build knows");
            m.Note = r.ReadString();
            m.Seconds = r.ReadSingle();
            if (!BodyWire.Finite(m.Seconds) || m.Seconds < 0f) throw new ProtocolException("a work of " + m.Seconds + " seconds is no work");
            return m;
        }
    }

    /// <summary>
    /// Server → client, reliable (BF.2, protocol 18): how a founder's own work is going — its kind, its progress, the seconds
    /// left at the body's present capacity — once a second while it runs, and once more when it ends, done with the words for
    /// what it made, or stopped with the words for why.
    /// </summary>
    public struct WorkStateMessage
    {
        public const byte Running = 0;
        public const byte Done = 1;
        public const byte Stopped = 2;

        public WorkKind Kind;
        public float Progress01;
        public float SecondsLeft;
        public byte Ended;
        public string Note;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.WorkState);
            w.WriteByte((byte)Kind);
            w.WriteByte((byte)Math.Round(SimMath.Clamp01(Progress01) * 255.0));
            w.WriteSingle(SecondsLeft);
            w.WriteByte(Ended);
            w.WriteString(Note ?? string.Empty);
        }

        public static WorkStateMessage Read(PacketReader r)
        {
            WorkStateMessage m;
            m.Kind = (WorkKind)r.ReadByte();
            if (m.Kind == WorkKind.None || (byte)m.Kind > (byte)WorkKind.Twist) throw new ProtocolException("a work of kind " + (byte)m.Kind + " is not one this build knows");
            m.Progress01 = r.ReadByte() / 255f;
            m.SecondsLeft = r.ReadSingle();
            if (!BodyWire.Finite(m.SecondsLeft) || m.SecondsLeft < 0f) throw new ProtocolException("a work with " + m.SecondsLeft + " seconds left is no work");
            m.Ended = r.ReadByte();
            if (m.Ended > Stopped) throw new ProtocolException("a work's ending " + m.Ended + " is not one this build knows");
            m.Note = r.ReadString();
            return m;
        }
    }

    /// <summary>
    /// Server → client, reliable (protocol v6): what you carry, place by place, and which place is the hand; sent at the
    /// join, before the snapshot's end, and after every verb that changed it. A thing's spawn tick stays with the server.
    /// Since protocol 17 (BF.1) each thing's own state rides with it, so the hand knows a hammer's mass and a stick's tree.
    /// </summary>
    public struct CarryingMessage
    {
        public byte Hand;
        public CarriedThing[] Things;

        public void Write(PacketWriter w)
        {
            w.WriteByte((byte)MessageKind.Carrying);
            w.WriteByte(Hand);
            int count = Things != null ? Things.Length : 0;
            w.WriteByte((byte)count);
            for (int i = 0; i < count; i++)
            {
                w.WriteByte(Things[i].Place);
                w.WriteUInt64(Things[i].Id);
                w.WriteUInt32(Things[i].Definition.Id.Value);
                ThingWire.Write(w, Things[i].Item.State);
            }
        }

        public static CarryingMessage Read(PacketReader r)
        {
            CarryingMessage m;
            m.Hand = r.ReadByte();
            int count = r.ReadByte();
            if (count > Hands.Places || m.Hand > Hands.Places)
                throw new ProtocolException("carrying " + count + " things with the hand at place " + m.Hand + "; the hands have " + Hands.Places + " places");
            m.Things = new CarriedThing[count];
            for (int i = 0; i < count; i++)
            {
                byte place = r.ReadByte();
                if (place < 1 || place > Hands.Places) throw new ProtocolException("a carried thing in place " + place + "; the places are 1 to " + Hands.Places);
                ulong id = r.ReadUInt64();
                DefinitionId definitionId = new DefinitionId(r.ReadUInt32());
                if (!DefinitionCatalogue.TryById(definitionId, out Definition definition))
                    throw new ProtocolException("carried thing " + id + " has definition " + definitionId + ", which this build does not know");
                ItemComponent item = default;
                item.Resting = true;
                item.State = ThingWire.Read(r);
                m.Things[i] = new CarriedThing { Id = id, Definition = definition, Place = place, Item = item };
            }
            return m;
        }
    }

    /// <summary>
    /// Server → client, reliable (protocol v7, M1.5b): what has been taken from the loose layer, cell by cell — every
    /// cell at the join, before the snapshot's end, and a cell's takings again to every client whenever a founder takes
    /// something from it. A client adds what it is told to what it holds: nothing taken is ever put back.
    /// </summary>
    public struct LooseTakenMessage
    {
        /// <summary>The most cells one message carries, eight bytes each: well inside <see cref="ProtocolInfo.MaxMessageBytes"/>.</summary>
        public const int MaxCells = 2048;

        public LooseTaken.Cell[] Cells;

        public void Write(PacketWriter w)
        {
            int count = Cells != null ? Cells.Length : 0;
            if (count > MaxCells) throw new ProtocolException(count + " cells in one message; at most " + MaxCells);
            w.WriteByte((byte)MessageKind.LooseTaken);
            w.WriteUInt16((ushort)count);
            for (int i = 0; i < count; i++)
            {
                LooseTaken.Cell c = Cells[i];
                if (c.Row < 0 || c.Row > ushort.MaxValue || c.Col < 0 || c.Col > ushort.MaxValue)
                    throw new ProtocolException("cell (" + c.Row + ", " + c.Col + ") does not fit the wire");
                w.WriteUInt16((ushort)c.Row);
                w.WriteUInt16((ushort)c.Col);
                w.WriteUInt16(c.Sticks);
                w.WriteUInt16(c.Cobbles);
            }
        }

        public static LooseTakenMessage Read(PacketReader r)
        {
            LooseTakenMessage m;
            int count = r.ReadUInt16();
            if (count > MaxCells) throw new ProtocolException(count + " cells in one message; at most " + MaxCells);
            m.Cells = new LooseTaken.Cell[count];
            for (int i = 0; i < count; i++)
            {
                LooseTaken.Cell c;
                c.Row = r.ReadUInt16();
                c.Col = r.ReadUInt16();
                c.Sticks = r.ReadUInt16();
                c.Cobbles = r.ReadUInt16();
                if ((c.Sticks >> LooseCodes.MaxEach) != 0 || (c.Cobbles >> LooseCodes.MaxEach) != 0)
                    throw new ProtocolException("cell (" + c.Row + ", " + c.Col + ") has a taking past the " + LooseCodes.MaxEach + " things a code counts");
                m.Cells[i] = c;
            }
            return m;
        }
    }
}
