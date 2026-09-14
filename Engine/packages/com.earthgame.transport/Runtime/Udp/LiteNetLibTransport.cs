using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using LiteNetLib;

namespace EarthGame.Transport
{
    /// <summary>
    /// Knobs for the UDP transport, including the loss/latency simulator that the N1–N4 harness drives and the
    /// send cap that stands in for a real uplink. The simulator settings are applied at Listen/Connect and take
    /// effect on that side's manager only, so a test can shape the client's uplink and the server's downlink
    /// independently.
    /// </summary>
    public sealed class UdpOptions
    {
        /// <summary>Peers must present this key at connection; the real check is the application's Hello.</summary>
        public string ConnectionKey = "EarthGame2";

        /// <summary>Milliseconds without traffic before a peer is dropped.</summary>
        public int DisconnectTimeoutMs = 10000;

        /// <summary>Simulated one-way latency band in milliseconds; both zero means off.</summary>
        public int SimulatedMinLatencyMs = 0;
        public int SimulatedMaxLatencyMs = 0;

        /// <summary>Simulated packet loss in whole percent; zero means off.</summary>
        public int SimulatedPacketLossPercent = 0;

        /// <summary>
        /// Outgoing payload bytes per second per connection, a token bucket with a quarter-second burst; zero
        /// means uncapped. The N1 rows are stated at 10 and 5 Mbit/s (1,250,000 and 625,000 here) as harness
        /// conditions regardless of the owner's link (CANON ruling 11).
        /// </summary>
        public long SendCapBytesPerSecond = 0;

        /// <summary>
        /// Bind to this machine's loopback address alone (127.0.0.1 and ::1) instead of every address, so nothing
        /// off the machine can reach the socket and Windows' firewall has nothing to ask about: a socket bound to
        /// every address raises its "Security Alert" once for each new program path, and by 2026-09-14 the owner
        /// had answered it for eight test builds and the test suite twice, each corpus run's players joining
        /// 127.0.0.1 from a fresh build folder. The harness's hosts run with it (<c>+server.local 1</c>,
        /// <c>-eg-local</c>); a joining client takes it by itself when the address it joins is loopback; a game
        /// hosted for friends binds every address, and asks once.
        /// </summary>
        public bool LocalOnly = false;
    }

    public sealed class UdpConnection : IConnection
    {
        /// <summary>The largest single payload the bucket must be able to hold, so a big reliable message is never stuck.</summary>
        private const long MinimumBurstBytes = 65536;

        private readonly NetPeer _peer;
        private readonly long _capBytesPerSecond;
        private readonly double _burstBytes;
        private readonly Queue<KeyValuePair<byte[], Delivery>> _pending = new Queue<KeyValuePair<byte[], Delivery>>();
        private double _tokens;

        public UdpConnection(int id, NetPeer peer, long capBytesPerSecond)
        {
            Id = id;
            _peer = peer;
            _capBytesPerSecond = capBytesPerSecond;
            _burstBytes = Math.Max(MinimumBurstBytes, capBytesPerSecond * 0.25);
            _tokens = _burstBytes;
        }

        public int Id { get; }
        public bool IsOpen => _peer.ConnectionState == ConnectionState.Connected;
        internal NetPeer Peer => _peer;
        public long BytesSent { get; private set; }
        public long BytesReceived { get; internal set; }

        /// <summary>Round trip as LiteNetLib measures it, in milliseconds.</summary>
        public int RoundTripMs => _peer.RoundTripTime;

        /// <summary>Payloads held back by the cap and not yet on the wire.</summary>
        public int PendingCount => _pending.Count;

        public void Send(ReadOnlySpan<byte> payload, Delivery delivery)
        {
            if (!IsOpen) return;
            if (_capBytesPerSecond <= 0)
            {
                Transmit(payload, delivery);
                return;
            }
            if (_pending.Count == 0 && _tokens >= payload.Length)
            {
                _tokens -= payload.Length;
                Transmit(payload, delivery);
                return;
            }
            _pending.Enqueue(new KeyValuePair<byte[], Delivery>(payload.ToArray(), delivery));
        }

        /// <summary>Refills the bucket for the elapsed time and sends what it now allows, in order.</summary>
        internal void Refill(double elapsedSeconds)
        {
            if (_capBytesPerSecond <= 0) return;
            if (elapsedSeconds > 0.0) _tokens = Math.Min(_burstBytes, _tokens + _capBytesPerSecond * elapsedSeconds);
            while (_pending.Count > 0 && IsOpen)
            {
                KeyValuePair<byte[], Delivery> next = _pending.Peek();
                if (_tokens < next.Key.Length) break;
                _pending.Dequeue();
                _tokens -= next.Key.Length;
                Transmit(next.Key, next.Value);
            }
        }

        private void Transmit(ReadOnlySpan<byte> payload, Delivery delivery)
        {
            BytesSent += payload.Length;
            _peer.Send(payload, 0, delivery == Delivery.Reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
        }

        public void Close(string reason)
        {
            if (_peer.ConnectionState == ConnectionState.Disconnected) return;
            byte[] data = Encoding.UTF8.GetBytes(reason ?? string.Empty);
            _peer.Disconnect(data);
        }
    }

    /// <summary>Shared plumbing: a LiteNetLib manager, its listener, and the event queue the game polls.</summary>
    public abstract class UdpTransportBase : ITransport
    {
        protected readonly EventBasedNetListener Listener = new EventBasedNetListener();
        protected readonly NetManager Manager;
        protected readonly Queue<TransportEvent> Events = new Queue<TransportEvent>();
        protected readonly Dictionary<NetPeer, UdpConnection> ByPeer = new Dictionary<NetPeer, UdpConnection>();
        protected readonly UdpOptions Options;
        private int _nextId = 1;

        protected UdpTransportBase(UdpOptions options)
        {
            Options = options ?? new UdpOptions();
            Manager = new NetManager(Listener);
            Manager.AutoRecycle = true;
            Manager.DisconnectTimeout = Options.DisconnectTimeoutMs;
            Manager.UnsyncedEvents = false; // events are delivered only from PollEvents, on our thread
            ApplySimulation();

            Listener.PeerConnectedEvent += OnPeerConnected;
            Listener.PeerDisconnectedEvent += OnPeerDisconnected;
            Listener.NetworkReceiveEvent += OnReceive;
        }

        private void ApplySimulation()
        {
            bool latency = Options.SimulatedMaxLatencyMs > 0;
            bool loss = Options.SimulatedPacketLossPercent > 0;
            Manager.SimulateLatency = latency;
            Manager.SimulatePacketLoss = loss;
            if (latency)
            {
                Manager.SimulationMinLatency = Options.SimulatedMinLatencyMs;
                Manager.SimulationMaxLatency = Options.SimulatedMaxLatencyMs < Options.SimulatedMinLatencyMs
                    ? Options.SimulatedMinLatencyMs
                    : Options.SimulatedMaxLatencyMs;
            }
            if (loss) Manager.SimulationPacketLossChance = Options.SimulatedPacketLossPercent;
        }

        /// <summary>The port the socket is bound to; 0 before it is.</summary>
        public int LocalPort => Manager.LocalPort;

        /// <summary>True once the socket is bound to the loopback address alone (<see cref="UdpOptions.LocalOnly"/>).</summary>
        public bool BoundLocalOnly { get; private set; }

        /// <summary>
        /// Binds the socket: the loopback address alone when this end is for this machine, else every address, which
        /// is LiteNetLib's own Start(port) and the firewall's question.
        /// </summary>
        protected bool Start(bool localOnly, int port)
        {
            BoundLocalOnly = localOnly;
            return Manager.Start(localOnly ? IPAddress.Loopback : IPAddress.Any,
                                 localOnly ? IPAddress.IPv6Loopback : IPAddress.IPv6Any, port);
        }

        /// <summary>The addresses that name this machine: 127.0.0.0/8, ::1 and "localhost".</summary>
        public static bool IsLoopback(string address) =>
            string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(address, out IPAddress ip) && IPAddress.IsLoopback(ip));

        protected UdpConnection Track(NetPeer peer)
        {
            UdpConnection c;
            if (!ByPeer.TryGetValue(peer, out c))
            {
                c = new UdpConnection(_nextId++, peer, Options.SendCapBytesPerSecond);
                ByPeer[peer] = c;
            }
            return c;
        }

        private void OnPeerConnected(NetPeer peer)
        {
            UdpConnection c = Track(peer);
            Events.Enqueue(new TransportEvent { Kind = TransportEventKind.Connected, Connection = c });
            PeerOpened(c);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            UdpConnection c = Track(peer);
            string reason = info.Reason.ToString();
            bool fromPeer = false;
            if (info.AdditionalData != null && info.AdditionalData.AvailableBytes > 0)
            {
                byte[] bytes = info.AdditionalData.GetRemainingBytes();
                reason = Encoding.UTF8.GetString(bytes);
                fromPeer = true;
            }
            ByPeer.Remove(peer);
            PeerClosed(c);
            Events.Enqueue(new TransportEvent { Kind = TransportEventKind.Disconnected, Connection = c, Reason = reason, ReasonFromPeer = fromPeer });
        }

        private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            UdpConnection c = Track(peer);
            byte[] data = reader.GetRemainingBytes();
            c.BytesReceived += data.Length;
            Events.Enqueue(new TransportEvent { Kind = TransportEventKind.Data, Connection = c, Data = data, Offset = 0, Count = data.Length });
        }

        protected virtual void PeerOpened(UdpConnection c) { }
        protected virtual void PeerClosed(UdpConnection c) { }

        public void Update(double elapsedSeconds)
        {
            foreach (UdpConnection c in ByPeer.Values) c.Refill(elapsedSeconds);
            Manager.PollEvents();
        }

        public bool Poll(out TransportEvent evt)
        {
            if (Events.Count == 0)
            {
                evt = default;
                return false;
            }
            evt = Events.Dequeue();
            return true;
        }

        public virtual void Dispose()
        {
            Manager.Stop(true);
        }

        /// <summary>
        /// Drops the socket without a word to the peer, the way a cable does: no Disconnect packet, no reason.
        /// The peer learns of it by its own timeout. The N3 scenario's cut.
        /// </summary>
        public void Sever()
        {
            Manager.Stop(false);
        }
    }

    /// <summary>The listening side over UDP. One NetManager, one port, many peers.</summary>
    public sealed class UdpServerTransport : UdpTransportBase, IServerTransport
    {
        private readonly List<IConnection> _connections = new List<IConnection>();

        public UdpServerTransport(UdpOptions options = null) : base(options)
        {
            Listener.ConnectionRequestEvent += request => request.AcceptIfKey(Options.ConnectionKey);
        }

        public bool IsListening => Manager.IsRunning;
        public int Port => LocalPort;
        public IReadOnlyList<IConnection> Connections => _connections;

        public void Listen(int port)
        {
            if (!Start(Options.LocalOnly, port))
                throw new InvalidOperationException("could not bind UDP port " + port);
        }

        protected override void PeerOpened(UdpConnection c) => _connections.Add(c);
        protected override void PeerClosed(UdpConnection c) => _connections.Remove(c);
    }

    /// <summary>The connecting side over UDP. One NetManager bound to any port, one peer.</summary>
    public sealed class UdpClientTransport : UdpTransportBase, IClientTransport
    {
        private UdpConnection _connection;

        public UdpClientTransport(UdpOptions options = null) : base(options) { }

        public IConnection Connection => _connection;

        public void Connect(string address, int port)
        {
            // A client joining this machine binds to it alone: the harness's players, and a hosted game's own client.
            if (!Manager.IsRunning && !Start(Options.LocalOnly || IsLoopback(address), 0))
                throw new InvalidOperationException("could not open a UDP socket");
            NetPeer peer = Manager.Connect(address, port, Options.ConnectionKey);
            if (peer == null) throw new InvalidOperationException("connect refused locally for " + address + ":" + port);
            _connection = Track(peer);
        }
    }
}
