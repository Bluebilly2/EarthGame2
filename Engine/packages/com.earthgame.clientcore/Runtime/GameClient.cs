using System;
using System.Collections.Generic;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Transport;

namespace EarthGame.ClientCore
{
    /// <summary>Where a client is in its life. Transitions only move forward except through Disconnect.</summary>
    public enum ClientState : byte
    {
        Disconnected = 0,
        Connecting = 1,
        Handshaking = 2,
        Connected = 3,
        Refused = 4,
    }

    /// <summary>
    /// The engine-free client: it owns the connection, speaks the handshake, asks for the tiles around its
    /// spawn, reports its own body's movement and holds what the server has told it (the other players' bodies
    /// as mirrors, the tiles, and any correction of its own). It never simulates the world as truth. The Unity
    /// client wraps this and draws what it holds.
    ///
    /// <para>Interactive (N1) is a state of this object: the Welcome's nine tiles answered, the snapshot that
    /// followed the Welcome applied. The wrapper adds "collidable ground under the player" when it has built it.</para>
    /// </summary>
    public sealed class GameClient
    {
        private readonly IClientTransport _transport;
        private readonly ITileCache _tileCache;
        private readonly PacketWriter _writer = new PacketWriter(256);
        private readonly Dictionary<uint, PlayerStateMessage> _others = new Dictionary<uint, PlayerStateMessage>();
        private readonly Dictionary<uint, RemoteMirror> _mirrors = new Dictionary<uint, RemoteMirror>();
        private readonly HashSet<(TileLayer Layer, TileId Id)> _outstanding = new HashSet<(TileLayer, TileId)>();
        private readonly HashSet<(TileLayer Layer, TileId Id)> _requested = new HashSet<(TileLayer, TileId)>();
        private readonly HashSet<TileLayer> _unserved = new HashSet<TileLayer>();
        private string _playerName;
        private string _password;
        private uint _sequence;
        private long _lastUpdateMs = long.MinValue;
        private long _tickObservedAtMs = long.MinValue;

        public GameClient(IClientTransport transport, ITileCache tileCache = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _tileCache = tileCache ?? new NoTileCache();
        }

        public ClientState State { get; private set; } = ClientState.Disconnected;

        /// <summary>What the server said at Welcome; valid once <see cref="State"/> is Connected.</summary>
        public WelcomeMessage Welcome { get; private set; }

        /// <summary>The region's tile grid, from the extent the Welcome carries; null until then.</summary>
        public TileGrid Grid { get; private set; }

        /// <summary>The tiles received and held; created at Welcome.</summary>
        public TileReceiver Tiles { get; private set; }

        /// <summary>Tiles asked for and not yet answered (held, refused or failed).</summary>
        /// <summary>Ground tiles asked for and not yet answered. The water layers travel beside them and gate nothing.</summary>
        public int TilesOutstanding
        {
            get
            {
                int n = 0;
                foreach (var key in _outstanding) if (key.Layer == TileLayer.Ground) n++;
                return n;
            }
        }

        /// <summary>Tiles of any layer asked for and not yet answered, for a harness watching the whole stream.</summary>
        public int TilesOutstandingOfEveryLayer => _outstanding.Count;

        /// <summary>True once the tiles around the spawn have been asked for.</summary>
        public bool TilesRequested { get; private set; }

        /// <summary>True once the snapshot that follows the Welcome has been applied.</summary>
        public bool SnapshotApplied { get; private set; }

        /// <summary>Tiles answered and snapshot applied: what the client core can say of N1's "interactive".</summary>
        /// <summary>
        /// What CANON ruling 11 (N1) measures: the ground under the founder answered, the snapshot applied. The
        /// water layers of M1.4b arrive beside the ground and are deliberately not part of it, so the number this
        /// reports means the same thing after that slice as before it.
        /// </summary>
        public bool IsInteractive => State == ClientState.Connected && SnapshotApplied && TilesRequested && TilesOutstanding == 0;

        /// <summary>Why the server said no, or why the link dropped. Empty while connected.</summary>
        public string LastReason { get; private set; } = string.Empty;

        /// <summary>Round trip of the most recent Pong, in milliseconds; negative until one arrives.</summary>
        public long LastRttMs { get; private set; } = -1;

        /// <summary>The newest server tick seen in any message (Pong, PlayerState, SnapshotEnd); negative until one arrives.</summary>
        public long LastServerTick { get; private set; } = -1;

        /// <summary>How many ticks behind the estimated server tick a mirror is sampled, so a state is usually held either side.</summary>
        public int MirrorDelayTicks = 3;

        /// <summary>The most recent correction of this client's own body, and how many there have been.</summary>
        public CorrectionMessage LastCorrection { get; private set; }
        public int CorrectionCount { get; private set; }

        /// <summary>The other players' bodies as the server last sent them, by session id.</summary>
        public IReadOnlyDictionary<uint, PlayerStateMessage> Others => _others;

        /// <summary>The other players as mirrors, by session id.</summary>
        public IReadOnlyDictionary<uint, RemoteMirror> Mirrors => _mirrors;

        /// <summary>The entities the server has shown this client (M1.3).</summary>
        public EntityMirror Entities { get; } = new EntityMirror();

        /// <summary>Payload bytes this connection has sent and received, or zero before it exists.</summary>
        public long BytesSent => _transport.Connection != null ? _transport.Connection.BytesSent : 0;
        public long BytesReceived => _transport.Connection != null ? _transport.Connection.BytesReceived : 0;

        public event Action<WelcomeMessage> Welcomed;
        public event Action<string> Dropped;
        public event Action<CorrectionMessage> Corrected;
        public event Action<PlayerStateMessage> PlayerStateReceived;
        public event Action<uint> PlayerLeft;
        public event Action<ReceivedTile> TileReady;
        public event Action<TileLayer, TileId, string> TileFailed;
        public event Action SnapshotEnded;

        /// <summary>Opens the connection; the Hello is sent when the transport reports Connected.</summary>
        public void Connect(string address, int port, string playerName, string password)
        {
            _playerName = playerName ?? string.Empty;
            _password = password ?? string.Empty;
            LastReason = string.Empty;
            State = ClientState.Connecting;
            _transport.Connect(address, port);
        }

        /// <summary>Sends a Ping carrying the caller's clock so the answer can be timed by the same clock.</summary>
        public void Ping(long clientTimeMs)
        {
            if (State != ClientState.Connected) return;
            PingMessage ping;
            ping.ClientTimeMs = clientTimeMs;
            _writer.Reset();
            ping.Write(_writer);
            _transport.Connection.Send(_writer.Written, Delivery.Unreliable);
        }

        /// <summary>
        /// Reports the input applied since the last report and the body it produced. Unreliable and unordered on
        /// the wire; the sequence number returned is what a Correction will name.
        /// </summary>
        public uint SendMove(in MoverInput input, float yawDeg, float pitchDeg, in MoverState body)
        {
            if (State != ClientState.Connected) return 0;
            PlayerMoveMessage move;
            move.Sequence = ++_sequence;
            move.Input = input;
            move.YawDeg = yawDeg;
            move.PitchDeg = pitchDeg;
            move.Body = body;
            _writer.Reset();
            move.Write(_writer);
            _transport.Connection.Send(_writer.Written, Delivery.Unreliable);
            return move.Sequence;
        }

        /// <summary>
        /// Asks for the tiles around a point that have not been asked for yet, with the checksums the cache
        /// holds. Called at Welcome for the spawn; a wrapper may call it again as the player moves.
        /// </summary>
        public int RequestTilesAround(double east, double north)
        {
            if (State != ClientState.Connected || Grid == null || Tiles == null) return 0;
            IReadOnlyList<TileId> around = Grid.Around(Grid.ForPosition(east, north));
            int asked = 0;
            foreach (TileLayer layer in TileLayers.All)
            {
                // A layer this world has none of answers every tile with a refusal; asking once is how the client
                // finds that out, and asking again for every tile it walks onto is how it would waste the wire.
                if (_unserved.Contains(layer)) continue;
                List<TileId> fresh = new List<TileId>(around.Count);
                for (int i = 0; i < around.Count; i++)
                    if (_requested.Add((layer, around[i]))) fresh.Add(around[i]);
                if (fresh.Count == 0) continue;
                TileRequestMessage request;
                request.Wants = Tiles.WantsFor(layer, fresh);
                PacketWriter w = new PacketWriter(16 + fresh.Count * 16);
                request.Write(w);
                _transport.Connection.Send(w.Written, Delivery.Reliable);
                for (int i = 0; i < fresh.Count; i++) _outstanding.Add((layer, fresh[i]));
                if (layer == TileLayer.Ground) asked = fresh.Count;
            }
            TilesRequested = true;
            return asked;
        }

        /// <summary>The server's tick now, as this client estimates it from the newest tick seen and the time since.</summary>
        public double EstimatedServerTick(long clientTimeMs)
        {
            if (LastServerTick < 0) return 0.0;
            return LastServerTick + (clientTimeMs - _tickObservedAtMs) * Welcome.TickRate / 1000.0;
        }

        /// <summary>A remote player's drawn position now: its mirror sampled the delay behind the estimated tick.</summary>
        public bool TrySampleMirror(uint sessionId, long clientTimeMs, out MirrorSample sample)
        {
            RemoteMirror mirror;
            if (!_mirrors.TryGetValue(sessionId, out mirror) || mirror.Count == 0)
            {
                sample = default;
                return false;
            }
            sample = mirror.Sample(EstimatedServerTick(clientTimeMs) - MirrorDelayTicks);
            return true;
        }

        /// <summary>The name of a mirror's newest body, computed exactly as the server names its own record of it.</summary>
        public string MirrorDigest(uint sessionId)
        {
            RemoteMirror mirror;
            if (!_mirrors.TryGetValue(sessionId, out mirror) || mirror.Count == 0) return string.Empty;
            return WorldDigest.Bodies(new[] { new KeyValuePair<uint, MoverState>(sessionId, mirror.Latest.Body) });
        }

        /// <summary>Pumps the transport and handles everything it reports. Call once per frame.</summary>
        /// <param name="clientTimeMs">The caller's clock now, used to time Pongs and to meter the transport.</param>
        public void Update(long clientTimeMs)
        {
            double elapsed = _lastUpdateMs == long.MinValue ? 0.0 : Math.Max(0.0, (clientTimeMs - _lastUpdateMs) / 1000.0);
            _lastUpdateMs = clientTimeMs;
            _transport.Update(elapsed);
            TransportEvent evt;
            while (_transport.Poll(out evt))
            {
                switch (evt.Kind)
                {
                    case TransportEventKind.Connected:
                        State = ClientState.Handshaking;
                        SendHello();
                        break;
                    case TransportEventKind.Data:
                        HandleData(evt.Data, evt.Offset, evt.Count, clientTimeMs);
                        break;
                    case TransportEventKind.Disconnected:
                        if (State == ClientState.Refused) break;
                        // A server that says no sends Refused and then closes with the same reason. Over UDP the
                        // close can overtake the message, so a peer-stated close during the handshake IS the
                        // refusal; a close without a stated reason (a timeout, a dead socket) is a drop.
                        State = State == ClientState.Handshaking && evt.ReasonFromPeer
                            ? ClientState.Refused
                            : ClientState.Disconnected;
                        LastReason = evt.Reason ?? string.Empty;
                        Dropped?.Invoke(LastReason);
                        break;
                }
            }
        }

        public void Disconnect(string reason)
        {
            if (_transport.Connection != null) _transport.Connection.Close(reason);
            State = ClientState.Disconnected;
            LastReason = reason ?? string.Empty;
        }

        private void SendHello()
        {
            HelloMessage hello;
            hello.ProtocolVersion = ProtocolInfo.Version;
            hello.PlayerName = _playerName;
            hello.Password = _password;
            _writer.Reset();
            hello.Write(_writer);
            _transport.Connection.Send(_writer.Written, Delivery.Reliable);
        }

        private void ObserveTick(long serverTick, long clientTimeMs)
        {
            if (serverTick <= LastServerTick) return;
            LastServerTick = serverTick;
            _tickObservedAtMs = clientTimeMs;
        }

        private void HandleData(byte[] data, int offset, int count, long clientTimeMs)
        {
            MessageKind kind = MessageHeader.PeekKind(data, offset, count);
            try
            {
                PacketReader reader = new PacketReader(data, offset, count);
                reader.ReadByte();
                switch (kind)
                {
                    case MessageKind.Welcome:
                        Welcome = WelcomeMessage.Read(reader);
                        reader.ExpectEnd();
                        State = ClientState.Connected;
                        ObserveTick(Welcome.Tick, clientTimeMs);
                        BeginStreaming();
                        Welcomed?.Invoke(Welcome);
                        break;
                    case MessageKind.Refused:
                    {
                        RefusedMessage refused = RefusedMessage.Read(reader);
                        reader.ExpectEnd();
                        State = ClientState.Refused;
                        LastReason = refused.Reason ?? string.Empty;
                        Dropped?.Invoke(LastReason);
                        break;
                    }
                    case MessageKind.Pong:
                    {
                        PongMessage pong = PongMessage.Read(reader);
                        reader.ExpectEnd();
                        LastRttMs = clientTimeMs - pong.ClientTimeMs;
                        ObserveTick(pong.ServerTick, clientTimeMs);
                        break;
                    }
                    case MessageKind.Correction:
                    {
                        CorrectionMessage correction = CorrectionMessage.Read(reader);
                        reader.ExpectEnd();
                        ObserveTick(correction.ServerTick, clientTimeMs);
                        LastCorrection = correction;
                        CorrectionCount++;
                        Corrected?.Invoke(correction);
                        break;
                    }
                    case MessageKind.PlayerState:
                    {
                        PlayerStateMessage state = PlayerStateMessage.Read(reader);
                        reader.ExpectEnd();
                        ObserveTick(state.ServerTick, clientTimeMs);
                        // Unreliable and unordered: an older report arriving late must not overwrite a newer one.
                        if (_others.TryGetValue(state.SessionId, out PlayerStateMessage held) && held.ServerTick > state.ServerTick) break;
                        _others[state.SessionId] = state;
                        RemoteMirror mirror;
                        if (!_mirrors.TryGetValue(state.SessionId, out mirror))
                        {
                            mirror = new RemoteMirror(state.SessionId);
                            _mirrors[state.SessionId] = mirror;
                        }
                        mirror.Push(state);
                        PlayerStateReceived?.Invoke(state);
                        break;
                    }
                    case MessageKind.PlayerLeft:
                    {
                        PlayerLeftMessage left = PlayerLeftMessage.Read(reader);
                        reader.ExpectEnd();
                        _others.Remove(left.SessionId);
                        _mirrors.Remove(left.SessionId);
                        PlayerLeft?.Invoke(left.SessionId);
                        break;
                    }
                    case MessageKind.EntitySpawn:
                    {
                        EntitySpawnMessage spawn = EntitySpawnMessage.Read(reader);
                        reader.ExpectEnd();
                        ObserveTick(spawn.ServerTick, clientTimeMs);
                        Entities.Apply(spawn);
                        break;
                    }
                    case MessageKind.EntityState:
                    {
                        EntityStateMessage state = EntityStateMessage.Read(reader);
                        reader.ExpectEnd();
                        ObserveTick(state.ServerTick, clientTimeMs);
                        Entities.Apply(state);
                        break;
                    }
                    case MessageKind.EntityGone:
                    {
                        EntityGoneMessage gone = EntityGoneMessage.Read(reader);
                        reader.ExpectEnd();
                        Entities.Apply(gone);
                        break;
                    }
                    case MessageKind.SnapshotEnd:
                    {
                        SnapshotEndMessage end = SnapshotEndMessage.Read(reader);
                        reader.ExpectEnd();
                        ObserveTick(end.ServerTick, clientTimeMs);
                        SnapshotApplied = true;
                        SnapshotEnded?.Invoke();
                        break;
                    }
                    case MessageKind.TileHeader:
                    {
                        TileHeaderMessage header = TileHeaderMessage.Read(reader);
                        reader.ExpectEnd();
                        if (Tiles != null) Tiles.Handle(header);
                        break;
                    }
                    case MessageKind.TileChunk:
                    {
                        TileChunkMessage chunk = TileChunkMessage.Read(reader);
                        reader.ExpectEnd();
                        if (Tiles != null) Tiles.Handle(chunk);
                        break;
                    }
                    default:
                        break;
                }
            }
            catch (ProtocolException ex)
            {
                // A server that sends us garbage is not one we stay attached to.
                Disconnect("malformed " + kind + " from server: " + ex.Message);
            }
        }

        /// <summary>At Welcome: the receiver for this region, and the request for the nine tiles around the spawn.</summary>
        private void BeginStreaming()
        {
            Grid = Welcome.ExtentM > 0.0 ? new TileGrid(Welcome.ExtentM) : null;
            Tiles = new TileReceiver(Welcome.RegionId, _tileCache);
            Tiles.TileReady += tile =>
            {
                _outstanding.Remove((tile.Layer, tile.Id));
                TileReady?.Invoke(tile);
            };
            Tiles.TileFailed += (layer, id, why) =>
            {
                _outstanding.Remove((layer, id));
                if (why.StartsWith("the server has no ", StringComparison.Ordinal)) _unserved.Add(layer);
                TileFailed?.Invoke(layer, id, why);
            };
            _outstanding.Clear();
            _requested.Clear();
            _unserved.Clear();
            TilesRequested = false;
            SnapshotApplied = false;
            _others.Clear();
            _mirrors.Clear();
            RequestTilesAround(Welcome.SpawnEast, Welcome.SpawnNorth);
        }
    }
}
