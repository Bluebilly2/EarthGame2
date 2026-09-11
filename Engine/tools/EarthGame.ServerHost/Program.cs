using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;

namespace EarthGame.ServerHost
{
    /// <summary>
    /// DEDICATED: the server alone, over UDP, driven by a wall-clock loop and a stdin console.
    /// Arguments follow the +key value convention (Rust's, and close to Minecraft's server.properties):
    ///   +server.port 28015  +server.password secret  +server.maxplayers 8  +server.seed 1347
    ///   +server.region bherwerre  +server.tickrate 20  +server.data Data/regions/bherwerre
    ///   +server.simulate.latency 50  +server.simulate.jitter 10  +server.simulate.loss 2   (the N1–N4 shaping,
    ///   one-way milliseconds and whole percent, applied to this end's socket)
    ///   +server.sendcap 1250000  (bytes per second per connection; 0 uncapped)
    ///   +server.log Artefacts/corpus/x/server/run.jsonl  (the server's run log, eg2.run v1; see ARCHITECTURE §10)
    ///   +server.seconds 1800  (stop by itself after this long)
    ///   +server.world Saves/world-1347  (the world folder: created with its layers, wake and census when absent, continued
    ///   when present, saved at stop)
    ///   +server.dev 1  (a development server: a founder may fly, M1.5e)
    /// Console commands: status, pause, resume, digest, stop.
    ///
    /// <para>The host is the server's clock and its instruments: it times each update for the tick statistics,
    /// reads the managed heap and the working set, and writes the bodies digest of every session per tick, so
    /// that N3 and N4 are recomputable from the log by a reader that never saw the process.</para>
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Dictionary<string, string> a = ParseArgs(args);
            int port = Int(a, "server.port", 28015);
            ServerConfig config = new ServerConfig
            {
                TickRate = Int(a, "server.tickrate", 20),
                MaxPlayers = Int(a, "server.maxplayers", 8),
                Password = Str(a, "server.password", string.Empty),
                Movement = new MovementRules { AllowFlight = Int(a, "server.dev", 0) != 0 },
            };
            if (config.Movement.AllowFlight) Log("a development server: a founder may fly");
            ulong seed = ULong(a, "server.seed", 1347UL);
            string regionId = Str(a, "server.region", Region.Bherwerre.Id);
            Region region = Region.ById(regionId);
            if (region == null)
            {
                Log("unknown region '" + regionId + "'; known: " + Region.Bherwerre.Id);
                return 2;
            }

            // The region's baked ground. Without it the server still runs, and says so: movement is then validated
            // for speed and extent only, never against a ground it does not have, and no tile is served.
            string dataDir = Str(a, "server.data", DefaultDataDir(region));
            Heightfield terrain = null;
            RegionRaster bake = null;
            string sidecar = Path.Combine(dataDir, "heights.json");
            if (File.Exists(sidecar))
            {
                bake = RegionRaster.Load(sidecar);
                terrain = new Heightfield(bake);
                Log("terrain " + sidecar + ": " + bake.Width + "x" + bake.Height + " at " + bake.CellM.ToString("0.#", CultureInfo.InvariantCulture) + " m");
            }
            else
            {
                Log("no terrain: " + sidecar + " not found; movement is validated for speed and extent only");
            }

            int latency = Int(a, "server.simulate.latency", 0);
            int jitter = Int(a, "server.simulate.jitter", 0);
            int loss = Int(a, "server.simulate.loss", 0);
            long sendCap = Long(a, "server.sendcap", 0);
            UdpOptions options = new UdpOptions
            {
                SimulatedMinLatencyMs = Math.Max(0, latency - jitter),
                SimulatedMaxLatencyMs = latency + jitter,
                SimulatedPacketLossPercent = loss,
                SendCapBytesPerSecond = sendCap,
            };
            int stopAfter = Int(a, "server.seconds", 0);

            // The world: a folder created with its layers (M1.2), continued when it exists, or nothing but the
            // region's canonical wake when no folder is named (Region owns the day, hour and longitude).
            //
            // One function prepares a world, here and in the game (WorldPreparation, M1.4 loading 2026-09-10):
            // a saved world's own terrain must load and match its manifest, and the region's bake is never put
            // quietly in its place. This host did that until 2026-09-10, so a world whose heights layer had gone
            // came back standing on different ground without a word, and its digest with it.
            string worldDir = Str(a, "server.world", null);
            WorldState world;
            WorldSaveInfo saved = null;
            IReadOnlyDictionary<string, string> layerChecksums = null;
            Stopwatch clock = Stopwatch.StartNew();
            if (!string.IsNullOrEmpty(worldDir))
            {
                bool continuing = WorldSave.Exists(worldDir);
                double started = clock.Elapsed.TotalSeconds;
                string now = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
                WorldPreparation.Result prepared;
                try
                {
                    // The console has no loading screen; the stages the screen shows go to the log instead.
                    prepared = WorldPreparation.Load(worldDir, dataDir, region, seed, now, stage => Log("  " + stage), CancellationToken.None);
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is JsonException || ex is KeyNotFoundException)
                {
                    Log("cannot " + (continuing ? "continue " : "create ") + worldDir + ": " + ex.Message);
                    return 2;
                }
                world = prepared.World;
                saved = prepared.Saved;
                layerChecksums = prepared.Checksums;
                seed = world.Seed;
                if (continuing)
                    Log("continuing " + worldDir + " at tick " + world.Tick + ", " + saved.Players.Count + " player(s) remembered"
                        + (world.Wake.HasValue ? ", wake at east " + world.Wake.Value.X.ToString("0", CultureInfo.InvariantCulture) + " north " + world.Wake.Value.Z.ToString("0", CultureInfo.InvariantCulture) : ""));
                else
                {
                    Log("created " + worldDir + " in " + (clock.Elapsed.TotalSeconds - started).ToString("0.0", CultureInfo.InvariantCulture) + " s: " + layerChecksums.Count + " layers");
                    foreach (string line in prepared.Census.Split('\n'))
                        if (line.Length > 0) Log("census  " + line);
                }
            }
            else
            {
                world = new WorldState(seed, region, region.WakeClock(), terrain);
            }
            UdpServerTransport transport = new UdpServerTransport(options);
            GameServer server = new GameServer(config, transport, world);
            if (saved != null) server.RememberPlayers(saved.Players.Values);

            RunLog log = null;
            string logPath = Str(a, "server.log", null);
            if (!string.IsNullOrEmpty(logPath))
            {
                JsonObject header = new JsonObject()
                    .With("role", "server").With("region", region.Id).With("seed", seed).With("tick_rate", config.TickRate).With("port", port)
                    .With("latency_ms", latency).With("jitter_ms", jitter).With("loss_percent", loss).With("send_cap_bytes_per_second", sendCap)
                    .With("interest_radius_m", config.InterestRadiusM).With("terrain", world.Terrain != null)
                    .With("started_utc", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
                log = RunLog.Open(logPath, header);
                Log("logging to " + logPath);
            }
            Instruments instruments = new Instruments(server, log, clock);

            server.SessionJoined += s =>
            {
                Log("join   " + s.Name + " (session " + s.SessionId + ") at tick " + s.JoinedTick + (s.HasBody ? ", remembered" : ""));
                instruments.Joined(s);
            };
            server.SessionLeft += (s, reason) =>
            {
                Log("leave  " + s.Name + " (session " + s.SessionId + "): " + reason);
                instruments.Left(s, reason);
            };
            server.MoveCorrected += (s, reason) =>
            {
                Log("correct " + s.Name + ": " + reason);
                instruments.Corrected(s, reason);
            };
            server.Stepped += (w, dt) => instruments.Stepped();

            double encodeStart = clock.Elapsed.TotalSeconds;
            int encoded = server.Tiles.EncodeAll();
            if (encoded > 0)
                Log("encoded " + encoded + " tiles in " + (clock.Elapsed.TotalSeconds - encodeStart).ToString("0.00", CultureInfo.InvariantCulture) + " s");
            try
            {
                server.Listen(port);
            }
            catch (Exception ex)
            {
                Log("cannot listen on " + port + ": " + ex.Message);
                return 2;
            }
            Log("EarthGame2 dedicated server " + EngineInfo.Version + " listening on UDP " + port
                + ", region " + region.Id + ", seed " + seed + ", tick " + config.TickRate + " Hz"
                + (latency > 0 || loss > 0 ? ", simulating " + options.SimulatedMinLatencyMs + "-" + options.SimulatedMaxLatencyMs + " ms one way, " + loss + "% loss" : "")
                + (sendCap > 0 ? ", send cap " + sendCap + " B/s" : ""));

            Queue<string> commands = new Queue<string>();
            object gate = new object();
            Thread stdin = new Thread(() =>
            {
                string line;
                while ((line = Console.ReadLine()) != null)
                {
                    lock (gate) commands.Enqueue(line.Trim());
                }
            });
            stdin.IsBackground = true;
            stdin.Start();

            double last = 0.0;
            bool running = true;
            Func<double> seconds = () => clock.Elapsed.TotalSeconds;
            while (running)
            {
                double now = seconds();
                server.Update(now - last, seconds);
                last = now;
                instruments.Tick(now);
                if (stopAfter > 0 && now >= stopAfter)
                {
                    Log("+server.seconds elapsed");
                    running = false;
                }

                lock (gate)
                {
                    while (commands.Count > 0)
                    {
                        string cmd = commands.Dequeue();
                        switch (cmd)
                        {
                            case "stop":
                                running = false;
                                break;
                            case "pause":
                                server.Paused = true;
                                Log("paused at tick " + world.Tick);
                                instruments.Command("pause");
                                break;
                            case "resume":
                                server.Paused = false;
                                Log("resumed at tick " + world.Tick);
                                instruments.Command("resume");
                                break;
                            case "digest":
                                Log("digest " + server.Digest() + " at tick " + world.Tick);
                                instruments.Command("digest");
                                break;
                            case "status":
                                Log("tick " + world.Tick + ", " + world.Clock.UtcText + ", players " + server.Sessions.Count
                                    + (server.Paused ? ", paused" : "") + ", dropped " + server.DroppedSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s"
                                    + ", digest " + server.Digest());
                                break;
                            case "save":
                                if (string.IsNullOrEmpty(worldDir)) Log("no world folder to save to (+server.world)");
                                else
                                {
                                    server.Save(worldDir, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture), layerChecksums);
                                    Log("saved " + worldDir + " at tick " + world.Tick + " digest " + server.Digest());
                                }
                                break;
                            case "":
                                break;
                            default:
                                if (cmd.StartsWith("spawn ", StringComparison.Ordinal)) Spawn(server, cmd);
                                else if (cmd.StartsWith("stand ", StringComparison.Ordinal)) Stand(server, cmd);
                                else Log("unknown command '" + cmd + "' (status, pause, resume, digest, spawn <key> <east> <north> [<up>], stand <name> <east> <north>, save, stop)");
                                break;
                        }
                    }
                }

                Thread.Sleep(5);
            }

            Log("stopping");
            instruments.End(seconds());
            if (!string.IsNullOrEmpty(worldDir))
            {
                server.Save(worldDir, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture), layerChecksums);
                Log("saved " + worldDir);
            }
            transport.Dispose();
            log?.Dispose();
            return 0;
        }

        /// <summary>
        /// What the host measures and writes: per tick the bodies digests (buffered into a line per session per
        /// second), per second the bandwidth per session and the process memory, per minute the tick window.
        /// Every record's tick is the world's tick at the moment of writing.
        /// </summary>
        private sealed class Instruments
        {
            private readonly GameServer _server;
            private readonly RunLog _log;
            private readonly Stopwatch _clock;
            private readonly Dictionary<uint, List<string>> _digests = new Dictionary<uint, List<string>>();
            private readonly Dictionary<uint, List<object>> _positions = new Dictionary<uint, List<object>>();
            private readonly Dictionary<uint, long> _fromTick = new Dictionary<uint, long>();
            private readonly Dictionary<uint, long> _sentBefore = new Dictionary<uint, long>();
            private readonly Dictionary<uint, long> _receivedBefore = new Dictionary<uint, long>();
            private readonly Process _process = Process.GetCurrentProcess();
            private double _nextSecond = 1.0;
            private double _nextMinute = 60.0;
            private long _tilesServedBefore;

            public Instruments(GameServer server, RunLog log, Stopwatch clock)
            {
                _server = server;
                _log = log;
                _clock = clock;
            }

            private double T => _clock.Elapsed.TotalSeconds;

            public void Joined(PlayerSession s)
            {
                _log?.Record(T, _server.World.Tick, "join", new JsonObject().With("session", s.SessionId).With("name", s.Name)
                    .With("remembered", s.HasBody).With("east", s.Body.East).With("up", s.Body.Up).With("north", s.Body.North));
            }

            public void Left(PlayerSession s, string reason)
            {
                _log?.Record(T, _server.World.Tick, "leave", new JsonObject().With("session", s.SessionId).With("name", s.Name).With("reason", reason));
                Flush(s.SessionId);
                _digests.Remove(s.SessionId);
                _positions.Remove(s.SessionId);
                _fromTick.Remove(s.SessionId);
            }

            public void Corrected(PlayerSession s, string reason)
            {
                _log?.Record(T, _server.World.Tick, "correction", new JsonObject().With("session", s.SessionId).With("name", s.Name).With("reason", reason)
                    .With("east", s.Body.East).With("up", s.Body.Up).With("north", s.Body.North));
            }

            public void Command(string name)
            {
                _log?.Record(T, _server.World.Tick, name, new JsonObject().With("digest", _server.Digest()).With("paused", _server.Paused));
            }

            /// <summary>After every world step: the digest of every session's accepted body, as the client will mirror it.</summary>
            public void Stepped()
            {
                if (_log == null) return;
                long tick = _server.World.Tick;
                foreach (PlayerSession s in _server.Sessions)
                {
                    if (!s.HasBody) continue;
                    List<string> list;
                    if (!_digests.TryGetValue(s.SessionId, out list))
                    {
                        list = new List<string>(64);
                        _digests[s.SessionId] = list;
                        _positions[s.SessionId] = new List<object>(192);
                    }
                    // The window's first tick is this one: the flush that ended the last window happened after
                    // an earlier tick, and naming that tick put every window after the first one tick out
                    // (the quick corpus of 2026-09-08 matched 3 mirror digests in 71 for that reason alone).
                    if (list.Count == 0) _fromTick[s.SessionId] = tick;
                    list.Add(_server.BodyDigest(s));
                    List<object> positions = _positions[s.SessionId];
                    positions.Add(s.Body.East);
                    positions.Add(s.Body.Up);
                    positions.Add(s.Body.North);
                }
            }

            public void Tick(double now)
            {
                if (_log == null) return;
                if (now >= _nextSecond)
                {
                    _nextSecond += 1.0;
                    long tick = _server.World.Tick;
                    foreach (uint id in new List<uint>(_digests.Keys)) Flush(id);
                    long tilesServed = _server.Tiles.BytesServed;
                    foreach (PlayerSession s in _server.Sessions)
                    {
                        long sent = s.Connection.BytesSent;
                        long received = s.Connection.BytesReceived;
                        long sentBefore, receivedBefore;
                        _sentBefore.TryGetValue(s.SessionId, out sentBefore);
                        _receivedBefore.TryGetValue(s.SessionId, out receivedBefore);
                        _sentBefore[s.SessionId] = sent;
                        _receivedBefore[s.SessionId] = received;
                        _log.Record(T, tick, "bandwidth", new JsonObject().With("session", s.SessionId).With("name", s.Name)
                            .With("sent", sent - sentBefore).With("received", received - receivedBefore)
                            .With("sent_total", sent).With("received_total", received)
                            .With("moves_accepted", s.MovesAccepted).With("corrections", s.Corrections));
                    }
                    _log.Record(T, tick, "memory", new JsonObject()
                        .With("heap_bytes", GC.GetTotalMemory(false)).With("working_set_bytes", WorkingSet())
                        .With("players", _server.Sessions.Count).With("tiles_served_bytes", tilesServed - _tilesServedBefore)
                        .With("dropped_seconds", _server.DroppedSeconds).With("digest", _server.Digest()));
                    _tilesServedBefore = tilesServed;
                }
                if (now >= _nextMinute)
                {
                    _nextMinute += 60.0;
                    TickWindowRecord();
                }
            }

            /// <summary>Once a minute: the tick window, and the heap after a full collection (the one tick that pays for it is inside N4's 0.1 %).</summary>
            private void TickWindowRecord()
            {
                TickWindow w = _server.Ticks.Snapshot();
                _log.Record(T, _server.World.Tick, "ticks", new JsonObject().With("count", w.Count).With("over_interval", w.OverInterval)
                    .With("max_ms", w.MaxSeconds * 1000.0).With("mean_ms", w.MeanSeconds * 1000.0).With("p95_ms", w.P95Seconds * 1000.0)
                    .With("heap_collected_bytes", GC.GetTotalMemory(true)).With("working_set_bytes", WorkingSet()));
            }

            private void Flush(uint sessionId)
            {
                List<string> list;
                if (_log == null || !_digests.TryGetValue(sessionId, out list) || list.Count == 0) return;
                List<object> digests = new List<object>(list.Count);
                foreach (string d in list) digests.Add(d);
                List<object> positions = new List<object>(_positions[sessionId]);
                _log.Record(T, _server.World.Tick, "bodies", new JsonObject().With("session", sessionId).With("from", _fromTick[sessionId])
                    .With("digests", digests).With("positions", positions));
                list.Clear();
                _positions[sessionId].Clear();
            }

            private long WorkingSet()
            {
                try
                {
                    _process.Refresh();
                    return _process.WorkingSet64;
                }
                catch (InvalidOperationException)
                {
                    return -1;
                }
            }

            public void End(double now)
            {
                if (_log == null) return;
                foreach (uint id in new List<uint>(_digests.Keys)) Flush(id);
                TickWindowRecord();
                _log.Record(T, _server.World.Tick, "end", new JsonObject().With("seconds", now).With("players", _server.Sessions.Count)
                    .With("digest", _server.Digest()).With("dropped_seconds", _server.DroppedSeconds));
            }
        }

        /// <summary>Data/regions/&lt;region&gt; under the repository root (found by global.json above the working directory), else under the working directory.</summary>
        private static string DefaultDataDir(Region region)
        {
            string dir = Directory.GetCurrentDirectory();
            while (dir != null && !File.Exists(Path.Combine(dir, "global.json")))
                dir = Path.GetDirectoryName(dir);
            return Path.Combine(dir ?? Directory.GetCurrentDirectory(), "Data", "regions", region.Id);
        }

        private static void Log(string line)
        {
            Console.WriteLine(DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + line);
            Console.Out.Flush();
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (!key.StartsWith("+", StringComparison.Ordinal)) continue;
                string value = i + 1 < args.Length && !args[i + 1].StartsWith("+", StringComparison.Ordinal) ? args[++i] : "true";
                map[key.Substring(1)] = value;
            }
            return map;
        }
        /// <summary>stand &lt;name&gt; &lt;east&gt; &lt;north&gt;: a remembered body put there, so that name's next join wakes there (M1.4c).</summary>
        private static void Stand(GameServer server, string cmd)
        {
            string[] parts = cmd.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                Log("stand takes a name, an east and a north");
                return;
            }
            try
            {
                SavedPlayer p = server.StandPlayer(parts[1], double.Parse(parts[2], CultureInfo.InvariantCulture), double.Parse(parts[3], CultureInfo.InvariantCulture));
                Log("stood " + p.Name + " at east " + p.Body.East.ToString("0.0", CultureInfo.InvariantCulture)
                    + " north " + p.Body.North.ToString("0.0", CultureInfo.InvariantCulture)
                    + ", on ground at " + p.Body.Up.ToString("0.00", CultureInfo.InvariantCulture) + " m; save to keep it");
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                Log("stand refused: " + ex.Message);
            }
        }

        /// <summary>spawn &lt;key&gt; &lt;east&gt; &lt;north&gt; [&lt;up&gt;]: an item dropped there, falling when above the ground (M1.3).</summary>
        private static void Spawn(GameServer server, string cmd)
        {
            string[] parts = cmd.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || parts.Length > 5)
            {
                Log("spawn takes a key, east and north, and an optional height");
                return;
            }
            try
            {
                double east = double.Parse(parts[2], CultureInfo.InvariantCulture);
                double north = double.Parse(parts[3], CultureInfo.InvariantCulture);
                double? up = parts.Length == 5 ? double.Parse(parts[4], CultureInfo.InvariantCulture) : (double?)null;
                Entity e = server.SpawnItem(parts[1], east, north, up);
                Log("spawned " + parts[1] + " " + e.Id + " at east " + e.Position.X.ToString("0.00", CultureInfo.InvariantCulture)
                    + " up " + e.Position.Y.ToString("0.00", CultureInfo.InvariantCulture) + " north " + e.Position.Z.ToString("0.00", CultureInfo.InvariantCulture)
                    + (e.Item.Resting ? ", resting" : ", falling"));
            }
            catch (Exception ex) when (ex is FormatException || ex is KeyNotFoundException || ex is ArgumentException)
            {
                Log("spawn refused: " + ex.Message);
            }
        }


        private static int Int(Dictionary<string, string> a, string key, int fallback)
        {
            string v;
            int parsed;
            return a.TryGetValue(key, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static long Long(Dictionary<string, string> a, string key, long fallback)
        {
            string v;
            long parsed;
            return a.TryGetValue(key, out v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static ulong ULong(Dictionary<string, string> a, string key, ulong fallback)
        {
            string v;
            ulong parsed;
            return a.TryGetValue(key, out v) && ulong.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static string Str(Dictionary<string, string> a, string key, string fallback)
        {
            string v;
            return a.TryGetValue(key, out v) ? v : fallback;
        }
    }
}
