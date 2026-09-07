using System;
using System.Collections.Generic;

namespace EarthGame.Transport
{
    /// <summary>How a payload should travel. Reliable is ordered; unreliable may drop or reorder.</summary>
    public enum Delivery : byte
    {
        Reliable = 0,
        Unreliable = 1,
    }

    /// <summary>What a poll can report.</summary>
    public enum TransportEventKind : byte
    {
        Connected = 1,
        Data = 2,
        Disconnected = 3,
    }

    /// <summary>One thing that happened on the transport since the last poll.</summary>
    public struct TransportEvent
    {
        public TransportEventKind Kind;
        public IConnection Connection;
        /// <summary>The payload for <see cref="TransportEventKind.Data"/>; owned by the receiver after the poll.</summary>
        public byte[] Data;
        public int Offset;
        public int Count;
        /// <summary>For <see cref="TransportEventKind.Disconnected"/>: why, as the transport knows it.</summary>
        public string Reason;
        /// <summary>
        /// True when <see cref="Reason"/> is the text the PEER gave when it closed, false when it is the transport's
        /// own diagnosis (a timeout, a socket error). A server that refuses a client says why in its close, and
        /// over UDP that close can overtake the Refused message it sent a moment earlier; the flag lets the client
        /// treat a peer-stated close during the handshake as the refusal it is.
        /// </summary>
        public bool ReasonFromPeer;
    }

    /// <summary>One peer, as seen from one side. Ids are unique per transport instance, never reused.</summary>
    public interface IConnection
    {
        int Id { get; }
        bool IsOpen { get; }
        /// <summary>Queues a payload. The bytes are copied; the caller may reuse its buffer immediately.</summary>
        void Send(ReadOnlySpan<byte> payload, Delivery delivery);
        /// <summary>Closes with a reason the peer will see as its Disconnected event.</summary>
        void Close(string reason);
    }

    /// <summary>
    /// The seam between the game and the wire. Both ends pump it from their own loop: <see cref="Update"/>
    /// moves bytes, <see cref="Poll"/> hands events over one at a time. Single-threaded by contract.
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>Pumps the underlying socket or queue. Call once per frame or tick before polling.</summary>
        void Update();
        /// <summary>Takes the next pending event; false when there is none.</summary>
        bool Poll(out TransportEvent evt);
    }

    /// <summary>The listening side.</summary>
    public interface IServerTransport : ITransport
    {
        void Listen(int port);
        bool IsListening { get; }
        IReadOnlyList<IConnection> Connections { get; }
    }

    /// <summary>The connecting side. One connection at a time.</summary>
    public interface IClientTransport : ITransport
    {
        void Connect(string address, int port);
        IConnection Connection { get; }
    }
}
