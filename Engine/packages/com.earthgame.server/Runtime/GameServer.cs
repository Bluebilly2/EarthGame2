using System;
using System.Collections.Generic;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Transport;

namespace EarthGame.Server
{
    /// <summary>
    /// The authoritative world host. Owns the one <see cref="WorldState"/>, the sessions, and the fixed-step
    /// loop. It does not know what a socket is: it is handed an <see cref="IServerTransport"/> and pumps it.
    /// Single player, a hosted game and a dedicated server all run this same object; only the transport and
    /// the caller of <see cref="Update(double)"/> differ (plan §4.6, Rule 1: there is no single player).
    ///
    /// <para>Pause is a server concept: while <see cref="Paused"/>, the loop still pumps the transport (so a
    /// paused host still answers pings, serves tiles and refuses late joiners cleanly) but releases no world
    /// steps and accepts no moves, so the world digest holds (N3's held-tick variant).</para>
    ///
    /// <para>Movement is client-authoritative and server-validated: a PlayerMove is checked by
    /// <see cref="MovementValidator"/> against the last accepted body, the heightfield and the region's edge, and
    /// a violation is answered with a Correction to where the server holds the player. Accepted bodies are sent
    /// after each tick to every other session within the interest radius (M1.B).</para>
    ///
    /// <para>A session that ends keeps its body here by player name, so the same name wakes where it was
    /// (M1.B rejoin). The Welcome is followed by a snapshot of every body the joiner can see and a SnapshotEnd,
    /// sent after the next step so that the states it carries are the ones that tick's digest names (a snapshot
    /// sent at the Hello carried a body a move had just changed, stamped with the tick before it: one mirror
    /// digest in 310 disagreed with the server's in the first full corpus, 2026-09-08); while paused it goes at
    /// once, since no body changes then. The tiles the joiner asks for are served by the <see cref="TileService"/>.</para>
    /// </summary>
    public sealed class GameServer
    {
        private readonly ServerConfig _config;
        private readonly IServerTransport _transport;
        private readonly FixedStepAccumulator _accumulator;
        private readonly Dictionary<int, PlayerSession> _sessionsByConnection = new Dictionary<int, PlayerSession>();
        private readonly List<PlayerSession> _sessions = new List<PlayerSession>();
        private readonly HashSet<int> _refused = new HashSet<int>();
        private readonly PacketWriter _writer = new PacketWriter(512);
        private readonly Dictionary<string, SavedPlayer> _savedPlayers = new Dictionary<string, SavedPlayer>(StringComparer.Ordinal);
        private readonly TileService _tiles;
        private readonly TickStats _ticks;
        private uint _nextSessionId = 1;
        private readonly List<Entity> _near = new List<Entity>();
        private readonly List<Entity> _retired = new List<Entity>();
        private readonly HashSet<ulong> _nearIds = new HashSet<ulong>();
        private readonly List<ulong> _leaving = new List<ulong>();
        private readonly List<EntityRecord> _records = new List<EntityRecord>();

        public GameServer(ServerConfig config, IServerTransport transport, WorldState world)
        {
            _config = config ?? new ServerConfig();
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            World = world ?? throw new ArgumentNullException(nameof(world));
            _accumulator = new FixedStepAccumulator(1.0 / _config.TickRate, _config.MaxStepsPerUpdate);
            _tiles = new TileService(World);
            _ticks = new TickStats(1.0 / _config.TickRate);
        }

        /// <summary>The world this server is authoritative for.</summary>
        public WorldState World { get; }

        /// <summary>The configuration the server was started with; the mover numbers in it are what clients must use.</summary>
        public ServerConfig Config => _config;

        /// <summary>Connected players, in join order.</summary>
        public IReadOnlyList<PlayerSession> Sessions => _sessions;

        /// <summary>The tiles served to joiners, and the bytes they cost.</summary>
        public TileService Tiles => _tiles;

        /// <summary>
        /// The cost of each host update that released a step, as timed by the clock the host passes to
        /// <see cref="Update(double, Func{double})"/>: the transport pump, the events, the steps and the broadcast
        /// together, which is what has to fit inside the tick interval (N4). Empty when no clock is passed.
        /// </summary>
        public TickStats Ticks => _ticks;

        /// <summary>While true no world step is released; the transport is still pumped.</summary>
        public bool Paused { get; set; }

        /// <summary>Real seconds the loop dropped because the host stalled. Diagnostics.</summary>
        public double DroppedSeconds => _accumulator.DroppedSeconds;

        /// <summary>Raised after every world step, with the step length. Systems that must observe ticks hook here.</summary>
        public event Action<WorldState, double> Stepped;

        /// <summary>Raised when a session is created (after Welcome is queued) or removed.</summary>
        public event Action<PlayerSession> SessionJoined;
        public event Action<PlayerSession, string> SessionLeft;

        /// <summary>Raised when a reported move is refused, with the reason; every one on a legal walk is a false positive (N2).</summary>
        public event Action<PlayerSession, string> MoveCorrected;

        /// <summary>
        /// Players a previous run left in this world, by name: one who joins with a saved name wakes where they
        /// were, not at the region's wake point. Read by the host from the world folder before listening; added
        /// to by every session that ends.
        /// </summary>
        public void RememberPlayers(IEnumerable<SavedPlayer> players)
        {
            if (players == null) return;
            foreach (SavedPlayer p in players)
                if (!string.IsNullOrEmpty(p.Name)) _savedPlayers[p.Name] = p;
        }

        /// <summary>Writes the world folder: the server is its only writer (ARCHITECTURE §6).</summary>
        public void Save(string dir, string nowUtcText, IReadOnlyDictionary<string, string> layerChecksums = null)
            => WorldSave.Write(dir, World, PlayersToSave(), nowUtcText, layerChecksums);

        /// <summary>
        /// The world folder's next save, made now from the world and the players as they stand, for
        /// <see cref="PreparedSave.Commit"/> to write on another thread while the server steps on (M1.3c).
        /// </summary>
        public PreparedSave PrepareSave(string dir, string nowUtcText, IReadOnlyDictionary<string, string> layerChecksums = null)
            => WorldSave.Prepare(dir, World, PlayersToSave(), nowUtcText, layerChecksums);

        /// <summary>Every body the server knows by name: the remembered ones, overlaid by the live sessions'.</summary>
        public IReadOnlyList<SavedPlayer> PlayersToSave()
        {
            Dictionary<string, SavedPlayer> all = new Dictionary<string, SavedPlayer>(_savedPlayers, StringComparer.Ordinal);
            for (int i = 0; i < _sessions.Count; i++)
                if (_sessions[i].HasBody) all[_sessions[i].Name] = SavedOf(_sessions[i]);
            return new List<SavedPlayer>(all.Values);
        }

        /// <summary>The world's name: clock, tick, every body by player name and what each carries (ARCHITECTURE §6).</summary>
        public string Digest()
        {
            List<KeyValuePair<string, MoverState>> bodies = new List<KeyValuePair<string, MoverState>>();
            List<CarrierRecord> carriers = new List<CarrierRecord>();
            foreach (SavedPlayer p in PlayersToSave())
            {
                bodies.Add(new KeyValuePair<string, MoverState>(p.Name, p.Body));
                carriers.Add(new CarrierRecord { Name = p.Name, Hand = p.Hand, Things = p.Carried ?? Array.Empty<CarriedThing>() });
            }
            return WorldDigest.World(World, bodies, carriers);
        }

        /// <summary>The name of one session's accepted body, as a client's mirror of it would compute it.</summary>
        public string BodyDigest(PlayerSession session)
        {
            return WorldDigest.Bodies(new[] { new KeyValuePair<uint, MoverState>(session.SessionId, session.Body) });
        }

        /// <summary>Starts listening. The world exists before the first player does.</summary>
        public void Listen(int port) => _transport.Listen(port);

        /// <summary>One host update without step timing; see the other overload.</summary>
        public void Update(double realSecondsElapsed) => Update(realSecondsElapsed, null);

        /// <summary>
        /// One host update: pumps the transport, handles every pending event, then releases as many fixed
        /// steps as the elapsed real time owes (bounded), then sends the bodies. Call from the host's loop with the
        /// real seconds since the previous call; the time scale, when it exists, multiplies here and nowhere else.
        /// The host's clock, when given, times the whole update into <see cref="Ticks"/> whenever a step was
        /// released; the server reads no clock of its own.
        /// </summary>
        public void Update(double realSecondsElapsed, Func<double> hostClockSeconds)
        {
            double started = hostClockSeconds != null ? hostClockSeconds() : 0.0;
            _transport.Update(realSecondsElapsed);
            if (realSecondsElapsed > 0.0)
            {
                double cap = _config.Movement.MoveCreditCapSeconds;
                for (int i = 0; i < _sessions.Count; i++)
                    _sessions[i].MoveCredit = Math.Min(cap, _sessions[i].MoveCredit + realSecondsElapsed);
            }
            TransportEvent evt;
            while (_transport.Poll(out evt)) Handle(evt);

            World.InterestPoints.Clear();
            for (int i = 0; i < _sessions.Count; i++)
                if (_sessions[i].HasBody) World.InterestPoints.Add(_sessions[i].Body.Feet);
            _accumulator.Accumulate(realSecondsElapsed);
            bool stepped = false;
            while (_accumulator.TryStep())
            {
                if (Paused) continue;
                World.Step(_accumulator.StepSeconds);
                Stepped?.Invoke(World, _accumulator.StepSeconds);
                stepped = true;
            }
            if (stepped || Paused) FlushSnapshots();
            if (stepped)
            {
                BroadcastBodies();
                ReplicateEntities();
                if (hostClockSeconds != null) _ticks.Record(hostClockSeconds() - started);
            }
        }

        private void Handle(TransportEvent evt)
        {
            switch (evt.Kind)
            {
                case TransportEventKind.Connected:
                    // Nothing yet: a connection is not a player until its Hello is accepted.
                    break;
                case TransportEventKind.Data:
                    HandleData(evt.Connection, evt.Data, evt.Offset, evt.Count);
                    break;
                case TransportEventKind.Disconnected:
                    _refused.Remove(evt.Connection.Id);
                    PlayerSession gone;
                    if (_sessionsByConnection.TryGetValue(evt.Connection.Id, out gone))
                        EndSession(gone, evt.Reason ?? string.Empty);
                    break;
            }
        }

        /// <summary>Removes a session, keeps its body by name, and tells the others it has gone.</summary>
        private void EndSession(PlayerSession gone, string reason)
        {
            _sessionsByConnection.Remove(gone.Connection.Id);
            _sessions.Remove(gone);
            if (gone.HasBody) _savedPlayers[gone.Name] = SavedOf(gone);
            PlayerLeftMessage left;
            left.SessionId = gone.SessionId;
            _writer.Reset();
            left.Write(_writer);
            for (int i = 0; i < _sessions.Count; i++)
                _sessions[i].Connection.Send(_writer.Written, Delivery.Reliable);
            SessionLeft?.Invoke(gone, reason);
        }

        private SavedPlayer SavedOf(PlayerSession s)
        {
            SavedPlayer p = new SavedPlayer();
            p.Name = s.Name;
            p.Body = s.Body;
            p.YawDeg = s.YawDeg;
            p.PitchDeg = s.PitchDeg;
            p.SavedTick = World.Tick;
            p.Carried = new CarriedThing[s.Hands.Things.Count];
            for (int i = 0; i < p.Carried.Length; i++) p.Carried[i] = s.Hands.Things[i];
            p.Hand = s.Hands.Hand;
            return p;
        }

        private void HandleData(IConnection connection, byte[] data, int offset, int count)
        {
            if (_refused.Contains(connection.Id)) return;
            MessageKind kind = MessageHeader.PeekKind(data, offset, count);
            PacketReader reader;
            try
            {
                reader = new PacketReader(data, offset, count);
                reader.ReadByte(); // the kind, already peeked
                PlayerSession session;
                bool known = _sessionsByConnection.TryGetValue(connection.Id, out session);

                if (!known)
                {
                    if (kind != MessageKind.Hello)
                    {
                        Refuse(connection, "first message must be Hello, got " + kind);
                        return;
                    }
                    HelloMessage hello = HelloMessage.Read(reader);
                    reader.ExpectEnd();
                    HandleHello(connection, hello);
                    return;
                }

                switch (kind)
                {
                    case MessageKind.Ping:
                    {
                        PingMessage ping = PingMessage.Read(reader);
                        reader.ExpectEnd();
                        PongMessage pong;
                        pong.ClientTimeMs = ping.ClientTimeMs;
                        pong.ServerTick = World.Tick;
                        _writer.Reset();
                        pong.Write(_writer);
                        connection.Send(_writer.Written, Delivery.Unreliable);
                        break;
                    }
                    case MessageKind.PlayerMove:
                    {
                        PlayerMoveMessage move = PlayerMoveMessage.Read(reader);
                        reader.ExpectEnd();
                        // A held tick holds the bodies too: a move reported while paused is neither accepted nor
                        // corrected, and the first move after the resume is judged over the whole interval.
                        if (!Paused) HandlePlayerMove(session, move);
                        break;
                    }
                    case MessageKind.TileRequest:
                    {
                        TileRequestMessage request = TileRequestMessage.Read(reader);
                        reader.ExpectEnd();
                        _tiles.Serve(connection, request);
                        break;
                    }
                    case MessageKind.Intent:
                    {
                        IntentMessage intent = IntentMessage.Read(reader);
                        reader.ExpectEnd();
                        HandleIntent(session, intent);
                        break;
                    }
                    default:
                        // Unknown or out-of-place kinds are ignored, not fatal: a newer client may speak more
                        // than this server understands, and the protocol version check already gates layout.
                        break;
                }
            }
            catch (ProtocolException ex)
            {
                Refuse(connection, "malformed " + kind + ": " + ex.Message);
            }
        }

        private void HandleHello(IConnection connection, HelloMessage hello)
        {
            if (hello.ProtocolVersion != ProtocolInfo.Version)
            {
                Refuse(connection, "protocol version " + hello.ProtocolVersion + " differs from the server's " + ProtocolInfo.Version);
                return;
            }
            string name = hello.PlayerName ?? string.Empty;
            if (name.Length == 0 || name.Length > _config.MaxPlayerNameLength)
            {
                Refuse(connection, "player name must be 1 to " + _config.MaxPlayerNameLength + " characters");
                return;
            }
            if (!string.IsNullOrEmpty(_config.Password) && !string.Equals(_config.Password, hello.Password ?? string.Empty, StringComparison.Ordinal))
            {
                Refuse(connection, "password rejected");
                return;
            }

            // A name that is still connected is a rejoin the transport has not yet noticed the cut of (a cable
            // pulled, a process killed): the old session ends now, its body remembered, and the new one takes it.
            PlayerSession previous = FindByName(name);
            if (previous != null)
            {
                previous.Connection.Close("superseded by a new session for " + name);
                EndSession(previous, "superseded");
            }

            if (_sessions.Count >= _config.MaxPlayers)
            {
                Refuse(connection, "server is full (" + _config.MaxPlayers + " players)");
                return;
            }

            PlayerSession session = new PlayerSession(_nextSessionId++, name, connection, World.Tick);
            session.MoveCredit = _config.Movement.MoveCreditCapSeconds;
            _sessionsByConnection[connection.Id] = session;
            _sessions.Add(session);

            Double3 spawn = World.SpawnPoint();
            SavedPlayer saved;
            if (_savedPlayers.TryGetValue(name, out saved))
            {
                spawn = saved.Body.Feet;
                session.Body = saved.Body;
                session.YawDeg = saved.YawDeg;
                session.PitchDeg = saved.PitchDeg;
                session.LastMoveTick = World.Tick;
                session.HasBody = true;
                session.Hands.Restore(saved.Carried, saved.Hand);
            }
            WelcomeMessage welcome;
            welcome.SessionId = session.SessionId;
            welcome.Seed = World.Seed;
            welcome.RegionId = World.RegionId;
            welcome.ExtentM = World.Region.ExtentM;
            welcome.TotalHours = World.Clock.TotalHours;
            welcome.Tick = World.Tick;
            welcome.TickRate = (byte)_config.TickRate;
            welcome.SpawnEast = spawn.X;
            welcome.SpawnUp = spawn.Y;
            welcome.SpawnNorth = spawn.Z;
            welcome.InterestRadiusM = _config.InterestRadiusM;
            _writer.Reset();
            welcome.Write(_writer);
            connection.Send(_writer.Written, Delivery.Reliable);
            session.SnapshotPending = true;
            session.SnapshotEast = spawn.X;
            session.SnapshotNorth = spawn.Z;
            SessionJoined?.Invoke(session);
        }

        private PlayerSession FindByName(string name)
        {
            for (int i = 0; i < _sessions.Count; i++)
                if (string.Equals(_sessions[i].Name, name, StringComparison.Ordinal)) return _sessions[i];
            return null;
        }

        /// <summary>The snapshots owed to sessions welcomed since the last step: every other body each can see, reliably, then the end marker.</summary>
        private void FlushSnapshots()
        {
            for (int j = 0; j < _sessions.Count; j++)
            {
                PlayerSession joiner = _sessions[j];
                if (!joiner.SnapshotPending) continue;
                joiner.SnapshotPending = false;
                for (int i = 0; i < _sessions.Count; i++)
                {
                    PlayerSession subject = _sessions[i];
                    if (subject == joiner || !subject.HasBody) continue;
                    if (!WithinInterest(subject.Body, joiner.SnapshotEast, joiner.SnapshotNorth)) continue;
                    WriteState(subject);
                    joiner.Connection.Send(_writer.Written, Delivery.Reliable);
                }
                // Every entity within the radius of the spawn, in full and unbudgeted: the join is once.
                _near.Clear();
                World.Entities.Within(joiner.SnapshotEast, joiner.SnapshotNorth, _config.InterestRadiusM, _near);
                for (int i = 0; i < _near.Count; i++)
                {
                    if (_near[i].Killed) continue;
                    WriteSpawn(_near[i]);
                    joiner.Connection.Send(_writer.Written, Delivery.Reliable);
                    joiner.Interest[_near[i].Id.Value] = World.Tick;
                }
                // What the joiner carries and what has been taken from the ground, before the end: interactive means
                // both are known too.
                SendCarrying(joiner);
                SendTaken(joiner);
                SnapshotEndMessage end;
                end.ServerTick = World.Tick;
                _writer.Reset();
                end.Write(_writer);
                joiner.Connection.Send(_writer.Written, Delivery.Reliable);
            }
        }

        private void HandlePlayerMove(PlayerSession session, PlayerMoveMessage move)
        {
            // Unreliable and unordered: a report older than the newest accepted is stale, neither judged nor adopted.
            if (session.HasBody && move.Sequence <= session.LastSequence) return;
            // Measured over the client's own spacing (one tick interval per sequence step), bounded by the real
            // time this session has banked (MovementRules.MoveCreditCapSeconds).
            double interval = 0.0;
            if (session.HasBody)
            {
                double bySequence = (move.Sequence - session.LastSequence) * _accumulator.StepSeconds;
                interval = Math.Min(bySequence, session.MoveCredit);
            }
            string reason = MovementValidator.Check(session.Body, session.HasBody, move.Body, interval, World.Terrain,
                                                    World.Region.HalfExtentM, _config.Mover, _config.Movement);
            if (reason == null)
            {
                session.MoveCredit = Math.Max(0.0, session.MoveCredit - interval);
                session.Body = move.Body;
                session.YawDeg = move.YawDeg;
                session.PitchDeg = move.PitchDeg;
                session.LastSequence = move.Sequence;
                session.LastMoveTick = World.Tick;
                session.HasBody = true;
                session.MovesAccepted++;
                return;
            }

            session.Corrections++;
            CorrectionMessage correction;
            correction.Sequence = move.Sequence;
            correction.ServerTick = World.Tick;
            if (session.HasBody)
            {
                correction.Body = session.Body;
            }
            else
            {
                Double3 spawn = World.SpawnPoint();
                correction.Body = MoverState.AtRest(spawn.X, spawn.Y, spawn.Z);
            }
            correction.Reason = reason;
            _writer.Reset();
            correction.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
            MoveCorrected?.Invoke(session, reason);
        }

        /// <summary>Whether a viewer at a point is sent a subject's body: within the interest radius, horizontally.</summary>
        private bool WithinInterest(in MoverState subject, double viewerEast, double viewerNorth)
        {
            double dx = subject.East - viewerEast;
            double dz = subject.North - viewerNorth;
            double r = _config.InterestRadiusM;
            return dx * dx + dz * dz <= r * r;
        }

        /// <summary>True when the viewer is sent the subject's state: no body yet sees everything.</summary>
        public bool CanSee(PlayerSession viewer, PlayerSession subject)
        {
            if (viewer == subject || !subject.HasBody) return false;
            if (!viewer.HasBody) return true;
            return WithinInterest(subject.Body, viewer.Body.East, viewer.Body.North);
        }

        private void WriteState(PlayerSession subject)
        {
            PlayerStateMessage state;
            state.SessionId = subject.SessionId;
            state.Sequence = subject.LastSequence;
            state.ServerTick = World.Tick;
            state.YawDeg = subject.YawDeg;
            state.PitchDeg = subject.PitchDeg;
            state.Body = subject.Body;
            _writer.Reset();
            state.Write(_writer);
        }

        private void BroadcastBodies()
        {
            if (_sessions.Count < 2) return;
            for (int i = 0; i < _sessions.Count; i++)
            {
                PlayerSession subject = _sessions[i];
                if (!subject.HasBody) continue;
                bool written = false;
                for (int j = 0; j < _sessions.Count; j++)
                {
                    PlayerSession viewer = _sessions[j];
                    if (!CanSee(viewer, subject)) continue;
                    if (!written)
                    {
                        WriteState(subject);
                        written = true;
                    }
                    viewer.Connection.Send(_writer.Written, Delivery.Unreliable);
                }
            }
        }

        /// <summary>
        /// Puts a remembered body somewhere, so the next join by that name wakes there (M1.4c). A diagnostic, for
        /// looking at a place a founder would otherwise have to walk half an hour to reach; it does not move a
        /// player already connected, and the body is placed on the ground the server holds.
        /// </summary>
        public SavedPlayer StandPlayer(string name, double east, double north)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("a player has a name", nameof(name));
            if (Math.Abs(east) > World.Region.HalfExtentM || Math.Abs(north) > World.Region.HalfExtentM)
                throw new ArgumentOutOfRangeException(nameof(east), "(" + east + ", " + north + ") is outside the region");
            SavedPlayer p = new SavedPlayer();
            p.Name = name;
            p.Body = MoverState.AtRest(east, World.GroundAt(east, north), north);
            p.Body.Grounded = true;
            p.SavedTick = World.Tick;
            // Standing a founder somewhere else does not empty their hands.
            if (_savedPlayers.TryGetValue(name, out SavedPlayer before))
            {
                p.Carried = before.Carried;
                p.Hand = before.Hand;
            }
            _savedPlayers[name] = p;
            return p;
        }

        /// <summary>Drops an item into the world by its key; the host's console and M1.5's verbs come here.</summary>
        public Entity SpawnItem(string key, double east, double north, double? up = null)
        {
            Definition definition = DefinitionCatalogue.ByKey(key);
            if (Math.Abs(east) > World.Region.HalfExtentM || Math.Abs(north) > World.Region.HalfExtentM)
                throw new ArgumentOutOfRangeException(nameof(east), "(" + east + ", " + north + ") is outside the region");
            return World.SpawnItem(definition, east, north, up);
        }

        /// <summary>Kills an entity by id; false when there is none. It leaves at the end of the next step, and its viewers are told then.</summary>
        public bool KillEntity(ulong id)
        {
            if (!World.Entities.TryGet(id, out Entity e) || e.Killed) return false;
            World.Entities.Kill(e);
            return true;
        }

        /// <summary>The name of the entities a session has been shown, as its mirror computes it (ARCHITECTURE §7).</summary>
        public string EntityDigest(PlayerSession session)
        {
            _records.Clear();
            foreach (KeyValuePair<ulong, long> pair in session.Interest)
                if (World.Entities.TryGet(pair.Key, out Entity e)) _records.Add(e.Record());
            return WorldDigest.Entities(_records);
        }

        private void Viewpoint(PlayerSession s, out double east, out double north)
        {
            if (s.HasBody)
            {
                east = s.Body.East;
                north = s.Body.North;
            }
            else
            {
                east = s.SnapshotEast;
                north = s.SnapshotNorth;
            }
        }

        private void WriteSpawn(Entity e)
        {
            EntitySpawnMessage m;
            m.Id = e.Id.Value;
            m.DefinitionId = e.Definition.Id.Value;
            m.ServerTick = World.Tick;
            m.East = e.Position.X;
            m.Up = e.Position.Y;
            m.North = e.Position.Z;
            m.YawDeg = e.YawDeg;
            m.HasItem = e.HasItem;
            m.Item = e.Item;
            m.HasAnimal = e.HasAnimal;
            m.Animal = e.Animal;
            _writer.Reset();
            m.Write(_writer);
        }

        private void WriteEntityState(Entity e, EntityFields fields)
        {
            EntityStateMessage m;
            m.Id = e.Id.Value;
            m.ServerTick = World.Tick;
            m.Fields = fields;
            m.East = e.Position.X;
            m.Up = e.Position.Y;
            m.North = e.Position.Z;
            m.YawDeg = e.YawDeg;
            m.Item = e.Item;
            m.Animal = e.Animal;
            _writer.Reset();
            m.Write(_writer);
        }

        private void WriteGone(ulong id, byte reason)
        {
            EntityGoneMessage m;
            m.Id = id;
            m.Reason = reason;
            _writer.Reset();
            m.Write(_writer);
        }

        /// <summary>
        /// After every step: each session is told which of its entities died or left its interest (leaving takes
        /// InterestMarginM more than entering), then shown the ones that entered and the changes to those it holds,
        /// within its byte budget (ARCHITECTURE §7 rule 5); the stamps on the entity say what it has not seen, so
        /// what the budget defers is sent later, not lost. A state that carries an item's rest or an animal's pose goes
        /// reliably, since nothing follows either to put a loss right (M1.7a).
        /// </summary>
        private void ReplicateEntities()
        {
            _retired.Clear();
            World.Entities.DrainRetired(_retired);
            double radius = _config.InterestRadiusM, outer = radius + _config.InterestMarginM;
            double r2 = radius * radius;
            for (int s = 0; s < _sessions.Count; s++)
            {
                PlayerSession session = _sessions[s];
                if (session.SnapshotPending) continue;
                for (int i = 0; i < _retired.Count; i++)
                    if (session.Interest.Remove(_retired[i].Id.Value))
                    {
                        // An animal taken away left the founders' reach; it did not die (M1.7a).
                        WriteGone(_retired[i].Id.Value, _retired[i].IsTransient ? EntityGoneMessage.Left
                                                        : _retired[i].Taken ? EntityGoneMessage.TakenUp : EntityGoneMessage.Died);
                        session.Connection.Send(_writer.Written, Delivery.Reliable);
                    }
                Viewpoint(session, out double east, out double north);
                _near.Clear();
                _nearIds.Clear();
                World.Entities.Within(east, north, outer, _near);
                for (int i = 0; i < _near.Count; i++) _nearIds.Add(_near[i].Id.Value);
                _leaving.Clear();
                foreach (KeyValuePair<ulong, long> pair in session.Interest)
                    if (!_nearIds.Contains(pair.Key)) _leaving.Add(pair.Key);
                for (int i = 0; i < _leaving.Count; i++)
                {
                    session.Interest.Remove(_leaving[i]);
                    WriteGone(_leaving[i], EntityGoneMessage.Left);
                    session.Connection.Send(_writer.Written, Delivery.Reliable);
                }
                int budget = _config.EntityBytesPerTick;
                for (int i = 0; i < _near.Count && budget > 0; i++)
                {
                    Entity e = _near[i];
                    if (e.Killed) continue;
                    if (session.Interest.TryGetValue(e.Id.Value, out long sent))
                    {
                        EntityFields fields = e.ChangedSince(sent);
                        if (fields == EntityFields.None) continue;
                        WriteEntityState(e, fields);
                        if (_writer.Written.Length > budget) break;
                        budget -= _writer.Written.Length;
                        session.Connection.Send(_writer.Written, (fields & (EntityFields.Item | EntityFields.Pose)) != 0 ? Delivery.Reliable : Delivery.Unreliable);
                        session.Interest[e.Id.Value] = World.Tick;
                    }
                    else
                    {
                        double dx = e.Position.X - east, dz = e.Position.Z - north;
                        if (dx * dx + dz * dz > r2) continue;
                        WriteSpawn(e);
                        if (_writer.Written.Length > budget) break;
                        budget -= _writer.Written.Length;
                        session.Connection.Send(_writer.Written, Delivery.Reliable);
                        session.Interest[e.Id.Value] = World.Tick;
                    }
                }
            }
        }

        /// <summary>
        /// A verb, committed on the world the server holds (M1.5a): the founder's hands do it or say why not, the answer
        /// goes back under the intent's sequence, and a change to the hands is sent to their owner. While the world is
        /// held, or before the founder has a body and their snapshot, nothing is done.
        /// </summary>
        private void HandleIntent(PlayerSession session, IntentMessage intent)
        {
            VerbOutcome outcome;
            if (Paused || !session.HasBody || session.SnapshotPending) outcome = VerbOutcome.NotNow;
            else
            {
                Double3 eye = Eye(session);
                switch (intent.Verb)
                {
                    case Verb.PickUp:
                        if (intent.Target == IntentMessage.TargetLying)
                        {
                            outcome = session.Hands.PickUpLying(World, intent.Lying, eye);
                            if (outcome == VerbOutcome.Done) BroadcastTaken(intent.Lying.Row, intent.Lying.Col);
                        }
                        else outcome = session.Hands.PickUp(World, intent.EntityId, eye);
                        break;
                    case Verb.PutDown:
                        outcome = session.Hands.PutDown(World, new Double3(intent.East, intent.Up, intent.North), eye, session.YawDeg);
                        break;
                    case Verb.Hold:
                        outcome = session.Hands.Hold(intent.Place);
                        break;
                    default:
                        outcome = VerbOutcome.NotNow;
                        break;
                }
            }
            IntentResultMessage result;
            result.Sequence = intent.Sequence;
            result.Outcome = outcome;
            _writer.Reset();
            result.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
            if (outcome == VerbOutcome.Done) SendCarrying(session);
        }

        /// <summary>Where a founder's eye is, as the server holds their body: a verb's reach is measured from here.</summary>
        private Double3 Eye(PlayerSession s) => new Double3(s.Body.East, s.Body.Up + _config.Mover.EyeHeight(s.Body.Stance), s.Body.North);

        /// <summary>What a founder carries, sent to them alone.</summary>
        private void SendCarrying(PlayerSession session)
        {
            CarryingMessage m;
            m.Hand = session.Hands.Hand;
            m.Things = new CarriedThing[session.Hands.Things.Count];
            for (int i = 0; i < m.Things.Length; i++) m.Things[i] = session.Hands.Things[i];
            _writer.Reset();
            m.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
        }

        /// <summary>
        /// A cell's takings, to every founder whose snapshot is sent (M1.5b): each client holds every taking, so a stick
        /// taken is gone for whoever holds that tile now and whoever comes to it later. A joiner still waiting for its
        /// snapshot is sent every taking with it.
        /// </summary>
        private void BroadcastTaken(int row, int col)
        {
            if (!World.Taken.TryGet(row, col, out LooseTaken.Cell cell)) return;
            LooseTakenMessage m;
            m.Cells = new[] { cell };
            _writer.Reset();
            m.Write(_writer);
            for (int i = 0; i < _sessions.Count; i++)
                if (!_sessions[i].SnapshotPending) _sessions[i].Connection.Send(_writer.Written, Delivery.Reliable);
        }

        /// <summary>Everything taken from the loose layer so far, to a joiner, in messages of at most <see cref="LooseTakenMessage.MaxCells"/> cells.</summary>
        private void SendTaken(PlayerSession joiner)
        {
            List<LooseTaken.Cell> cells = World.Taken.Cells();
            for (int start = 0; start < cells.Count; start += LooseTakenMessage.MaxCells)
            {
                LooseTakenMessage m;
                m.Cells = cells.GetRange(start, Math.Min(LooseTakenMessage.MaxCells, cells.Count - start)).ToArray();
                _writer.Reset();
                m.Write(_writer);
                joiner.Connection.Send(_writer.Written, Delivery.Reliable);
            }
        }

        private void Refuse(IConnection connection, string reason)
        {
            _refused.Add(connection.Id);
            RefusedMessage refused;
            refused.Reason = reason;
            _writer.Reset();
            refused.Write(_writer);
            connection.Send(_writer.Written, Delivery.Reliable);
            connection.Close(reason);
        }
    }
}
