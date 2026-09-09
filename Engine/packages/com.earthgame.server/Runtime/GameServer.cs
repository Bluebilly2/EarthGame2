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

        /// <summary>Every body the server knows by name: the remembered ones, overlaid by the live sessions'.</summary>
        public IReadOnlyList<SavedPlayer> PlayersToSave()
        {
            Dictionary<string, SavedPlayer> all = new Dictionary<string, SavedPlayer>(_savedPlayers, StringComparer.Ordinal);
            for (int i = 0; i < _sessions.Count; i++)
                if (_sessions[i].HasBody) all[_sessions[i].Name] = SavedOf(_sessions[i]);
            return new List<SavedPlayer>(all.Values);
        }

        /// <summary>The world's name: clock, tick and every body by player name (ARCHITECTURE §6).</summary>
        public string Digest()
        {
            List<KeyValuePair<string, MoverState>> bodies = new List<KeyValuePair<string, MoverState>>();
            foreach (SavedPlayer p in PlayersToSave()) bodies.Add(new KeyValuePair<string, MoverState>(p.Name, p.Body));
            return WorldDigest.World(World, bodies);
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
