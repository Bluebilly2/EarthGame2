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
    /// The engine-free client: it owns the connection, speaks the handshake, reports its own body's movement and
    /// holds what the server has told it (the other players' bodies, and any correction of its own). It never
    /// simulates the world as truth. The Unity client wraps this and draws what it holds.
    /// </summary>
    public sealed class GameClient
    {
        private readonly IClientTransport _transport;
        private readonly PacketWriter _writer = new PacketWriter(256);
        private readonly Dictionary<uint, PlayerStateMessage> _others = new Dictionary<uint, PlayerStateMessage>();
        private string _playerName;
        private string _password;
        private uint _sequence;

        public GameClient(IClientTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public ClientState State { get; private set; } = ClientState.Disconnected;

        /// <summary>What the server said at Welcome; valid once <see cref="State"/> is Connected.</summary>
        public WelcomeMessage Welcome { get; private set; }

        /// <summary>Why the server said no, or why the link dropped. Empty while connected.</summary>
        public string LastReason { get; private set; } = string.Empty;

        /// <summary>Round trip of the most recent Pong, in milliseconds; negative until one arrives.</summary>
        public long LastRttMs { get; private set; } = -1;

        /// <summary>The server's tick as of the most recent Pong.</summary>
        public long LastServerTick { get; private set; } = -1;

        /// <summary>The most recent correction of this client's own body, and how many there have been.</summary>
        public CorrectionMessage LastCorrection { get; private set; }
        public int CorrectionCount { get; private set; }

        /// <summary>The other players' bodies as the server last sent them, by session id.</summary>
        public IReadOnlyDictionary<uint, PlayerStateMessage> Others => _others;

        public event Action<WelcomeMessage> Welcomed;
        public event Action<string> Dropped;
        public event Action<CorrectionMessage> Corrected;
        public event Action<PlayerStateMessage> PlayerStateReceived;

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

        /// <summary>Pumps the transport and handles everything it reports. Call once per frame.</summary>
        /// <param name="clientTimeMs">The caller's clock now, used to time Pongs.</param>
        public void Update(long clientTimeMs)
        {
            _transport.Update();
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
                        LastServerTick = pong.ServerTick;
                        break;
                    }
                    case MessageKind.Correction:
                    {
                        CorrectionMessage correction = CorrectionMessage.Read(reader);
                        reader.ExpectEnd();
                        LastCorrection = correction;
                        CorrectionCount++;
                        Corrected?.Invoke(correction);
                        break;
                    }
                    case MessageKind.PlayerState:
                    {
                        PlayerStateMessage state = PlayerStateMessage.Read(reader);
                        reader.ExpectEnd();
                        // Unreliable and unordered: an older report arriving late must not overwrite a newer one.
                        if (_others.TryGetValue(state.SessionId, out PlayerStateMessage held) && held.ServerTick > state.ServerTick) break;
                        _others[state.SessionId] = state;
                        PlayerStateReceived?.Invoke(state);
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
    }
}
