using System;
using System.Collections.Generic;
using EarthGame.Transport;

namespace EarthGame.Tests.Server
{
    /// <summary>
    /// A server transport for the tests that hands everything on to the one it wraps and keeps how each message the server
    /// sent was to be delivered. Told to lose unreliable messages, it loses every one the server sends, as a link that
    /// dropped them all would, and still delivers the rest: what must arrive can then be shown to arrive.
    /// </summary>
    public sealed class DeliveryRecorder : IServerTransport
    {
        private readonly IServerTransport _inner;
        private readonly Dictionary<int, Recorded> _connections = new Dictionary<int, Recorded>();

        public DeliveryRecorder(IServerTransport inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>Whether every unreliable message the server sends from now on is lost.</summary>
        public bool LoseUnreliable;

        /// <summary>Every message the server sent, in order: its bytes, how it was to be delivered, and whether it was lost.</summary>
        public List<(byte[] Payload, Delivery Delivery, bool Lost)> Sent { get; } = new List<(byte[] Payload, Delivery Delivery, bool Lost)>();

        public bool IsListening => _inner.IsListening;

        public IReadOnlyList<IConnection> Connections => _inner.Connections;

        public void Listen(int port) => _inner.Listen(port);

        public void Update(double elapsedSeconds) => _inner.Update(elapsedSeconds);

        public bool Poll(out TransportEvent evt)
        {
            if (!_inner.Poll(out evt)) return false;
            if (evt.Connection != null) evt.Connection = Wrap(evt.Connection);
            return true;
        }

        public void Dispose() => _inner.Dispose();

        private IConnection Wrap(IConnection inner)
        {
            if (!_connections.TryGetValue(inner.Id, out Recorded recorded))
            {
                recorded = new Recorded(this, inner);
                _connections[inner.Id] = recorded;
            }
            return recorded;
        }

        private sealed class Recorded : IConnection
        {
            private readonly DeliveryRecorder _owner;
            private readonly IConnection _inner;

            public Recorded(DeliveryRecorder owner, IConnection inner)
            {
                _owner = owner;
                _inner = inner;
            }

            public int Id => _inner.Id;
            public bool IsOpen => _inner.IsOpen;
            public long BytesSent => _inner.BytesSent;
            public long BytesReceived => _inner.BytesReceived;

            public void Send(ReadOnlySpan<byte> payload, Delivery delivery)
            {
                bool lost = _owner.LoseUnreliable && delivery == Delivery.Unreliable;
                _owner.Sent.Add((payload.ToArray(), delivery, lost));
                if (!lost) _inner.Send(payload, delivery);
            }

            public void Close(string reason) => _inner.Close(reason);
        }
    }
}
