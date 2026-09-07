using EarthGame.ClientCore;
using EarthGame.Protocol;
using EarthGame.Transport;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The Unity wrapper around the engine-free <see cref="GameClient"/>: pumps it once per frame against
    /// Unity's real-time clock and reports what the server said. Everything that draws the world will hang off
    /// this component; in M1.0 it only proves the seam is alive (the log shows the Welcome and the round trip).
    /// This assembly never references EarthGame.Server; the asmdef enforces it.
    /// </summary>
    public sealed class ClientRuntime : MonoBehaviour
    {
        private GameClient _client;
        private IClientTransport _transport;
        private float _nextPingAt;

        public GameClient Client => _client;

        /// <summary>Attaches the client to a transport and starts the handshake. Called by the bootstrap.</summary>
        public void Attach(IClientTransport transport, string address, int port, string playerName, string password)
        {
            _transport = transport;
            _client = new GameClient(transport);
            _client.Welcomed += OnWelcomed;
            _client.Dropped += OnDropped;
            _client.Connect(address, port, playerName, password);
        }

        private void Update()
        {
            if (_client == null) return;
            long nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
            _client.Update(nowMs);
            if (_client.State == ClientState.Connected && Time.realtimeSinceStartup >= _nextPingAt)
            {
                _client.Ping(nowMs);
                _nextPingAt = Time.realtimeSinceStartup + 1.0f;
            }
        }

        private void OnWelcomed(WelcomeMessage welcome)
        {
            Debug.Log("[client] welcomed: session " + welcome.SessionId + ", region " + welcome.RegionId
                      + ", seed " + welcome.Seed + ", tick " + welcome.Tick + " at " + welcome.TickRate + " Hz");
        }

        private void OnDropped(string reason)
        {
            Debug.Log("[client] " + (_client.State == ClientState.Refused ? "refused: " : "dropped: ") + reason);
        }

        private void OnDestroy()
        {
            _client?.Disconnect("client destroyed");
            _transport?.Dispose();
        }
    }
}
