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
            _withoutFlight = _config.Movement.WithoutFlight();
        }

        /// <summary>The server's movement rules with flight taken away, made once: what a player is held to until they switch developer mode on (M1.E).</summary>
        private readonly MovementRules _withoutFlight;

        /// <summary>
        /// The movement rules this player's moves are held to: the server's own while they have developer mode on, the same
        /// without flight otherwise (M1.E). A development server once let every player fly; since 2026-09-20 only the one who
        /// switched it on.
        /// </summary>
        public MovementRules MovementRulesFor(PlayerSession session)
            => session != null && session.DeveloperMode && _config.Movement.AllowFlight ? _config.Movement : _withoutFlight;

        /// <summary>The world this server is authoritative for.</summary>
        public WorldState World { get; }

        /// <summary>
        /// The ground a founder's reported feet are judged against (BF.4): the world's one ground, relief and hollows with it,
        /// where until then the raster alone stood; null for a world without terrain, which judges no height.
        /// </summary>
        private IHeightSource FeetGround => World.Terrain == null ? null : _feetGround ?? (_feetGround = new FineGroundSource(World));

        private IHeightSource _feetGround;

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

        /// <summary>Raised when a developer's setting has been applied (M1.D), for the host's log.</summary>
        public event Action<PlayerSession, DevSettingMessage> DevSettingApplied;

        /// <summary>A founder died (FP.2): who, and the death with its numbers, for the host's record and the game's log.</summary>
        public event Action<PlayerSession, Death> FounderDied;

        /// <summary>Where the beta arc's bridge holds a freezing founder's core, °C: a hair above the lethal, severely hypothermic, alive.</summary>
        public const double BridgeCoreC = Warmth.LethalCoreC + 0.5;

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
                AdvanceFounders(_accumulator.StepSeconds);
                AdvanceWork(_accumulator.StepSeconds);
                Stepped?.Invoke(World, _accumulator.StepSeconds);
                stepped = true;
            }
            if (stepped || Paused) FlushSnapshots();
            if (stepped)
            {
                BroadcastBodies();
                ReplicateEntities();
                SendFounderStates();
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
            p.WaterLoss = s.Hydration.Loss;
            p.CoreDeficitC = Warmth.NormalCoreC - s.Warmth.CoreC;
            return p;
        }

        /// <summary>
        /// Every founder's body lives the step on the world's clock (FP.1, FP.2): a held clock holds the body too. The
        /// surroundings are read once a second (the ground's openness is the costly part); the warmth runs every step in
        /// them, doing what the body's speed says the founder is doing; the water goes at the resting rate plus the breath's
        /// water above rest and the sweat, both the balance's own (2026-09-16).
        /// A body past the lethal core or the lethal loss dies here, in the step, whatever moved it there.
        /// </summary>
        private void AdvanceFounders(double stepSeconds)
        {
            double days = World.Clock.DaysFor(stepSeconds);
            if (days <= 0.0) return;
            double worldSeconds = days * 86400.0;
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                PlayerSession s = _sessions[i];
                if (!s.HasBody) continue;
                if (s.SurroundingsTick < 0 || World.Tick - s.SurroundingsTick >= _config.TickRate)
                {
                    s.Surroundings = World.SurroundingsAt(s.Body.East, s.Body.Up, s.Body.North);
                    s.SurroundingsTick = World.Tick;
                }
                // The effort is the gait's (BF.4 stage two): a founder slowed by sand is working as hard as their pace on the
                // table's ground, so the exertion is judged by that pace and a runner through sand is still running.
                Exertion exertion = Warmth.ExertionOf(s.Body.HorizontalSpeed / Locomotion.GroundPace(World.UnderfootAt(s.Body.East, s.Body.North)));
                s.Warmth.Tick(worldSeconds, s.Surroundings, exertion, s.Hydration.WorkCapacity01, 1.0 - s.Hydration.Loss / Hydration.LethalWaterLoss);
                s.Hydration.Advance(days, s.Warmth.BreathWaterLPerHour, s.Warmth.SweatRateLPerHour);
                if (!s.Warmth.IsAlive)
                {
                    // The beta arc's bridge (ServerConfig.BetaArcBridge): the cold reaches the edge of death and no further.
                    if (_config.BetaArcBridge) s.Warmth.Restore(BridgeCoreC);
                    else Die(s, CauseOfDeath.Cold);
                }
                else if (!s.Hydration.IsAlive) Die(s, CauseOfDeath.Thirst);
            }
        }

        /// <summary>
        /// The Standard death (the owner, 2026-09-01; FP.2): everything carried is let go where the founder fell, a new
        /// founder wakes at the wake with a full body, stood as a correction stands them, and their client is told what
        /// killed them and the numbers of it, then their new state and their empty hands. The world persists. The Hardcore
        /// mode, one life, waits for a choice at new game (DEBTS).
        /// </summary>
        private void Die(PlayerSession session, CauseOfDeath cause)
        {
            session.Work = null;
            Double3 fell = session.Body.Feet;
            Warmth warmth = session.Warmth;
            Death death = new Death(cause, World.Clock.LocalHourOfDay(World.Region.CentreLongitudeDeg), session.Surroundings.AirC,
                                    session.Surroundings.WindAtBodyMs, warmth.SensibleLossW + warmth.SkyLossW + warmth.RespiratoryLossW,
                                    warmth.ProductionW, warmth.CoreC, session.Hydration.Loss, fell.X, fell.Z);
            session.Hands.LetGoOfEverything(World, fell, session.YawDeg);
            warmth.Reset();
            session.Hydration.Restore(1.0);
            Double3 wake = World.SpawnPoint();
            session.Body = MoverState.AtRest(wake.X, wake.Y, wake.Z);
            session.Body.Grounded = true;
            session.StoodUp = wake.Y;
            session.LastMoveTick = World.Tick;
            CorrectionMessage stood;
            stood.Sequence = session.LastSequence;
            stood.ServerTick = World.Tick;
            stood.Body = session.Body;
            stood.Reason = "a new founder wakes: the last died of " + (cause == CauseOfDeath.Cold ? "the cold" : "thirst");
            _writer.Reset();
            stood.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
            DiedMessage died;
            died.Death = death;
            _writer.Reset();
            died.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
            SendFounderState(session);
            SendCarrying(session);
            FounderDied?.Invoke(session, death);
        }

        /// <summary>Each founder's state to their own client once a second, by the tick rate (FP.1).</summary>
        private void SendFounderStates()
        {
            for (int i = 0; i < _sessions.Count; i++)
            {
                PlayerSession s = _sessions[i];
                if (!s.HasBody || s.SnapshotPending) continue;
                if (s.FounderStateTick >= 0 && World.Tick - s.FounderStateTick < _config.TickRate) continue;
                SendFounderState(s);
            }
        }

        /// <summary>The founder's state to their own client now, and the capacity it carries remembered for the ceiling.</summary>
        private void SendFounderState(PlayerSession session)
        {
            session.FounderStateTick = World.Tick;
            session.ToldCapacityBefore = session.ToldCapacity;
            session.ToldCapacity = session.Hydration.WorkCapacity01;
            FounderStateMessage m;
            m.Water01 = session.Hydration.Water01;
            m.CoreC = session.Warmth.CoreC;
            _writer.Reset();
            m.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
        }

        /// <summary>
        /// A drink (FP.1): the point is judged as a put-down's is, by the reach from the eye and the region, then the water
        /// there by the world's own layer. A creek, a stream or a lake gives up to a visit's litre and a half; the sea answers
        /// salt; anything else has nothing to drink.
        /// </summary>
        private VerbOutcome Drink(PlayerSession session, Double3 at, Double3 eye)
        {
            if (Double3.Distance(eye, at) > Hands.ReachM) return VerbOutcome.OutOfReach;
            double half = World.Region.HalfExtentM;
            if (Math.Abs(at.X) > half || Math.Abs(at.Z) > half) return VerbOutcome.OutOfReach;
            WaterClass water = World.WaterAt(at.X, at.Z, out _);
            if (water == WaterClass.Sea) return VerbOutcome.Salt;
            if (!WorldLayers.IsFresh(water)) return VerbOutcome.NoWater;
            session.Hydration.Drink(Hydration.MaxDrinkPerVisitL);
            SendFounderState(session);
            return VerbOutcome.Done;
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
                        pong.ServerTotalHours = World.Clock.TotalHours;
                        pong.ClockScale = World.Clock.Scale;
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
                    case MessageKind.DevSetting:
                    {
                        DevSettingMessage setting = DevSettingMessage.Read(reader);
                        reader.ExpectEnd();
                        HandleDevSetting(session, setting);
                        break;
                    }
                    case MessageKind.DeveloperMode:
                    {
                        DeveloperModeMessage asked = DeveloperModeMessage.Read(reader);
                        reader.ExpectEnd();
                        HandleDeveloperMode(session, asked);
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
            string alike = KnownNameSharingAFileWith(name);
            if (alike != null)
            {
                Refuse(connection, "the name " + name + " is too like " + alike + ", whom this world already knows: names may not differ only in capitals or marks");
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
                session.StoodUp = saved.Body.Up;
                session.YawDeg = saved.YawDeg;
                session.PitchDeg = saved.PitchDeg;
                session.LastMoveTick = World.Tick;
                session.HasBody = true;
                session.Hands.Restore(saved.Carried, saved.Hand);
                session.Hydration.Restore(1.0 - saved.WaterLoss);
                session.Warmth.Restore(Warmth.NormalCoreC - saved.CoreDeficitC);
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

        /// <summary>
        /// A name this server knows, remembered or connected, that is not this name but would be written to its player file
        /// (M1.3d); null when there is none. The world could keep only one of the two, so the second is refused at the door.
        /// </summary>
        private string KnownNameSharingAFileWith(string name)
        {
            foreach (string known in _savedPlayers.Keys)
                if (WorldSave.ShareAFile(known, name)) return known;
            for (int i = 0; i < _sessions.Count; i++)
                if (WorldSave.ShareAFile(_sessions[i].Name, name)) return _sessions[i].Name;
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
                SendFounderState(joiner);
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
            double landed = Math.Max(session.Landing.AllowanceAt(move.Sequence, _accumulator.StepSeconds, _config.Mover),
                                     session.Stride.AllowanceAt(move.Sequence, _accumulator.StepSeconds, _config.Mover));
            // The report is judged on the faster of the grounds it leaves and reaches (BF.4 stage two): the client's mover took
            // its pace from the cell under its feet, which may be either as the report crosses a cell's edge.
            GroundType underfoot = World.UnderfootAt(move.Body.East, move.Body.North);
            if (session.HasBody) underfoot = Locomotion.Faster(underfoot, World.UnderfootAt(session.Body.East, session.Body.North));
            string reason = MovementValidator.Check(session.Body, session.HasBody, move.Body, interval, FeetGround,
                                                    World.Region.HalfExtentM, _config.Mover, MovementRulesFor(session), session.StoodUp, session.CeilingCapacity, landed,
                                                    underfoot);
            if (reason == null)
            {
                // A body on its feet keeps what its ground allowed while the brake takes it off, when the next is slower (BF.4
                // stage two): the run over rock, onto dry sand.
                double groundRun = _config.Mover.MaxHorizontalSpeedAt(session.CeilingCapacity, underfoot);
                if (move.Body.Grounded && groundRun > session.Stride.AllowanceAt(move.Sequence, _accumulator.StepSeconds, _config.Mover))
                    session.Stride = new MovementValidator.Landing { CeilingMs = groundRun, Sequence = move.Sequence };
                // A body come down on its feet keeps what its fall allowed while the brake takes it off (2026-09-23): the corpus's
                // walkers, landing from a slide, were corrected while they braked.
                if (session.HasBody && !session.Body.Grounded && move.Body.Grounded && !double.IsNaN(session.StoodUp))
                    session.Landing = new MovementValidator.Landing
                    {
                        CeilingMs = MovementValidator.FallCeiling(session.StoodUp, move.Body.Up, _config.Mover, session.CeilingCapacity),
                        Sequence = move.Sequence,
                    };
                session.MoveCredit = Math.Max(0.0, session.MoveCredit - interval);
                session.Body = move.Body;
                // A first report, or a founder on their feet, is where they stood; a fall keeps the height it left.
                if (move.Body.Grounded || double.IsNaN(session.StoodUp)) session.StoodUp = move.Body.Up;
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
            string alike = KnownNameSharingAFileWith(name);
            if (alike != null) throw new ArgumentException("the name " + name + " is too like " + alike + ", whom this world already knows", nameof(name));
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
                p.WaterLoss = before.WaterLoss;
                p.CoreDeficitC = before.CoreDeficitC;
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
            string note = string.Empty;
            float seconds = 0f;
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
                    case Verb.Drink:
                        outcome = Drink(session, new Double3(intent.East, intent.Up, intent.North), eye);
                        break;
                    case Verb.Knap:
                        outcome = Knap(session, intent, eye, out note);
                        break;
                    case Verb.Work:
                        outcome = StartWork(session, intent, eye, out note, out seconds);
                        break;
                    case Verb.StopWork:
                        outcome = StopWork(session, "let go") ? VerbOutcome.Done : VerbOutcome.NotNow;
                        break;
                    default:
                        outcome = VerbOutcome.NotNow;
                        break;
                }
            }
            IntentResultMessage result;
            result.Sequence = intent.Sequence;
            result.Outcome = outcome;
            result.Note = note;
            result.Seconds = seconds;
            _writer.Reset();
            result.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
            if (outcome == VerbOutcome.Done && intent.Verb != Verb.Drink) SendCarrying(session);
        }

        /// <summary>How far ahead of a founder's feet a flake struck off a held core lands, m: at their feet, where a knapper's flakes fall.</summary>
        private const double FlakeAheadM = 0.5;

        /// <summary>
        /// A blow on stone (FP.3): the stone in hand is the hammer, the core is the stone aimed at (an item, or one of the litter)
        /// or one held in another place, and <see cref="Knapping.Strike"/> decides what the stone does with the energy the wind-up,
        /// the hammer's mass and what thirst has left of the founder's work put behind the swing. The answer is the physics' own
        /// words. What a blow changes is committed on the world the server holds: the core's state is written back to it (or the
        /// core is gone, spent); a cobble of the litter that a blow changes becomes an item where it lay, as one taken up does;
        /// a flake comes into the world with its own mass and edge, let go a little above the ground beside the core, or at the
        /// founder's feet when the core was held, and falls. A blow that bounces changes nothing.
        /// </summary>
        private VerbOutcome Knap(PlayerSession session, IntentMessage intent, Double3 eye, out string note)
        {
            note = string.Empty;
            Hands hands = session.Hands;
            if (!hands.TryAt(hands.Hand, out CarriedThing hammer)) return VerbOutcome.NothingInHand;
            if (!KnappingItems.IsHammer(hammer.Definition)) return VerbOutcome.NoHammer;

            // The core: where it is, what it is and what blows have made of it so far.
            Definition coreDefinition;
            ItemComponent coreItem;
            Double3 at;
            Entity coreEntity = null;
            byte corePlace = 0;
            switch (intent.Target)
            {
                case IntentMessage.TargetEntity:
                {
                    if (!World.Entities.TryGet(intent.EntityId, out coreEntity) || coreEntity.Killed || !coreEntity.HasItem) return VerbOutcome.NotThere;
                    if (Double3.Distance(eye, coreEntity.Position) > Hands.ReachM + coreEntity.Definition.RadiusM) return VerbOutcome.OutOfReach;
                    coreDefinition = coreEntity.Definition;
                    coreItem = coreEntity.Item;
                    at = coreEntity.Position;
                    break;
                }
                case IntentMessage.TargetLying:
                {
                    if (!LyingThings.TryFind(World, intent.Lying, out at)) return VerbOutcome.NotThere;
                    LyingSite site = LyingSites.Of(World, intent.Lying);
                    coreDefinition = LyingProperties.DefinitionOf(intent.Lying.Kind, site);
                    if (Double3.Distance(eye, at) > Hands.ReachM + coreDefinition.RadiusM) return VerbOutcome.OutOfReach;
                    // A cobble of the litter is struck as what its place says it is (BF.1): its own mass by its stone, the shape it lies in.
                    coreItem = default;
                    coreItem.Resting = true;
                    coreItem.State = LyingProperties.StateOf(intent.Lying, site);
                    break;
                }
                case IntentMessage.TargetPlace:
                {
                    // A stone cannot be struck on itself: the core is another place's.
                    if (intent.Place == hands.Hand) return VerbOutcome.NotStone;
                    if (!hands.TryAt(intent.Place, out CarriedThing held)) return VerbOutcome.NotThere;
                    coreDefinition = held.Definition;
                    coreItem = held.Item;
                    corePlace = intent.Place;
                    at = Ahead(session, FlakeAheadM);
                    break;
                }
                default:
                    return VerbOutcome.NotNow;
            }
            StoneType coreStone = DefinitionCatalogue.StoneOf(coreDefinition);
            if (coreStone == null) return VerbOutcome.NotStone;

            StoneCore core = KnappingItems.CoreOf(coreDefinition, coreItem);
            double hammerMass = KnappingItems.MassOf(hammer.Definition, hammer.Item);
            double energy = Knapping.SwingEnergyJ(intent.WindUp01, hammerMass, session.Hydration.WorkCapacity01);
            KnapResult result = Knapping.Strike(core, DefinitionCatalogue.StoneOf(hammer.Definition), hammerMass, energy);
            note = result.Note;
            VerbOutcome outcome = KnappingItems.OutcomeOf(result.Outcome);
            if (result.Outcome == KnapOutcome.NoFracture) return outcome;

            // What the blow made of the core, written back to wherever the core is; a spent core is gone.
            ItemComponent after = KnappingItems.Struck(coreItem, core);
            if (coreEntity != null)
            {
                if (core.IsSpent) World.Entities.Kill(coreEntity);
                else coreEntity.SetItem(after, World.Tick);
            }
            else if (corePlace != 0)
            {
                if (core.IsSpent) hands.Discard(corePlace);
                else hands.TryUpdate(corePlace, after);
                SendCarrying(session);
            }
            else
            {
                // A cobble of the litter the blow changed leaves the layer for good (M1.5b's rule for a thing moved), and unless it
                // is spent it lies on as an item where it lay, with what the blow made of it.
                World.Taken.Take(intent.Lying);
                if (!core.IsSpent)
                {
                    Entity lying = World.Entities.Return(World.Entities.AllocateId(), coreDefinition, at, LyingThings.YawOf(intent.Lying, World.Loose.CellM), World.Tick, World.Tick);
                    after.Resting = true;
                    after.FallSpeed = 0f;
                    lying.SetItem(after, World.Tick);
                }
                BroadcastTaken(intent.Lying.Row, intent.Lying.Col);
            }

            if (result.Outcome == KnapOutcome.Flake)
            {
                // The flake comes away beside the core and falls from a put-down's height; off a held core it falls at the
                // founder's feet.
                Double3 where = corePlace != 0 ? at : FlakeFalls(at, session.Body.Feet, core.FlakesTaken);
                Entity flake = World.SpawnItem(DefinitionCatalogue.FlakeOf(coreStone), where.X, where.Z, World.GroundAt(where.X, where.Z) + Hands.ReleaseM, session.YawDeg);
                flake.SetItem(KnappingItems.FlakeOf(result), World.Tick);
            }
            return outcome;
        }

        /// <summary>How far from a lying core its flakes land, m: a step towards the founder, and to one side then the other as they come off.</summary>
        private const double FlakeTowardsM = 0.2, FlakeSideM = 0.1;

        /// <summary>
        /// Where a flake off a lying core lands: <see cref="FlakeTowardsM"/> towards the founder, so it is not under the core, and
        /// <see cref="FlakeSideM"/> to the right for the first flake, the left for the second and further out for each pair after,
        /// so a knapper's flakes lie scattered about the core rather than in one heap. The core's own place when the move would
        /// leave the region.
        /// </summary>
        private Double3 FlakeFalls(Double3 core, Double3 feet, int flakesTaken)
        {
            double dx = feet.X - core.X, dz = feet.Z - core.Z;
            double d = Math.Sqrt(dx * dx + dz * dz);
            if (d < 1e-6)
            {
                dx = 0.0;
                dz = 1.0;
                d = 1.0;
            }
            dx /= d;
            dz /= d;
            double side = FlakeSideM * ((flakesTaken + 1) / 2) * (flakesTaken % 2 == 1 ? 1.0 : -1.0);
            double east = core.X + dx * FlakeTowardsM - dz * side, north = core.Z + dz * FlakeTowardsM + dx * side;
            if (Math.Abs(east) > World.Region.HalfExtentM || Math.Abs(north) > World.Region.HalfExtentM) return core;
            return new Double3(east, World.GroundAt(east, north), north);
        }

        /// <summary>Where a founder's eye is, as the server holds their body: a verb's reach is measured from here.</summary>
        /// <summary>How far a founder may move from where a work began before it stops, m: a shuffle, not a step away.</summary>
        public const double WorkStillM = 0.5;

        /// <summary>
        /// A work begins (BF.2): the target is found as a knap's is (an item, one of the litter with what its place says of it, or a
        /// thing held in another place), the thing in hand is the tool, and <see cref="Work.Judge"/> says whether, how long and in
        /// what words. A work that cannot be done is answered with its reason and starts nothing; one that can replaces any work
        /// in progress and is answered with its seconds.
        /// </summary>
        private VerbOutcome StartWork(PlayerSession session, IntentMessage intent, Double3 eye, out string note, out float seconds)
        {
            note = string.Empty;
            seconds = 0f;
            Hands hands = session.Hands;
            Definition tool = null;
            ThingState toolState = default;
            if (hands.TryAt(hands.Hand, out CarriedThing inHand))
            {
                tool = inHand.Definition;
                toolState = inHand.Item.State;
            }
            Definition target;
            ThingState targetState;
            double cutStart = 0.0;
            // A work on the ground names a cell and nothing else does (BF.3).
            if (Work.IsGroundWork(intent.Kind) != (intent.Target == IntentMessage.TargetGround)) return VerbOutcome.NotNow;
            switch (intent.Target)
            {
                case IntentMessage.TargetEntity:
                {
                    if (!World.Entities.TryGet(intent.EntityId, out Entity e) || e.Killed || !e.HasItem) return VerbOutcome.NotThere;
                    if (Double3.Distance(eye, e.Position) > Hands.ReachM + e.Definition.RadiusM) return VerbOutcome.OutOfReach;
                    target = e.Definition;
                    targetState = e.Item.State;
                    break;
                }
                case IntentMessage.TargetLying:
                {
                    if (!LyingThings.TryFind(World, intent.Lying, out Double3 at)) return VerbOutcome.NotThere;
                    LyingSite site = LyingSites.Of(World, intent.Lying);
                    target = LyingProperties.DefinitionOf(intent.Lying.Kind, site);
                    if (Double3.Distance(eye, at) > Hands.ReachM + target.RadiusM) return VerbOutcome.OutOfReach;
                    targetState = LyingProperties.StateOf(intent.Lying, site);
                    break;
                }
                case IntentMessage.TargetPlace:
                {
                    if (intent.Place == hands.Hand || !hands.TryAt(intent.Place, out CarriedThing held)) return VerbOutcome.NotThere;
                    target = held.Definition;
                    targetState = held.Item.State;
                    break;
                }
                case IntentMessage.TargetTrunk:
                {
                    // A trunk of the stand (BF.3): found from the layer and the changes, refused when felled; met at the eye's height.
                    if (!StandingThings.TryFindTrunk(World, intent.Row, intent.Col, out StandingTrunk trunk)) return VerbOutcome.NotThere;
                    if (!TrunkWithinReach(eye, trunk)) return VerbOutcome.OutOfReach;
                    target = StandingThings.TrunkDefinition(trunk.Species);
                    targetState = StandingThings.TrunkState(trunk);
                    cutStart = SimMath.Clamp01(trunk.Cut / 255.0);
                    break;
                }
                case IntentMessage.TargetTuft:
                {
                    // A tuft of the understorey (BF.3): the tuft rule's own placement, refused when taken or its cell cleared.
                    if (!StandingThings.TryFindTuft(World, intent.Row, intent.Col, intent.Index, out StandingTuft tuft)) return VerbOutcome.NotThere;
                    if (Double3.Distance(eye, tuft.At) > Hands.ReachM + TuftReachM) return VerbOutcome.OutOfReach;
                    target = StandingThings.TuftDefinition(tuft.Shape, tuft.Species);
                    if (target == null) return VerbOutcome.NotThere;
                    targetState = StandingThings.TuftState(tuft);
                    break;
                }
                case IntentMessage.TargetGround:
                {
                    // A cell of the ground (BF.3): what clearing and digging name; judged by the ground's own model.
                    if (!Work.IsGroundWork(intent.Kind)) return VerbOutcome.NotNow;
                    if (!StandingThings.TryGround(World, intent.Row, intent.Col, out GroundSite site)) return VerbOutcome.NotThere;
                    if (Double3.Distance(eye, site.Centre) > Hands.ReachM + GroundReachM) return VerbOutcome.OutOfReach;
                    WorkOffer ground = Work.JudgeGround(intent.Kind, tool, toolState, site);
                    note = ground.Words;
                    if (!ground.Possible) return ground.Outcome;
                    if (session.Work != null) StopWork(session, "a new work began");
                    session.Work = new WorkInProgress
                    {
                        Kind = intent.Kind, Target = intent.Target, Row = intent.Row, Col = intent.Col,
                        ToolPlace = hands.Hand, Seconds = ground.Seconds, Progress = 0.0, StartedAt = session.Body.Feet, ToldTick = World.Tick, Words = ground.Words,
                    };
                    seconds = (float)ground.Seconds;
                    return VerbOutcome.Done;
                }
                default:
                    return VerbOutcome.NotNow;
            }
            if (Work.IsGroundWork(intent.Kind)) return VerbOutcome.NotNow;
            WorkOffer offer = Work.Judge(intent.Kind, tool, toolState, target, targetState);
            note = offer.Words;
            if (!offer.Possible) return offer.Outcome;
            if (session.Work != null) StopWork(session, "a new work began");
            session.Work = new WorkInProgress
            {
                Kind = intent.Kind, Target = intent.Target, EntityId = intent.EntityId, Lying = intent.Lying, Place = intent.Place,
                Row = intent.Row, Col = intent.Col, Index = intent.Index, CutStart01 = cutStart,
                ToolPlace = hands.Hand, Seconds = offer.Seconds, Progress = 0.0, StartedAt = session.Body.Feet, ToldTick = World.Tick, Words = offer.Words,
            };
            seconds = (float)offer.Seconds;
            return VerbOutcome.Done;
        }

        /// <summary>How much further than the reach a tuft's own place may lie: it spreads that far.</summary>
        private const double TuftReachM = 0.5;

        /// <summary>How much further than the reach a cell's centre may lie: the crosshair may meet the cell at its corner, 2.8 m off its centre at 4 m.</summary>
        private const double GroundReachM = 3.0;

        /// <summary>Whether a standing trunk is within reach of the eye: met at the eye's own height on its axis, and no further than its radius there.</summary>
        private static bool TrunkWithinReach(Double3 eye, in StandingTrunk trunk)
        {
            double trunkLength = TreeGeometries.TrunkLengthM(trunk.Geometry, trunk.HeightM);
            double atUp = Math.Max(trunk.Foot.Y, Math.Min(eye.Y, trunk.Foot.Y + trunkLength));
            Double3 at = new Double3(trunk.Foot.X, atUp, trunk.Foot.Z);
            double radius = TreeGeometries.TrunkRadiusAt(trunk.Geometry, trunk.HeightM, atUp - trunk.Foot.Y);
            return Double3.Distance(eye, at) <= Hands.ReachM + radius;
        }

        /// <summary>Drops a work in progress, telling the client why; false when there was none.</summary>
        private bool StopWork(PlayerSession session, string why)
        {
            WorkInProgress w = session.Work;
            if (w == null) return false;
            session.Work = null;
            SendWorkState(session, w, WorkStateMessage.Stopped, why);
            return true;
        }

        /// <summary>
        /// Every work in progress advanced by the step at the body's capacity (BF.2): stopped when the founder has moved, when the
        /// hand has changed or when the target is gone or out of reach; told once a second; finished when its seconds are done.
        /// </summary>
        private void AdvanceWork(double stepSeconds)
        {
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                PlayerSession s = _sessions[i];
                WorkInProgress w = s.Work;
                if (w == null) continue;
                if (!s.HasBody) { s.Work = null; continue; }
                if (Double3.Distance(s.Body.Feet, w.StartedAt) > WorkStillM) { StopWork(s, "you moved"); continue; }
                if (s.Hands.Hand != w.ToolPlace) { StopWork(s, "the hand changed"); continue; }
                if (!TargetStands(s, w)) { StopWork(s, "it is gone"); continue; }
                w.Progress += stepSeconds * s.Hydration.WorkCapacity01;
                if (w.Progress >= w.Seconds) FinishWork(s, w);
                else if (World.Tick - w.ToldTick >= _config.TickRate)
                {
                    w.ToldTick = World.Tick;
                    // A felling cut is kept in the cell as it goes (BF.3): a founder who stops, or leaves, resumes where it was.
                    if (w.Kind == WorkKind.CutTrunk && w.Target == IntentMessage.TargetTrunk)
                    {
                        double done = w.CutStart01 + (1.0 - w.CutStart01) * Math.Min(1.0, w.Seconds > 0.0 ? w.Progress / w.Seconds : 1.0);
                        World.Changes.SetTrunkCut(w.Row, w.Col, (byte)Math.Min(254, Math.Floor(done * 255.0)));
                        BroadcastChange(w.Row, w.Col);
                    }
                    SendWorkState(s, w, WorkStateMessage.Running, w.Words);
                }
            }
        }

        private bool TargetStands(PlayerSession s, WorkInProgress w)
        {
            Double3 eye = Eye(s);
            switch (w.Target)
            {
                case IntentMessage.TargetEntity:
                    return World.Entities.TryGet(w.EntityId, out Entity e) && !e.Killed && e.HasItem && Double3.Distance(eye, e.Position) <= Hands.ReachM + e.Definition.RadiusM;
                case IntentMessage.TargetLying:
                    return LyingThings.TryFind(World, w.Lying, out Double3 at) && Double3.Distance(eye, at) <= Hands.ReachM + 0.1;
                case IntentMessage.TargetPlace:
                    return w.Place != s.Hands.Hand && s.Hands.TryAt(w.Place, out _);
                case IntentMessage.TargetTrunk:
                    return StandingThings.TryFindTrunk(World, w.Row, w.Col, out StandingTrunk trunk) && TrunkWithinReach(eye, trunk);
                case IntentMessage.TargetTuft:
                    return StandingThings.TryFindTuft(World, w.Row, w.Col, w.Index, out StandingTuft tuft) && Double3.Distance(eye, tuft.At) <= Hands.ReachM + TuftReachM + 0.1;
                case IntentMessage.TargetGround:
                    return StandingThings.TryGround(World, w.Row, w.Col, out GroundSite site) && Double3.Distance(eye, site.Centre) <= Hands.ReachM + GroundReachM + 0.1
                           && (w.Kind != WorkKind.ClearGround || site.TuftsLeft > 0);
                default:
                    return false;
            }
        }

        /// <summary>
        /// A work's end (BF.2): judged again on what the world holds now and applied by <see cref="Work.Apply"/>, then committed —
        /// the target killed, changed or, for one of the litter, taken from the layer and lying on as an item where it lay; the tool
        /// spent or worn; cord put into the hand where the strip was; everything else made let go beside the target to fall — and
        /// told, with the words for what was made.
        /// </summary>
        private void FinishWork(PlayerSession session, WorkInProgress w)
        {
            session.Work = null;
            Hands hands = session.Hands;
            Definition tool = null;
            ThingState toolState = default;
            CarriedThing inHand = default;
            if (hands.TryAt(hands.Hand, out inHand))
            {
                tool = inHand.Definition;
                toolState = inHand.Item.State;
            }
            if (w.Target == IntentMessage.TargetGround)
            {
                FinishGroundWork(session, w, tool, toolState);
                return;
            }
            Definition target;
            ThingState targetState;
            Double3 at;
            Entity entity = null;
            CarriedThing held = default;
            StandingTrunk standing = default;
            Double3 line = default;
            if (w.Target == IntentMessage.TargetTrunk)
            {
                // The trunk as it stands now (BF.3); the fall's line runs from the founder through it.
                StandingThings.TryFindTrunk(World, w.Row, w.Col, out standing);
                target = StandingThings.TrunkDefinition(standing.Species);
                targetState = StandingThings.TrunkState(standing);
                at = standing.Foot;
                line = FallLine(session, standing.Foot);
            }
            else if (w.Target == IntentMessage.TargetTuft)
            {
                StandingThings.TryFindTuft(World, w.Row, w.Col, w.Index, out StandingTuft tuft);
                target = StandingThings.TuftDefinition(tuft.Shape, tuft.Species);
                targetState = StandingThings.TuftState(tuft);
                at = tuft.At;
            }
            else if (w.Target == IntentMessage.TargetEntity)
            {
                World.Entities.TryGet(w.EntityId, out entity);
                target = entity.Definition;
                targetState = entity.Item.State;
                at = entity.Position;
            }
            else if (w.Target == IntentMessage.TargetLying)
            {
                LyingThings.TryFind(World, w.Lying, out at);
                LyingSite site = LyingSites.Of(World, w.Lying);
                target = LyingProperties.DefinitionOf(w.Lying.Kind, site);
                targetState = LyingProperties.StateOf(w.Lying, site);
            }
            else
            {
                hands.TryAt(w.Place, out held);
                target = held.Definition;
                targetState = held.Item.State;
                at = Ahead(session, FlakeAheadM);
            }
            ulong salt = (ulong)World.Tick * 0x9E3779B97F4A7C15UL ^ session.SessionId ^ w.EntityId;
            WorkResult r = Work.Apply(w.Kind, tool, toolState, target, targetState, salt);

            if (w.Target == IntentMessage.TargetEntity)
            {
                if (r.TargetSpent) World.Entities.Kill(entity);
                else if (r.TargetChanged)
                {
                    ItemComponent item = entity.Item;
                    item.State = r.TargetAfter;
                    entity.SetItem(item, World.Tick);
                }
            }
            else if (w.Target == IntentMessage.TargetLying)
            {
                World.Taken.Take(w.Lying);
                BroadcastTaken(w.Lying.Row, w.Lying.Col);
                if (!r.TargetSpent)
                {
                    Entity lying = World.Entities.Return(World.Entities.AllocateId(), target, at, LyingThings.YawOf(w.Lying, World.Loose.CellM), World.Tick, World.Tick);
                    ItemComponent item = default;
                    item.Resting = true;
                    item.State = r.TargetChanged ? r.TargetAfter : targetState;
                    lying.SetItem(item, World.Tick);
                }
            }
            else if (w.Target == IntentMessage.TargetTrunk)
            {
                // What the work did to the trunk is a change of its cell (BF.3): its bark taken, or felled with its limbs and the cut through.
                if (r.TrunkFlags != 0)
                {
                    World.Changes.MarkTrunk(w.Row, w.Col, r.TrunkFlags);
                    if ((r.TrunkFlags & TrunkChange.Felled) != 0) World.Changes.SetTrunkCut(w.Row, w.Col, 255);
                    BroadcastChange(w.Row, w.Col);
                }
            }
            else if (w.Target == IntentMessage.TargetTuft)
            {
                if (r.TargetSpent)
                {
                    World.Changes.TakeTuft(w.Row, w.Col, w.Index);
                    BroadcastChange(w.Row, w.Col);
                }
            }
            else
            {
                if (r.TargetSpent) hands.Discard(w.Place);
                else if (r.TargetChanged)
                {
                    ItemComponent item = held.Item;
                    item.State = r.TargetAfter;
                    hands.TryUpdate(w.Place, item);
                }
            }
            byte toolPlace = hands.Hand;
            if (tool != null)
            {
                if (r.ToolSpent) hands.Discard(toolPlace);
                else if (r.ToolChanged)
                {
                    ItemComponent item = inHand.Item;
                    item.State = r.ToolAfter;
                    hands.TryUpdate(toolPlace, item);
                }
            }
            for (int i = 0; i < r.Made.Count; i++)
            {
                MadeThing made = r.Made[i];
                if (made.Definition.Substance == Substance.Cord && toolPlace != 0)
                {
                    ItemComponent item = default;
                    item.Resting = true;
                    item.State = made.State;
                    hands.Put(toolPlace, new CarriedThing { Id = World.Entities.AllocateId(), Definition = made.Definition, SpawnTick = World.Tick, Place = toolPlace, Item = item });
                    continue;
                }
                double east, north;
                if (made.AlongM != 0.0 || made.AcrossM != 0.0)
                {
                    // Down the fall's line from the stump (BF.3): the logs along it, the limbs either side.
                    east = at.X + line.X * made.AlongM + line.Z * made.AcrossM;
                    north = at.Z + line.Z * made.AlongM - line.X * made.AcrossM;
                }
                else if (w.Target == IntentMessage.TargetTrunk)
                {
                    // Round the trunk's foot (BF.3): the strips off a big tree are dozens, and lie in a ring at its bark.
                    double ring = TreeGeometries.TrunkRadiusAt(standing.Geometry, standing.HeightM, 0.0) + 0.4;
                    double angle = 2.0 * Math.PI * i / Math.Max(1, r.Made.Count);
                    east = at.X + ring * Math.Cos(angle);
                    north = at.Z + ring * Math.Sin(angle);
                }
                else
                {
                    east = at.X + 0.25 * (i % 3 - 1);
                    north = at.Z + 0.25 * (i / 3);
                }
                Entity e = World.SpawnItem(made.Definition, east, north, World.GroundAt(east, north) + Hands.ReleaseM, session.YawDeg);
                ItemComponent fall = default;
                fall.Resting = false;
                fall.State = made.State;
                e.SetItem(fall, World.Tick);
            }
            SendWorkState(session, w, WorkStateMessage.Done, r.Words);
            SendCarrying(session);
        }

        /// <summary>The line a tree falls along (BF.3): away from the founder who cut it, level; the way they face when they stand at its foot.</summary>
        private Double3 FallLine(PlayerSession session, Double3 foot)
        {
            double dx = foot.X - session.Body.Feet.X, dz = foot.Z - session.Body.Feet.Z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length < 0.05)
            {
                double yaw = session.YawDeg * Math.PI / 180.0;
                return new Double3(Math.Sin(yaw), 0.0, Math.Cos(yaw));
            }
            return new Double3(dx / length, 0.0, dz / length);
        }

        /// <summary>
        /// A work on the ground's end (BF.3): judged again on the cell as it is now and applied by <see cref="Work.ApplyGround"/>, then
        /// committed as the cell's change — its tufts taken, cleared, dug — told to every client, with the bundles where their tufts
        /// stood and a tuber beside the hole.
        /// </summary>
        private void FinishGroundWork(PlayerSession session, WorkInProgress w, Definition tool, in ThingState toolState)
        {
            if (!StandingThings.TryGround(World, w.Row, w.Col, out GroundSite site))
            {
                SendWorkState(session, w, WorkStateMessage.Stopped, "it is gone");
                return;
            }
            ulong salt = (ulong)World.Tick * 0x9E3779B97F4A7C15UL ^ session.SessionId ^ ((ulong)(uint)w.Row << 32 | (uint)w.Col);
            GroundResult r = Work.ApplyGround(w.Kind, tool, toolState, site, salt);
            bool changed = false;
            for (int k = 0; k < WorldChanges.MostTufts; k++)
                if ((r.TuftsTaken & (1 << k)) != 0 && World.Changes.TakeTuft(w.Row, w.Col, k)) changed = true;
            if (r.Cleared && !site.Cleared) { World.Changes.Clear(w.Row, w.Col); changed = true; }
            if (r.DugCm > site.DugCm) { World.Changes.Dig(w.Row, w.Col, r.DugCm); changed = true; }
            if (changed) BroadcastChange(w.Row, w.Col);
            Double3 at = site.Centre;
            for (int i = 0; i < r.Made.Count; i++)
            {
                MadeThing made = r.Made[i];
                double east = at.X + made.AcrossM + (made.AlongM == 0.0 && made.AcrossM == 0.0 ? 0.25 * (i % 3 - 1) : 0.0);
                double north = at.Z + made.AlongM + (made.AlongM == 0.0 && made.AcrossM == 0.0 ? 0.25 * (i / 3) : 0.0);
                Entity e = World.SpawnItem(made.Definition, east, north, World.GroundAt(east, north) + Hands.ReleaseM, session.YawDeg);
                ItemComponent fall = default;
                fall.Resting = false;
                fall.State = made.State;
                e.SetItem(fall, World.Tick);
            }
            SendWorkState(session, w, WorkStateMessage.Done, r.Words);
            SendCarrying(session);
        }

        private void SendWorkState(PlayerSession session, WorkInProgress w, byte ended, string note)
        {
            WorkStateMessage m;
            m.Kind = w.Kind;
            m.Progress01 = (float)Math.Min(1.0, w.Seconds > 0.0 ? w.Progress / w.Seconds : 1.0);
            m.SecondsLeft = (float)Math.Max(0.0, (w.Seconds - w.Progress) / Math.Max(0.05, session.Hydration.WorkCapacity01));
            m.Ended = ended;
            m.Note = note ?? string.Empty;
            _writer.Reset();
            m.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
        }

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
        private void BroadcastTaken(int row, int col) => BroadcastChange(row, col);

        /// <summary>A cell's changes, every layer at once (BF.3), to every client that has its snapshot; a joiner is sent the whole set with its snapshot.</summary>
        private void BroadcastChange(int row, int col)
        {
            if (!World.Changes.TryGet(row, col, out CellChange cell)) return;
            ChangesMessage m;
            m.Cells = new[] { cell };
            _writer.Reset();
            m.Write(_writer);
            for (int i = 0; i < _sessions.Count; i++)
                if (!_sessions[i].SnapshotPending) _sessions[i].Connection.Send(_writer.Written, Delivery.Reliable);
        }

        /// <summary>Every change to the world so far, to a joiner, in messages of at most <see cref="ChangesMessage.MostCells"/> cells.</summary>
        private void SendTaken(PlayerSession joiner)
        {
            List<CellChange> cells = World.Changes.AllCells();
            for (int start = 0; start < cells.Count; start += ChangesMessage.MostCells)
            {
                ChangesMessage m;
                m.Cells = cells.GetRange(start, Math.Min(ChangesMessage.MostCells, cells.Count - start)).ToArray();
                _writer.Reset();
                m.Write(_writer);
                joiner.Connection.Send(_writer.Written, Delivery.Reliable);
            }
        }

        /// <summary>
        /// A player asking for developer mode on or off (M1.E, CANON ruling 39, the player's F2). A server that may grant it
        /// (its development mark) grants what was asked; one that may not refuses, and keeps the player, since F2 is a key
        /// every game answers. Either way the player is told what they now have, so the switch can say so.
        /// </summary>
        private void HandleDeveloperMode(PlayerSession session, DeveloperModeMessage asked)
        {
            bool mayGrant = _config.Movement.AllowFlight;
            session.DeveloperMode = mayGrant && asked.On;
            DeveloperModeMessage answer;
            answer.On = session.DeveloperMode;
            answer.Refused = asked.On && !mayGrant;
            _writer.Reset();
            answer.Write(_writer);
            session.Connection.Send(_writer.Written, Delivery.Reliable);
        }

        /// <summary>
        /// A developer's setting (M1.D, CANON ruling 30): taken on a development server alone, held to its table's range and
        /// applied to what owns it. A server not started for development refuses it and closes, as it does a malformed
        /// message, since a client that sends one to such a server is not the game's; a name this build's table lacks is
        /// refused the same way. The table (<see cref="DevSettings"/>) is the one owner of what exists; this is the one owner
        /// of what each does.
        /// </summary>
        private void HandleDevSetting(PlayerSession session, DevSettingMessage setting)
        {
            if (!_config.Movement.AllowFlight)
            {
                Refuse(session.Connection, "a developer's setting (" + setting.Name + ") on a server not started for development");
                return;
            }
            // A development server takes a setting only from a player who has switched developer mode on (M1.E). One that
            // has not is let be rather than closed: a setting can be sent a moment before the switch's own answer arrives,
            // and the client of this game is the only thing that sends one at all.
            if (!session.DeveloperMode) return;
            DevSetting known = DevSettings.Find(setting.Name);
            if (known == null)
            {
                Refuse(session.Connection, "a developer's setting this build does not know: " + setting.Name);
                return;
            }
            double value = known.IsDeed ? 0.0 : DevSettings.Held(known, setting.Value);
            switch (setting.Name)
            {
                case DevSettings.AnimalsStandUpM:
                case DevSettings.AnimalsTakeAwayM:
                {
                    AnimalStandUp animals = Animals();
                    if (animals == null) break;
                    if (setting.Name == DevSettings.AnimalsStandUpM) animals.StandUpRadiusM = value;
                    else animals.TakeAwayRadiusM = value;
                    // A group on the edge is not stood up and taken away by turns: the take-away is never nearer than the stand-up.
                    animals.TakeAwayRadiusM = Math.Max(animals.TakeAwayRadiusM, animals.StandUpRadiusM);
                    break;
                }
                case DevSettings.KangarooFleeWithinM:
                case DevSettings.KangarooRunM:
                case DevSettings.KangarooRunMs:
                case DevSettings.OystercatcherFleeWithinM:
                case DevSettings.OystercatcherRunM:
                case DevSettings.OystercatcherRunMs:
                {
                    AnimalStandUp animals = Animals();
                    bool kangaroo = setting.Name.StartsWith("animals.kangaroo.", StringComparison.Ordinal);
                    AnimalFlightRules rules = animals?.RulesFor(kangaroo ? AnimalSpecies.EasternGreyKangaroo : AnimalSpecies.PiedOystercatcher);
                    if (rules == null) break;
                    if (setting.Name.EndsWith(".flee_within_m", StringComparison.Ordinal)) rules.FleeWithinM = value;
                    else if (setting.Name.EndsWith(".run_m", StringComparison.Ordinal)) rules.RunM = value;
                    else rules.RunMs = value;
                    break;
                }
                case DevSettings.ClockLocalHour:
                {
                    // The same local day at the region's centre, at the hour asked for.
                    double longitude = World.Region.CentreLongitudeDeg;
                    double dayStart = Math.Floor(World.Clock.LocalHours(longitude) / 24.0) * 24.0;
                    World.Clock.SetTotalHours(dayStart + value - WorldClock.OffsetHours(longitude));
                    break;
                }
                case DevSettings.ClockDayOfYear:
                {
                    // The day asked for, of the clock's first year, at the same local hour at the region's centre.
                    double longitude = World.Region.CentreLongitudeDeg;
                    double hour = World.Clock.LocalHourOfDay(longitude);
                    World.Clock.SetTotalHours((Math.Round(value) - 1.0) * 24.0 + hour - WorldClock.OffsetHours(longitude));
                    break;
                }
                case DevSettings.ClockScale:
                    World.Clock.Scale = value;
                    break;
                case DevSettings.FounderWater:
                    session.Hydration.Restore(value);
                    SendFounderState(session);
                    break;
                case DevSettings.FounderCoreC:
                    // Below the lethal core, the next step's AdvanceFounders is the death: one place decides it.
                    session.Warmth.Restore(value);
                    SendFounderState(session);
                    break;
                case DevSettings.SpawnStick:
                case DevSettings.SpawnCobble:
                case DevSettings.SpawnSilcreteCobble:
                {
                    Definition definition = setting.Name == DevSettings.SpawnStick ? DefinitionCatalogue.Stick
                        : setting.Name == DevSettings.SpawnCobble ? DefinitionCatalogue.Cobble
                        : DefinitionCatalogue.CobbleOf(StoneType.Silcrete);
                    Ahead(session, out double east, out double north);
                    World.SpawnItem(definition, east, north, null, session.YawDeg);
                    break;
                }
                case DevSettings.SpawnKeenFlake:
                case DevSettings.SpawnChopper:
                {
                    // The panel's edges (BF.3): a keen flake as a good blow leaves one, and a chopper, a heavy keen core, which the
                    // hands have no way to make yet but a scenario needs to fell a tree in minutes rather than days.
                    bool chopper = setting.Name == DevSettings.SpawnChopper;
                    Definition definition = chopper ? DefinitionCatalogue.CobbleOf(StoneType.Silcrete) : DefinitionCatalogue.FlakeOf(StoneType.Silcrete);
                    Ahead(session, out double east, out double north);
                    Entity edge = World.SpawnItem(definition, east, north, null, session.YawDeg);
                    ItemComponent item = edge.Item;
                    item.State.SetMass(chopper ? 1.5f : 0.03f);
                    item.State.SetEdge(chopper ? 0.8f : 0.7f);
                    edge.SetItem(item, World.Tick);
                    break;
                }
                case DevSettings.SpawnKangaroo:
                case DevSettings.SpawnOystercatcher:
                {
                    // One animal to look at (M1.7b): stood the same two metres ahead as a stick, but broadside to the
                    // founder, because what names a kangaroo is its flank and not its face.
                    AnimalStandUp animals = Animals();
                    if (animals == null) break;
                    AnimalSpecies species = setting.Name == DevSettings.SpawnKangaroo
                        ? AnimalSpecies.EasternGreyKangaroo
                        : AnimalSpecies.PiedOystercatcher;
                    Ahead(session, out double east, out double north);
                    animals.SetDown(World, species, east, north, (float)Mod(session.YawDeg + 90.0, 360.0));
                    break;
                }
                case DevSettings.AnimalsSetDownPose:
                {
                    AnimalStandUp animals = Animals();
                    animals?.PoseSetDown(World, (byte)Math.Round(value));
                    break;
                }
                case DevSettings.StandAtWake:
                {
                    // Stood as a correction stands a founder: the body the server holds, sent back to the client to take.
                    Double3 wake = World.SpawnPoint();
                    session.Body = MoverState.AtRest(wake.X, wake.Y, wake.Z);
                    session.Body.Grounded = true;
                    session.StoodUp = wake.Y;
                    session.HasBody = true;
                    session.LastMoveTick = World.Tick;
                    CorrectionMessage stood;
                    stood.Sequence = session.LastSequence;
                    stood.ServerTick = World.Tick;
                    stood.Body = session.Body;
                    stood.Reason = "stood at the wake by the developer's panel";
                    _writer.Reset();
                    stood.Write(_writer);
                    session.Connection.Send(_writer.Written, Delivery.Reliable);
                    break;
                }
            }
            DevSettingApplied?.Invoke(session, setting);
        }

        /// <summary>How far ahead of a founder a developer's spawn is set down, m.</summary>
        private const double SpawnAheadM = 2.0;

        /// <summary>
        /// Where a developer's spawn goes: <see cref="SpawnAheadM"/> ahead of the founder who asked, and at the founder
        /// when that would be off the region. One owner for the two deeds that set something down (M1.7b took the second).
        /// </summary>
        private void Ahead(PlayerSession session, out double east, out double north)
        {
            Double3 at = Ahead(session, SpawnAheadM);
            east = at.X;
            north = at.Z;
        }

        /// <summary>A point on the ground a distance ahead of the founder the way they face, and at their feet when that would be off the region.</summary>
        private Double3 Ahead(PlayerSession session, double metres)
        {
            Double3 at = session.HasBody ? session.Body.Feet : World.SpawnPoint();
            double yaw = session.YawDeg * GeoMath.DegToRad;
            double east = at.X + metres * Math.Sin(yaw), north = at.Z + metres * Math.Cos(yaw);
            if (Math.Abs(east) > World.Region.HalfExtentM || Math.Abs(north) > World.Region.HalfExtentM)
            {
                east = at.X;
                north = at.Z;
            }
            return new Double3(east, World.GroundAt(east, north), north);
        }

        /// <summary>A number folded onto [0, period): a bearing that stays a bearing however it is added to.</summary>
        private static double Mod(double v, double period)
        {
            double r = v % period;
            return r < 0.0 ? r + period : r;
        }

        /// <summary>The system that stands the animals up, for a developer's setting to move and a host to hear its flights; null on a world without one.</summary>
        public AnimalStandUp Animals()
        {
            foreach (IFastSystem system in World.Systems)
                if (system is AnimalStandUp animals) return animals;
            return null;
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
