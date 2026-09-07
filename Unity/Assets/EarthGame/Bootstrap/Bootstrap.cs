using System;
using EarthGame.Client;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using UnityEngine;

namespace EarthGame.Bootstrap
{
    /// <summary>How this process takes part in a world (plan §4.1, §4.6 Rule 1: there is no single player).</summary>
    public enum LaunchMode
    {
        /// <summary>The server and the client in this process over the in-memory transport. What "single player" is.</summary>
        Solo = 0,
        /// <summary>The server listening on a UDP port and the client of this process joining it over loopback.</summary>
        Host = 1,
        /// <summary>Only a client, joining a server by address.</summary>
        Join = 2,
        /// <summary>Only the server, over UDP. The console host in Engine/tools does the same without Unity.</summary>
        Dedicated = 3,
    }

    /// <summary>
    /// The only component that knows both the server and the client exist. Reads the launch mode from the
    /// command line (<c>-eg-mode solo|host|join|dedicated</c>, <c>-eg-address</c>, <c>-eg-port</c>,
    /// <c>-eg-name</c>, <c>-eg-password</c>, <c>-eg-seed</c>) or from the inspector when there is none, builds the
    /// transports and objects for that mode, and pumps the server every frame with real elapsed time; the
    /// server's own accumulator turns that into fixed ticks. The world exists before the first player does.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private LaunchMode _mode = LaunchMode.Solo;
        [SerializeField] private string _address = "127.0.0.1";
        [SerializeField] private int _port = 28015;
        [SerializeField] private string _playerName = "William";
        [SerializeField] private string _password = "";
        [SerializeField] private ulong _seed = 1347;

        private GameServer _server;
        private IServerTransport _serverTransport;
        private ClientRuntime _clientRuntime;
        private double _lastRealtime;

        public LaunchMode Mode => _mode;
        public GameServer Server => _server;

        private void Awake()
        {
            Application.runInBackground = true;
            ReadCommandLine();
#if UNITY_SERVER
            _mode = LaunchMode.Dedicated;
#endif
            Region region = Region.Bherwerre;
            WorldState world = new WorldState(_seed, region, region.WakeClock());

            switch (_mode)
            {
                case LaunchMode.Solo:
                {
                    InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
                    StartServer(st, world, _port);
                    StartClient(ct, "memory", _port);
                    break;
                }
                case LaunchMode.Host:
                {
                    StartServer(new UdpServerTransport(new UdpOptions()), world, _port);
                    StartClient(new UdpClientTransport(new UdpOptions()), "127.0.0.1", _port);
                    break;
                }
                case LaunchMode.Join:
                    StartClient(new UdpClientTransport(new UdpOptions()), _address, _port);
                    break;
                case LaunchMode.Dedicated:
                    StartServer(new UdpServerTransport(new UdpOptions()), world, _port);
                    break;
            }
            _lastRealtime = Time.realtimeSinceStartupAsDouble;
            Debug.Log("[bootstrap] " + _mode + " in region " + region.DisplayName + ", seed " + _seed
                      + ", world clock " + world.Clock.UtcText);
        }

        private void StartServer(IServerTransport transport, WorldState world, int port)
        {
            _serverTransport = transport;
            _server = new GameServer(new ServerConfig { Password = _password }, transport, world);
            _server.SessionJoined += s => Debug.Log("[server] join  " + s.Name + " (session " + s.SessionId + ") at tick " + s.JoinedTick);
            _server.SessionLeft += (s, reason) => Debug.Log("[server] leave " + s.Name + ": " + reason);
            _server.Listen(port);
        }

        private void StartClient(IClientTransport transport, string address, int port)
        {
            _clientRuntime = gameObject.AddComponent<ClientRuntime>();
            _clientRuntime.Attach(transport, address, port, _playerName, _password);
        }

        private void Update()
        {
            if (_server == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            _server.Update(now - _lastRealtime);
            _lastRealtime = now;
        }

        private void OnDestroy()
        {
            _serverTransport?.Dispose();
        }

        private void ReadCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                string key = args[i];
                string value = args[i + 1];
                switch (key)
                {
                    case "-eg-mode":
                        if (Enum.TryParse(value, true, out LaunchMode parsed)) _mode = parsed;
                        break;
                    case "-eg-address": _address = value; break;
                    case "-eg-port": int.TryParse(value, out _port); break;
                    case "-eg-name": _playerName = value; break;
                    case "-eg-password": _password = value; break;
                    case "-eg-seed": ulong.TryParse(value, out _seed); break;
                }
            }
        }
    }
}
