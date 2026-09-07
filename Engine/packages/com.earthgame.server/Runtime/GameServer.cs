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
    /// the caller of <see cref="Update"/> differ (plan §4.6, Rule 1: there is no single player).
    ///
    /// <para>Pause is a server concept: while <see cref="Paused"/>, the loop still pumps the transport (so a
    /// paused host still answers pings and refuses late joiners cleanly) but releases no world steps.</para>
    ///
    /// <para>Movement is client-authoritative and server-validated: a PlayerMove is checked by
    /// <see cref="MovementValidator"/> against the last accepted body, the heightfield and the region's edge, and
    /// a violation is answered with a Correction to where the server holds the player. Accepted bodies are sent
    /// to every other session after each tick.</para>
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
        private uint _nextSessionId = 1;

        public GameServer(ServerConfig config, IServerTransport transport, WorldState world)
        {
            _config = config ?? new ServerConfig();
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            World = world ?? throw new ArgumentNullException(nameof(world));
            _accumulator = new FixedStepAccumulator(1.0 / _config.TickRate, _config.MaxStepsPerUpdate);
        }

        /// <summary>The world this server is authoritative for.</summary>
        public WorldState World { get; }

        /// <summary>The configuration the server was started with; the mover numbers in it are what clients must use.</summary>
        public ServerConfig Config => _config;

        /// <summary>Connected players, in join order.</summary>
        public IReadOnlyList<PlayerSession> Sessions => _sessions;

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

        /// <summary>Starts listening. The world exists before the first player does.</summary>
        public void Listen(int port) => _transport.Listen(port);

        /// <summary>
        /// One host update: pumps the transport, handles every pending event, then releases as many fixed
        /// steps as the elapsed real time owes (bounded). Call from the host's loop with the real seconds since
        /// the previous call; the time scale, when it exists, multiplies here and nowhere else.
        /// </summary>
        public void Update(double realSecondsElapsed)
        {
            _transport.Update();
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
            if (stepped) BroadcastBodies();
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
                    {
                        _sessionsByConnection.Remove(evt.Connection.Id);
                        _sessions.Remove(gone);
                        SessionLeft?.Invoke(gone, evt.Reason ?? string.Empty);
                    }
                    break;
            }
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
                        HandlePlayerMove(session, move);
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
            if (_sessions.Count >= _config.MaxPlayers)
            {
                Refuse(connection, "server is full (" + _config.MaxPlayers + " players)");
                return;
            }

            PlayerSession session = new PlayerSession(_nextSessionId++, name, connection, World.Tick);
            _sessionsByConnection[connection.Id] = session;
            _sessions.Add(session);

            Double3 spawn = World.SpawnPoint();
            WelcomeMessage welcome;
            welcome.SessionId = session.SessionId;
            welcome.Seed = World.Seed;
            welcome.RegionId = World.RegionId;
            welcome.TotalHours = World.Clock.TotalHours;
            welcome.Tick = World.Tick;
            welcome.TickRate = (byte)_config.TickRate;
            welcome.SpawnEast = spawn.X;
            welcome.SpawnUp = spawn.Y;
            welcome.SpawnNorth = spawn.Z;
            _writer.Reset();
            welcome.Write(_writer);
            connection.Send(_writer.Written, Delivery.Reliable);
            SessionJoined?.Invoke(session);
        }

        private void HandlePlayerMove(PlayerSession session, PlayerMoveMessage move)
        {
            double interval = session.HasBody ? (World.Tick - session.LastMoveTick) * _accumulator.StepSeconds : 0.0;
            string reason = MovementValidator.Check(session.Body, session.HasBody, move.Body, interval, World.Terrain,
                                                    World.Region.HalfExtentM, _config.Mover, _config.Movement);
            if (reason == null)
            {
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

        private void BroadcastBodies()
        {
            if (_sessions.Count < 2) return;
            for (int i = 0; i < _sessions.Count; i++)
            {
                PlayerSession subject = _sessions[i];
                if (!subject.HasBody) continue;
                PlayerStateMessage state;
                state.SessionId = subject.SessionId;
                state.Sequence = subject.LastSequence;
                state.ServerTick = World.Tick;
                state.YawDeg = subject.YawDeg;
                state.PitchDeg = subject.PitchDeg;
                state.Body = subject.Body;
                _writer.Reset();
                state.Write(_writer);
                for (int j = 0; j < _sessions.Count; j++)
                {
                    if (j == i) continue;
                    _sessions[j].Connection.Send(_writer.Written, Delivery.Unreliable);
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
