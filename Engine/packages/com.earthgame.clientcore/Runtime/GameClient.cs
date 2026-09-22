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
        private TileId _centre;
        private bool _hasCentre;

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

        /// <summary>The server's clock as its newest Pong carried it, hours (M1.D); NaN until one arrives.</summary>
        public double LastServerTotalHours { get; private set; } = double.NaN;

        /// <summary>How many times faster than the game's rate the server's clock runs, as its newest Pong carried it; one until one arrives.</summary>
        public double LastClockScale { get; private set; } = 1.0;

        /// <summary>The water in the founder's body as the server last told it (FP.1), 1 full; 1 before any word.</summary>
        public double LastWater01 { get; private set; } = 1.0;

        /// <summary>The core's temperature as the server last told it (FP.2), °C; normal before any word.</summary>
        public double LastCoreC { get; private set; } = Warmth.NormalCoreC;

        /// <summary>The last death the server told of (FP.2), or null.</summary>
        public Death? LastDeath { get; private set; }

        /// <summary>Whether the server has this player in developer mode (M1.E, CANON ruling 39); off until it says otherwise.</summary>
        public bool DeveloperMode { get; private set; }

        /// <summary>Whether the server refused the last asking for developer mode: a server started without development does.</summary>
        public bool DeveloperModeRefused { get; private set; }

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

        /// <summary>What this founder carries, place by place, and which place is the hand, as the server last said (M1.5a); its things are null until then.</summary>
        public CarryingMessage Carrying { get; private set; }

        /// <summary>The newest answer to an intent this client sent (M1.5a).</summary>
        public IntentResultMessage LastIntentResult { get; private set; }

        /// <summary>What has been taken from the loose layer, as the server has told this client (M1.5b): every taking, whichever tiles are held.</summary>
        public LooseTaken Taken { get; } = new LooseTaken();

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
        /// <summary>A tile the client let go to stay inside its bound (M1.4b); the view drops what it drew of it.</summary>
        public event Action<TileLayer, TileId> TileDropped;
        public event Action SnapshotEnded;
        /// <summary>The answer to an intent this client sent (M1.5a).</summary>
        public event Action<IntentResultMessage> IntentAnswered;
        /// <summary>What this founder carries changed, or was told at the join (M1.5a).</summary>
        public event Action<CarryingMessage> CarryingChanged;
        /// <summary>The founder's own work as the server last told it (BF.2): its progress, or its end with the words.</summary>
        public WorkStateMessage WorkState { get; private set; }
        public event Action<WorkStateMessage> WorkStateChanged;
        /// <summary>The founder's state as the server tells it (FP.1): once a second, and at every change that matters.</summary>
        public event Action<FounderStateMessage> FounderStateChanged;
        /// <summary>The founder died (FP.2): what killed them and the numbers, for the sentence the screen shows.</summary>
        public event Action<Death> Died;
        /// <summary>The server answered the developer's switch (M1.E): what the player now has, and whether the asking was refused.</summary>
        public event Action<bool, bool> DeveloperModeChanged;
        /// <summary>Something was taken from a cell of the loose layer, or the join told of it (M1.5b); the cell's takings as they now stand.</summary>
        public event Action<LooseTaken.Cell> LooseTakenChanged;

        private uint _intentSequence;

        /// <summary>Asks the server to do a verb (M1.5a), reliably; the sequence returned is the one its answer names, 0 when not connected.</summary>
        public uint SendIntent(IntentMessage intent)
        {
            if (State != ClientState.Connected) return 0;
            intent.Sequence = ++_intentSequence;
            _writer.Reset();
            intent.Write(_writer);
            _transport.Connection.Send(_writer.Written, Delivery.Reliable);
            return intent.Sequence;
        }

        /// <summary>Asks a development server to move a developer's setting, or do a deed (M1.D), reliably; nothing when not connected.</summary>
        /// <summary>
        /// Asks the server for developer mode on or off (M1.E, the player's F2). The answer comes back as
        /// <see cref="DeveloperModeChanged"/>; until it does, nothing a developer's mode allows is taken for granted.
        /// </summary>
        public void SendDeveloperMode(bool on)
        {
            if (State != ClientState.Connected) return;
            DeveloperModeMessage asked = new DeveloperModeMessage { On = on, Refused = false };
            _writer.Reset();
            asked.Write(_writer);
            _transport.Connection.Send(_writer.Written, Delivery.Reliable);
        }

        public void SendDevSetting(string name, double value)
        {
            if (State != ClientState.Connected) return;
            DevSettingMessage setting = new DevSettingMessage { Name = name, Value = value };
            _writer.Reset();
            setting.Write(_writer);
            _transport.Connection.Send(_writer.Written, Delivery.Reliable);
        }

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
            _centre = Grid.ForPosition(east, north);
            _hasCentre = true;
            IReadOnlyList<TileId> around = Grid.Around(_centre);
            int asked = 0;
            foreach (TileLayer layer in TileLayers.All)
            {
                // A layer this world has none of answers every tile with a refusal; asking once is how the client
                // finds that out, and asking again for every tile it walks onto is how it would waste the wire.
                if (_unserved.Contains(layer)) continue;
                // A far layer is the whole region's, asked for once and kept (M1.6d); every other is the tiles round the
                // founder. The far layers come last in the set, so the ground the founder stands on is sent first.
                IReadOnlyList<TileId> wanted = TileLayers.IsFar(layer) ? WholeGrid() : around;
                List<TileId> fresh = new List<TileId>(wanted.Count);
                for (int i = 0; i < wanted.Count; i++)
                    if (_requested.Add((layer, wanted[i]))) fresh.Add(wanted[i]);
                for (int start = 0; start < fresh.Count; start += TileRequestMessage.MostTiles)
                {
                    List<TileId> part = fresh.GetRange(start, Math.Min(TileRequestMessage.MostTiles, fresh.Count - start));
                    TileRequestMessage request;
                    request.Wants = Tiles.WantsFor(layer, part);
                    PacketWriter w = new PacketWriter(16 + part.Count * 16);
                    request.Write(w);
                    _transport.Connection.Send(w.Written, Delivery.Reliable);
                    for (int i = 0; i < part.Count; i++) _outstanding.Add((layer, part[i]));
                }
                if (layer == TileLayer.Ground) asked = fresh.Count;
            }
            TilesRequested = true;
            return asked;
        }

        /// <summary>Every tile of the region's grid, in rows from the south-west: what a far layer is asked for over (M1.6d).</summary>
        private IReadOnlyList<TileId> WholeGrid()
        {
            int side = Grid.TilesPerSide;
            if (_wholeGrid != null && _wholeGrid.Count == side * side) return _wholeGrid;
            _wholeGrid = new List<TileId>(side * side);
            for (int iz = 0; iz < side; iz++)
                for (int ix = 0; ix < side; ix++) _wholeGrid.Add(new TileId(ix, iz));
            return _wholeGrid;
        }

        private List<TileId> _wholeGrid;

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
                        LastServerTotalHours = pong.ServerTotalHours;
                        LastClockScale = pong.ClockScale;
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
                    case MessageKind.IntentResult:
                    {
                        IntentResultMessage result = IntentResultMessage.Read(reader);
                        reader.ExpectEnd();
                        LastIntentResult = result;
                        IntentAnswered?.Invoke(result);
                        break;
                    }
                    case MessageKind.Carrying:
                    {
                        CarryingMessage carrying = CarryingMessage.Read(reader);
                        reader.ExpectEnd();
                        Carrying = carrying;
                        CarryingChanged?.Invoke(carrying);
                        break;
                    }
                    case MessageKind.WorkState:
                    {
                        WorkStateMessage state = WorkStateMessage.Read(reader);
                        reader.ExpectEnd();
                        WorkState = state;
                        WorkStateChanged?.Invoke(state);
                        break;
                    }
                    case MessageKind.FounderState:
                    {
                        FounderStateMessage founder = FounderStateMessage.Read(reader);
                        reader.ExpectEnd();
                        LastWater01 = founder.Water01;
                        LastCoreC = founder.CoreC;
                        FounderStateChanged?.Invoke(founder);
                        break;
                    }
                    case MessageKind.Died:
                    {
                        DiedMessage died = DiedMessage.Read(reader);
                        reader.ExpectEnd();
                        LastDeath = died.Death;
                        Died?.Invoke(died.Death);
                        break;
                    }
                    case MessageKind.DeveloperMode:
                    {
                        DeveloperModeMessage answer = DeveloperModeMessage.Read(reader);
                        reader.ExpectEnd();
                        DeveloperMode = answer.On;
                        DeveloperModeRefused = answer.Refused;
                        DeveloperModeChanged?.Invoke(answer.On, answer.Refused);
                        break;
                    }
                    case MessageKind.LooseTaken:
                    {
                        LooseTakenMessage taken = LooseTakenMessage.Read(reader);
                        reader.ExpectEnd();
                        foreach (LooseTaken.Cell cell in taken.Cells)
                        {
                            Taken.Merge(cell);
                            if (Taken.TryGet(cell.Row, cell.Col, out LooseTaken.Cell now)) LooseTakenChanged?.Invoke(now);
                        }
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
            _wholeGrid = null;
            Tiles = new TileReceiver(Welcome.RegionId, _tileCache);
            Tiles.TileReady += tile =>
            {
                _outstanding.Remove((tile.Layer, tile.Id));
                TileReady?.Invoke(tile);
                if (_hasCentre) Tiles.Trim(Grid, _centre);
            };
            Tiles.TileDropped += (layer, id) =>
            {
                // Forgotten, so walking back to it asks again; the disk cache answers that without the wire.
                _requested.Remove((layer, id));
                TileDropped?.Invoke(layer, id);
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
            _hasCentre = false;
            TilesRequested = false;
            SnapshotApplied = false;
            _others.Clear();
            _mirrors.Clear();
            RequestTilesAround(Welcome.SpawnEast, Welcome.SpawnNorth);
        }
    }
}
