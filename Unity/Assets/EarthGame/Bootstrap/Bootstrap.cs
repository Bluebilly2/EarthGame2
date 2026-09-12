using System;
using System.Globalization;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
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
    /// <c>-eg-password</c>, <c>-eg-seed</c>, <c>-eg-world</c>, <c>-eg-data</c>, <c>-eg-saves</c>, <c>-eg-tiles</c>,
    /// <c>-eg-record dir</c>, <c>-eg-scenario first-frame|walk|soak|rejoin|join</c>, <c>-eg-seconds N</c>,
    /// <c>-eg-cycles N</c>, and for the client's own socket <c>-eg-latency</c>, <c>-eg-jitter</c>, <c>-eg-loss</c>,
    /// <c>-eg-sendcap</c>) it starts at once; without one it shows the shell (new world, continue, quit). It
    /// builds the transports and objects for the mode, pumps the server every frame with real elapsed time (the
    /// server's own accumulator turns that into fixed ticks), autosaves the world folder every half minute and at
    /// quit, and is the only caller of the server's Save. The world exists before the first player does.
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
        private System.Collections.Generic.IReadOnlyDictionary<string, string> _layerChecksums;
        private string _recordDir;
        private string _scenario;
        private double _lastRealtime;
        private float _nextAutosaveAt;
        private float _quitAt = -1f;
        private bool _launched;
        private LoadingController _loading;
        private LoadingRecorder _loadingRecorder;
        private Task<WorldPreparation.Result> _preparation;
        private CancellationTokenSource _preparationCancellation;
        private readonly ConcurrentQueue<string> _loadingStages = new ConcurrentQueue<string>();
        private Region _loadingRegion;
        private double _loadingStarted;
        private int _loadingUpdates;
        private bool _quitting;

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
            if (fromCommandLine && !LaunchArgs.Has("shell")) Launch();
            else ShowShell();
            // -eg-shell with -eg-record: the scenario walks the owner's own path through the shell, clicking New
            // world for him after a moment, so what that path draws can be recorded without a window.
            if (LaunchArgs.Has("shell") && LaunchArgs.Has("record")) Invoke(nameof(ScriptedNewWorld), 1.5f);
        }

        private void ScriptedNewWorld()
        {
            if (_shell != null) _shell.ClickNewWorld();
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
            _loadingRegion = Region.Bherwerre;
            _loadingStarted = Time.realtimeSinceStartupAsDouble;
            _loadingUpdates = 0;
            GameObject overlay = new GameObject("World loading");
            _loading = overlay.AddComponent<LoadingController>();
            _loading.Build();
            _loading.Back += ReturnAfterLoadFailure;
            if (LaunchArgs.Has("loading-record"))
            {
                _loadingRecorder = gameObject.AddComponent<LoadingRecorder>();
                _loadingRecorder.Begin(LaunchArgs.Get("loading-record", null), _loading);
            }
            StartCoroutine(BeginPreparation());
        }

        private IEnumerator BeginPreparation()
        {
            // Give the overlay a frame before starting disk work. Worker closures hold plain values only.
            yield return null;
            if (_quitting) yield break;
            if (_mode == LaunchMode.Join)
            {
                StartConnections(_loadingRegion, null, null);
                yield break;
            }
            Region region = _loadingRegion;
            if (_worldDir == null)
            {
                string name = LaunchArgs.Get("world", "world-" + _seed.ToString(CultureInfo.InvariantCulture));
                _worldDir = Path.Combine(RegionDataLocator.SavesDir(), name);
            }

            string worldDir = _worldDir;
            string dataDir = RegionDataLocator.DataDir(region);
            ulong seed = _seed;
            string now = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            var stages = _loadingStages;
            var cancellation = new CancellationTokenSource();
            _preparationCancellation = cancellation;
            CancellationToken token = cancellation.Token;
            _preparation = Task.Run(() => WorldPreparation.Load(worldDir, dataDir, region, seed, now, stages.Enqueue, token));
            // Observe errors even if the Unity object disappears before the task finishes.
            _preparation.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        private void PollPreparation()
        {
            while (_loadingStages.TryDequeue(out string stage))
            {
                _loading?.SetStage(stage);
                _loadingRecorder?.Stage(stage);
                Debug.Log("[loading] " + stage);
            }
            if (_preparation == null) return;
            _loadingUpdates++;
            if (!_preparation.IsCompleted) return;
            Task<WorldPreparation.Result> done = _preparation;
            _preparation = null;
            _preparationCancellation.Dispose();
            _preparationCancellation = null;
            if (_quitting) return;
            try
            {
                WorldPreparation.Result ready = done.GetAwaiter().GetResult();
                _seed = ready.World.Seed;
                _layerChecksums = ready.Checksums;
                _loadingRecorder?.Prepared();
                Debug.Log("[loading] prepared in " + (Time.realtimeSinceStartupAsDouble - _loadingStarted).ToString("0.0")
                    + " s; main-thread updates " + _loadingUpdates);
                if (ready.Census != null) Debug.Log("[bootstrap] " + ready.Census);
                StartConnections(_loadingRegion, ready.World, ready.Saved);
            }
            catch (Exception ex)
            {
                Debug.LogError("[loading] " + ex.Message);
                _loading?.ShowFailure(ex.Message, _server == null && _clientRuntime == null);
                _loadingRecorder?.Failed(ex.Message);
            }
        }

        private void ReturnAfterLoadFailure()
        {
            if (_preparation != null || _server != null || _clientRuntime != null) return;
            CloseLoading();
            _launched = false;
            _worldDir = null;
            _layerChecksums = null;
            ShowShell();
            _loadingRecorder?.ReturnedToMenu();
        }

        private void CloseLoading()
        {
            if (_loading == null) return;
            GameObject overlay = _loading.gameObject;
            _loading.Close();
            Destroy(overlay);
            _loading = null;
        }

        private void StartConnections(Region region, WorldState world, WorldSaveInfo saved)
        {
            if (_quitting) return;
            _loading?.SetStage("Preparing the ground around you");
            UdpOptions clientOptions = ClientUdpOptions();
            switch (_mode)
            {
                case LaunchMode.Solo:
                {
                    InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport ct);
                    StartServer(st, world, saved, _port);
                    // The pair's client first; a rejoin (a scenario's cut) gets a further client of the same server.
                    IClientTransport first = ct;
                    StartClient(() =>
                    {
                        if (first == null) return InMemoryTransport.CreateClient(st);
                        IClientTransport c = first;
                        first = null;
                        return c;
                    }, "memory", _port, region);
                    break;
                }
                case LaunchMode.Host:
                {
                    StartServer(new UdpServerTransport(new UdpOptions()), world, saved, _port);
                    StartClient(() => new UdpClientTransport(clientOptions), "127.0.0.1", _port, region);
                    break;
                }
                case LaunchMode.Join:
                    StartClient(() => new UdpClientTransport(clientOptions), _address, _port, region);
                    break;
                case LaunchMode.Dedicated:
                    StartServer(new UdpServerTransport(new UdpOptions()), world, saved, _port);
                    break;
            }
            if (_mode == LaunchMode.Dedicated) CloseLoading();
            _lastRealtime = Time.realtimeSinceStartupAsDouble;
            _nextAutosaveAt = Time.realtimeSinceStartup + AutosaveIntervalSeconds;
            // A scenario ends itself; without one, -eg-seconds is how long the process runs.
            int seconds = LaunchArgs.GetInt("seconds", 0);
            if (seconds > 0 && _scenario == null) _quitAt = Time.realtimeSinceStartup + seconds;
            Debug.Log("[bootstrap] " + _mode + " in region " + region.DisplayName + ", seed " + _seed
                      + (world != null ? ", world clock " + world.Clock.UtcText : "") + ", world folder " + _worldDir
                      + (_recordDir != null ? ", recording " + (_scenario ?? Recorder.Scenario) + " to " + _recordDir : "")
                      + (clientOptions.SimulatedMaxLatencyMs > 0 || clientOptions.SimulatedPacketLossPercent > 0
                          ? ", client socket simulating " + clientOptions.SimulatedMinLatencyMs + "-" + clientOptions.SimulatedMaxLatencyMs + " ms one way, " + clientOptions.SimulatedPacketLossPercent + "% loss" : "")
                      + (clientOptions.SendCapBytesPerSecond > 0 ? ", client send cap " + clientOptions.SendCapBytesPerSecond + " B/s" : ""));
        }

        /// <summary>The client socket's shaping and cap, the harness's conditions for the client's own uplink.</summary>
        private static UdpOptions ClientUdpOptions()
        {
            int latency = LaunchArgs.GetInt("latency", 0);
            int jitter = LaunchArgs.GetInt("jitter", 0);
            return new UdpOptions
            {
                SimulatedMinLatencyMs = Math.Max(0, latency - jitter),
                SimulatedMaxLatencyMs = latency + jitter,
                SimulatedPacketLossPercent = LaunchArgs.GetInt("loss", 0),
                SendCapBytesPerSecond = (long)LaunchArgs.GetDouble("sendcap", 0.0),
            };
        }

        private void StartServer(IServerTransport transport, WorldState world, WorldSaveInfo saved, int port)
        {
            _serverTransport = transport;
            // -eg-dev: a SOLO game for development, whose server lets a founder fly (M1.5e).
            _server = new GameServer(new ServerConfig { Password = _password, Movement = new MovementRules { AllowFlight = LaunchArgs.Has("dev") } },
                                     transport, world);
            if (saved != null) _server.RememberPlayers(saved.Players.Values);
            _server.SessionJoined += s => Debug.Log("[server] join  " + s.Name + " (session " + s.SessionId + ") at tick " + s.JoinedTick + (s.HasBody ? ", remembered" : ""));
            _server.SessionLeft += (s, reason) => Debug.Log("[server] leave " + s.Name + ": " + reason);
            _server.MoveCorrected += (s, reason) => Debug.Log("[server] correct " + s.Name + ": " + reason);
            _server.Listen(port);

            // -eg-items N: N things dropped on a ring three metres from the wake, cobbles and sticks in turn, every
            // third one from a metre and a half up so it falls (M1.3 promise 7: the frames of things at rest).
            int items = LaunchArgs.GetInt("items", 0);
            if (items > 0)
            {
                Double3 spawn = world.SpawnPoint();
                for (int i = 0; i < items; i++)
                {
                    double angle = i * 2.0 * Math.PI / items;
                    string key = i % 2 == 0 ? "item/cobble" : "item/stick";
                    Entity e = _server.SpawnItem(key, spawn.X + 3.0 * Math.Cos(angle), spawn.Z + 3.0 * Math.Sin(angle), i % 3 == 0 ? spawn.Y + 1.5 : (double?)null);
                    Debug.Log("[server] -eg-items: " + key + " " + e.Id + " at " + e.Position.X.ToString("0.0", CultureInfo.InvariantCulture) + ", " + e.Position.Z.ToString("0.0", CultureInfo.InvariantCulture) + (e.Item.Resting ? " resting" : " falling"));
                }
            }
        }

        private void StartClient(Func<IClientTransport> transportFactory, string address, int port, Region region)
        {
            _clientRuntime = gameObject.AddComponent<ClientRuntime>();
            _clientRuntime.BecameInteractive += _ => { _loadingRecorder?.Ready(); CloseLoading(); };
            _clientRuntime.Dropped += reason => _loading?.ShowFailure(reason, false);
            if (_recordDir != null && _scenario != null && !Recorder.IsKnown(_scenario))
            {
                // The runner's clock must start before the connection does: its first record is N1's number.
                JsonObject header = new JsonObject()
                    .With("role", "client").With("mode", _mode.ToString().ToLowerInvariant()).With("name", _playerName).With("address", address).With("port", port)
                    .With("region", region.Id).With("unity", Application.unityVersion).With("product_version", Application.version)
                    .With("latency_ms", LaunchArgs.GetInt("latency", 0)).With("jitter_ms", LaunchArgs.GetInt("jitter", 0)).With("loss_percent", LaunchArgs.GetInt("loss", 0))
                    .With("started_utc", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
                ScenarioRunner runner = gameObject.AddComponent<ScenarioRunner>();
                runner.Begin(_scenario, _recordDir, _clientRuntime, header);
            }
            _clientRuntime.Attach(transportFactory, address, port, _playerName, _password, region, _recordDir, _scenario);
        }

        private void Update()
        {
            PollPreparation();
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
                _server.Save(_worldDir, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture), _layerChecksums);
            }
            catch (Exception ex)
            {
                Debug.LogError("[bootstrap] save failed: " + ex.Message);
            }
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
            _preparationCancellation?.Cancel();
            Save();
        }

        private void OnDestroy()
        {
            _quitting = true;
            _preparationCancellation?.Cancel();
            if (_preparationCancellation != null)
            {
                var cancellation = _preparationCancellation;
                _preparation?.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
                _preparationCancellation = null;
            }
            CloseLoading();
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
            string scenario = LaunchArgs.Get("scenario", null);
            if (!string.IsNullOrEmpty(scenario) && scenario != "true")
            {
                if (!Recorder.IsKnown(scenario) && !ScenarioRunner.IsKnown(scenario))
                    Debug.LogError("[bootstrap] unknown scenario '" + scenario + "' (first-frame, carry, litter, wade, swim, trunk, controls, walk, soak, rejoin, join)");
                _scenario = scenario;
            }
        }
    }
}
