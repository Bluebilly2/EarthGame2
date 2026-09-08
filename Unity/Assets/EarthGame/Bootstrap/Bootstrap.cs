using System;
using System.Globalization;
using System.IO;
using EarthGame.Client;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Shared;
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
    /// The only component that knows both the server and the client exist. With a launch mode on the command line
    /// (<c>-eg-mode solo|host|join|dedicated</c>, <c>-eg-address</c>, <c>-eg-port</c>, <c>-eg-name</c>,
    /// <c>-eg-password</c>, <c>-eg-seed</c>, <c>-eg-world</c>, <c>-eg-data</c>, <c>-eg-saves</c>,
    /// <c>-eg-record dir</c>, <c>-eg-seconds N</c>) it starts at once; without one it shows the shell (new world,
    /// continue, quit). It builds the transports and objects for the mode, pumps the server every frame with real
    /// elapsed time (the server's own accumulator turns that into fixed ticks), autosaves the world folder every
    /// half minute and at quit, and is the only caller of the server's Save. The world exists before the first
    /// player does.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        private const float AutosaveIntervalSeconds = 30f;

        [SerializeField] private LaunchMode _mode = LaunchMode.Solo;
        [SerializeField] private string _address = "127.0.0.1";
        [SerializeField] private int _port = 28015;
        [SerializeField] private string _playerName = "William";
        [SerializeField] private string _password = "";
        [SerializeField] private ulong _seed = 1347;

        private GameServer _server;
        private IServerTransport _serverTransport;
        private ClientRuntime _clientRuntime;
        private ShellController _shell;
        private string _worldDir;
        private string _recordDir;
        private double _lastRealtime;
        private float _nextAutosaveAt;
        private float _quitAt = -1f;
        private bool _launched;

        public LaunchMode Mode => _mode;
        public GameServer Server => _server;
        public string WorldDir => _worldDir;

        private void Awake()
        {
            Application.runInBackground = true;
            if (Application.isBatchMode) AudioListener.volume = 0f;
            ReadCommandLine();
            bool fromCommandLine = LaunchArgs.Has("mode") || LaunchArgs.Has("record");
#if UNITY_SERVER
            _mode = LaunchMode.Dedicated;
            fromCommandLine = true;
#endif
            if (fromCommandLine) Launch();
            else ShowShell();
        }

        private void ShowShell()
        {
            string latest = LatestWorldDir();
            _shell = gameObject.AddComponent<ShellController>();
            _shell.Build(latest != null, latest != null ? "Continue " + Path.GetFileName(latest) : "");
            _shell.NewWorld += () =>
            {
                _seed = NewSeed();
                _worldDir = Path.Combine(RegionDataLocator.SavesDir(), "world-" + _seed.ToString(CultureInfo.InvariantCulture));
                CloseShellAndLaunch();
            };
            _shell.Continue += () =>
            {
                _worldDir = latest;
                CloseShellAndLaunch();
            };
            _shell.Quit += Application.Quit;
            _shell.SetStatus("saves: " + RegionDataLocator.SavesDir());
            Debug.Log("[shell] shown; " + (latest != null ? "latest world " + latest : "no world to continue") + "; saves " + RegionDataLocator.SavesDir());
        }

        private void CloseShellAndLaunch()
        {
            _shell.Close();
            _shell = null;
            Launch();
        }

        private void Launch()
        {
            if (_launched) return;
            _launched = true;
            Region region = Region.Bherwerre;
            if (_worldDir == null)
            {
                string name = LaunchArgs.Get("world", "world-" + _seed.ToString(CultureInfo.InvariantCulture));
                _worldDir = Path.Combine(RegionDataLocator.SavesDir(), name);
            }

            WorldState world = null;
            WorldSaveInfo saved = null;
            if (_mode != LaunchMode.Join)
            {
                Heightfield terrain = RegionDataLocator.TryLoadHeightfield(region, out string message);
                Debug.Log("[bootstrap] " + message);
                if (WorldSave.Exists(_worldDir))
                {
                    saved = WorldSave.Read(_worldDir);
                    world = WorldSave.Restore(saved, terrain);
                    _seed = world.Seed;
                    Debug.Log("[bootstrap] continuing " + _worldDir + " at tick " + world.Tick + ", day " + (world.Clock.DaysElapsed + 1) + ", " + saved.Players.Count + " player(s) remembered");
                }
                else
                {
                    world = new WorldState(_seed, region, region.WakeClock(), terrain);
                }
            }

            switch (_mode)
            {
                case LaunchMode.Solo:
                {
                    InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
                    StartServer(st, world, saved, _port);
                    StartClient(ct, "memory", _port, region);
                    break;
                }
                case LaunchMode.Host:
                {
                    StartServer(new UdpServerTransport(new UdpOptions()), world, saved, _port);
                    StartClient(new UdpClientTransport(new UdpOptions()), "127.0.0.1", _port, region);
                    break;
                }
                case LaunchMode.Join:
                    StartClient(new UdpClientTransport(new UdpOptions()), _address, _port, region);
                    break;
                case LaunchMode.Dedicated:
                    StartServer(new UdpServerTransport(new UdpOptions()), world, saved, _port);
                    break;
            }
            _lastRealtime = Time.realtimeSinceStartupAsDouble;
            _nextAutosaveAt = Time.realtimeSinceStartup + AutosaveIntervalSeconds;
            int seconds = LaunchArgs.GetInt("seconds", 0);
            if (seconds > 0) _quitAt = Time.realtimeSinceStartup + seconds;
            Debug.Log("[bootstrap] " + _mode + " in region " + region.DisplayName + ", seed " + _seed
                      + (world != null ? ", world clock " + world.Clock.UtcText : "") + ", world folder " + _worldDir
                      + (_recordDir != null ? ", recording to " + _recordDir : ""));
        }

        private void StartServer(IServerTransport transport, WorldState world, WorldSaveInfo saved, int port)
        {
            _serverTransport = transport;
            _server = new GameServer(new ServerConfig { Password = _password }, transport, world);
            if (saved != null) _server.RememberPlayers(saved.Players.Values);
            _server.SessionJoined += s => Debug.Log("[server] join  " + s.Name + " (session " + s.SessionId + ") at tick " + s.JoinedTick);
            _server.SessionLeft += (s, reason) => Debug.Log("[server] leave " + s.Name + ": " + reason);
            _server.MoveCorrected += (s, reason) => Debug.Log("[server] correct " + s.Name + ": " + reason);
            _server.Listen(port);
        }

        private void StartClient(IClientTransport transport, string address, int port, Region region)
        {
            _clientRuntime = gameObject.AddComponent<ClientRuntime>();
            _clientRuntime.Attach(transport, address, port, _playerName, _password, region, _recordDir);
        }

        private void Update()
        {
            if (_server != null)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                _server.Update(now - _lastRealtime);
                _lastRealtime = now;
                if (Time.realtimeSinceStartup >= _nextAutosaveAt)
                {
                    _nextAutosaveAt = Time.realtimeSinceStartup + AutosaveIntervalSeconds;
                    Save();
                }
            }
            if (_quitAt > 0f && Time.realtimeSinceStartup >= _quitAt)
            {
                _quitAt = -1f;
                Debug.Log("[bootstrap] -eg-seconds elapsed; quitting");
                Application.Quit(0);
            }
        }

        private void Save()
        {
            if (_server == null || _worldDir == null) return;
            try
            {
                _server.Save(_worldDir, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Debug.LogError("[bootstrap] save failed: " + ex.Message);
            }
        }

        private void OnApplicationQuit() => Save();

        private void OnDestroy()
        {
            _serverTransport?.Dispose();
        }

        /// <summary>The most recently written world under the saves folder, or null.</summary>
        private static string LatestWorldDir()
        {
            string saves = RegionDataLocator.SavesDir();
            if (!Directory.Exists(saves)) return null;
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string dir in Directory.GetDirectories(saves))
            {
                string worldFile = Path.Combine(dir, WorldSave.WorldFile);
                if (!File.Exists(worldFile)) continue;
                DateTime t = File.GetLastWriteTimeUtc(worldFile);
                if (t > bestTime)
                {
                    bestTime = t;
                    best = dir;
                }
            }
            return best;
        }

        private static ulong NewSeed()
        {
            // The seed is an input to the engine, chosen here from the clock; the engine itself never reads one.
            ulong ticks = (ulong)DateTime.UtcNow.Ticks;
            return (ticks ^ (ticks >> 17)) & 0xFFFFFFFFFFFFUL;
        }

        private void ReadCommandLine()
        {
            string mode = LaunchArgs.Get("mode", null);
            if (mode != null && Enum.TryParse(mode, true, out LaunchMode parsed)) _mode = parsed;
            _address = LaunchArgs.Get("address", _address);
            _port = LaunchArgs.GetInt("port", _port);
            _playerName = LaunchArgs.Get("name", _playerName);
            _password = LaunchArgs.Get("password", _password);
            _seed = LaunchArgs.GetULong("seed", _seed);
            string record = LaunchArgs.Get("record", null);
            _recordDir = string.IsNullOrEmpty(record) || record == "true" ? null : Path.GetFullPath(record);
        }
    }
}
