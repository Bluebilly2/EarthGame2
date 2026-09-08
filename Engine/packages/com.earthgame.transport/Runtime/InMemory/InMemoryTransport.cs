using System;
using System.Collections.Generic;

namespace EarthGame.Transport
{
    /// <summary>
    /// The single-player wire. A server transport and a client transport joined by two queues, so that solo
    /// play is a genuine client talking to a genuine server: every message is serialised, queued and
    /// delivered on the receiver's next poll, exactly as over a socket, with no fast path (plan §4.6, Rule 1).
    /// Delivery is reliable and ordered; a later simulated-loss mode belongs to the UDP transport's own
    /// simulator, not here.
    /// </summary>
    public static class InMemoryTransport
    {
        /// <summary>Creates a matched pair. The client connects to whatever port the server listens on.</summary>
        public static void CreatePair(out IServerTransport server, out IClientTransport client)
        {
            InMemoryServer s = new InMemoryServer();
            server = s;
            client = new InMemoryClient(s);
        }

        /// <summary>
        /// A further client for a server made by <see cref="CreatePair"/>: the two-client sessions of the tests and,
        /// later, of the soak. Throws when the server is not an in-memory one, so a mismatched pair is a loud error
        /// rather than a silent non-delivery.
        /// </summary>
        public static IClientTransport CreateClient(IServerTransport server)
        {
            InMemoryServer s = server as InMemoryServer;
            if (s == null) throw new ArgumentException("server is not an in-memory transport", nameof(server));
            return new InMemoryClient(s);
        }
    }

    internal sealed class InMemoryLink
    {
        /// <summary>Which end closed the link, so each side can tell a peer's stated reason from its own.</summary>
        public enum Side : byte { None = 0, Server = 1, Client = 2 }

        public readonly Queue<byte[]> ToServer = new Queue<byte[]>();
        public readonly Queue<byte[]> ToClient = new Queue<byte[]>();
        public bool Open = true;
        public string CloseReason;
        public Side ClosedBy = Side.None;
        public bool ServerNotifiedClosed;
        public bool ClientNotifiedClosed;
    }

    internal sealed class InMemoryConnection : IConnection
    {
        private readonly InMemoryLink _link;
        private readonly bool _isServerSide;

        public InMemoryConnection(int id, InMemoryLink link, bool isServerSide)
        {
            Id = id;
            _link = link;
            _isServerSide = isServerSide;
        }

        public int Id { get; }
        public bool IsOpen => _link.Open;
        internal InMemoryLink Link => _link;
        public long BytesSent { get; private set; }
        public long BytesReceived { get; internal set; }

        public void Send(ReadOnlySpan<byte> payload, Delivery delivery)
        {
            if (!_link.Open) return;
            byte[] copy = payload.ToArray();
            BytesSent += copy.Length;
            if (_isServerSide) _link.ToClient.Enqueue(copy);
            else _link.ToServer.Enqueue(copy);
        }

        public void Close(string reason)
        {
            if (!_link.Open) return;
            _link.Open = false;
            _link.CloseReason = reason ?? string.Empty;
            _link.ClosedBy = _isServerSide ? InMemoryLink.Side.Server : InMemoryLink.Side.Client;
        }
    }

    internal sealed class InMemoryServer : IServerTransport
    {
        private readonly List<IConnection> _connections = new List<IConnection>();
        private readonly List<InMemoryConnection> _live = new List<InMemoryConnection>();
        private readonly Queue<TransportEvent> _events = new Queue<TransportEvent>();
        private int _nextId = 1;

        public bool IsListening { get; private set; }
        public int Port { get; private set; } = -1;
        public IReadOnlyList<IConnection> Connections => _connections;

        public void Listen(int port)
        {
            IsListening = true;
            Port = port;
        }

        /// <summary>Called by a client's Connect. Returns the server-side connection, or null when not listening.</summary>
        internal InMemoryConnection Accept(InMemoryLink link)
        {
            if (!IsListening) return null;
            InMemoryConnection c = new InMemoryConnection(_nextId++, link, true);
            _live.Add(c);
            _connections.Add(c);
            _events.Enqueue(new TransportEvent { Kind = TransportEventKind.Connected, Connection = c });
            return c;
        }

        /// <summary>The elapsed time is unused here: memory has no bandwidth to meter.</summary>
        public void Update(double elapsedSeconds)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                InMemoryConnection c = _live[i];
                InMemoryLink link = c.Link;
                while (link.ToServer.Count > 0)
                {
                    byte[] data = link.ToServer.Dequeue();
                    c.BytesReceived += data.Length;
                    _events.Enqueue(new TransportEvent { Kind = TransportEventKind.Data, Connection = c, Data = data, Offset = 0, Count = data.Length });
                }
                if (!link.Open && !link.ServerNotifiedClosed)
                {
                    link.ServerNotifiedClosed = true;
                    _connections.Remove(c);
                    _events.Enqueue(new TransportEvent { Kind = TransportEventKind.Disconnected, Connection = c, Reason = link.CloseReason, ReasonFromPeer = link.ClosedBy != InMemoryLink.Side.Server });
                }
            }
            _live.RemoveAll(c => !c.Link.Open && c.Link.ServerNotifiedClosed);
        }

        public bool Poll(out TransportEvent evt)
        {
            if (_events.Count == 0)
            {
                evt = default;
                return false;
            }
            evt = _events.Dequeue();
            return true;
        }

        public void Dispose()
        {
            IsListening = false;
            for (int i = 0; i < _live.Count; i++) _live[i].Close("server disposed");
        }
    }

    internal sealed class InMemoryClient : IClientTransport
    {
        private readonly InMemoryServer _server;
        private readonly Queue<TransportEvent> _events = new Queue<TransportEvent>();
        private InMemoryConnection _connection;
        private bool _pendingConnect;

        public InMemoryClient(InMemoryServer server)
        {
            _server = server;
        }

        public IConnection Connection => _connection;

        public void Connect(string address, int port)
        {
            if (_connection != null && _connection.IsOpen) throw new InvalidOperationException("already connected");
            InMemoryLink link = new InMemoryLink();
            InMemoryConnection serverSide = _server.Accept(link);
            if (serverSide == null)
            {
                _connection = new InMemoryConnection(1, link, false);
                link.Open = false;
                link.CloseReason = "nothing is listening on port " + port;
                link.ClientNotifiedClosed = false;
                _pendingConnect = false;
                return;
            }
            _connection = new InMemoryConnection(1, link, false);
            _pendingConnect = true;
        }

        public void Update(double elapsedSeconds)
        {
            if (_connection == null) return;
            InMemoryLink link = _connection.Link;
            if (_pendingConnect)
            {
                _pendingConnect = false;
                _events.Enqueue(new TransportEvent { Kind = TransportEventKind.Connected, Connection = _connection });
            }
            while (link.ToClient.Count > 0)
            {
                byte[] data = link.ToClient.Dequeue();
                _connection.BytesReceived += data.Length;
                _events.Enqueue(new TransportEvent { Kind = TransportEventKind.Data, Connection = _connection, Data = data, Offset = 0, Count = data.Length });
            }
            if (!link.Open && !link.ClientNotifiedClosed)
            {
                link.ClientNotifiedClosed = true;
                _events.Enqueue(new TransportEvent { Kind = TransportEventKind.Disconnected, Connection = _connection, Reason = link.CloseReason, ReasonFromPeer = link.ClosedBy == InMemoryLink.Side.Server });
            }
        }

        public bool Poll(out TransportEvent evt)
        {
            if (_events.Count == 0)
            {
                evt = default;
                return false;
            }
            evt = _events.Dequeue();
            return true;
        }

        public void Dispose()
        {
            if (_connection != null) _connection.Close("client disposed");
        }
    }
}
