using System;
using System.Collections.Generic;
using System.Text;
using LiteNetLib;

namespace EarthGame.Transport
{
    /// <summary>
    /// Knobs for the UDP transport, including the loss/latency simulator that the N1–N4 harness drives.
    /// The simulator settings are applied at Listen/Connect and take effect on that side's manager only, so a
    /// test can shape the client's uplink and the server's downlink independently.
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
    }

    public sealed class UdpConnection : IConnection
    {
        private readonly NetPeer _peer;

        public UdpConnection(int id, NetPeer peer)
        {
            Id = id;
            _peer = peer;
        }

        public int Id { get; }
        public bool IsOpen => _peer.ConnectionState == ConnectionState.Connected;
        internal NetPeer Peer => _peer;

        /// <summary>Round trip as LiteNetLib measures it, in milliseconds.</summary>
        public int RoundTripMs => _peer.RoundTripTime;

        public void Send(ReadOnlySpan<byte> payload, Delivery delivery)
        {
            if (!IsOpen) return;
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

        protected UdpConnection Track(NetPeer peer)
        {
            UdpConnection c;
            if (!ByPeer.TryGetValue(peer, out c))
            {
                c = new UdpConnection(_nextId++, peer);
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
            Events.Enqueue(new TransportEvent { Kind = TransportEventKind.Data, Connection = c, Data = data, Offset = 0, Count = data.Length });
        }

        protected virtual void PeerOpened(UdpConnection c) { }
        protected virtual void PeerClosed(UdpConnection c) { }

        public void Update()
        {
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
        public int Port => Manager.LocalPort;
        public IReadOnlyList<IConnection> Connections => _connections;

        public void Listen(int port)
        {
            if (!Manager.Start(port))
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
            if (!Manager.IsRunning && !Manager.Start())
                throw new InvalidOperationException("could not open a UDP socket");
            NetPeer peer = Manager.Connect(address, port, Options.ConnectionKey);
            if (peer == null) throw new InvalidOperationException("connect refused locally for " + address + ":" + port);
            _connection = Track(peer);
        }
    }
}
