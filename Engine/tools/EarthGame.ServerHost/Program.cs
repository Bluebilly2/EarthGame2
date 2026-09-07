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
    /// Console commands: status, pause, resume, stop.
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
            };
            ulong seed = ULong(a, "server.seed", 1347UL);
            string regionId = Str(a, "server.region", Region.Bherwerre.Id);
            Region region = Region.ById(regionId);
            if (region == null)
            {
                Log("unknown region '" + regionId + "'; known: " + Region.Bherwerre.Id);
                return 2;
            }

            // The region's baked ground. Without it the server still runs, and says so: movement is then validated
            // for speed and extent only, never against a ground it does not have.
            string dataDir = Str(a, "server.data", DefaultDataDir(region));
            Heightfield terrain = null;
            string sidecar = Path.Combine(dataDir, "heights.json");
            if (File.Exists(sidecar))
            {
                RegionRaster raster = RegionRaster.Load(sidecar);
                terrain = new Heightfield(raster);
                Log("terrain " + sidecar + ": " + raster.Width + "x" + raster.Height + " at " + raster.CellM.ToString("0.#", CultureInfo.InvariantCulture) + " m");
            }
            else
            {
                Log("no terrain: " + sidecar + " not found; movement is validated for speed and extent only");
            }

            // The world starts at the region's canonical wake (Region owns the day, hour and longitude).
            WorldState world = new WorldState(seed, region, region.WakeClock(), terrain);
            UdpServerTransport transport = new UdpServerTransport(new UdpOptions());
            GameServer server = new GameServer(config, transport, world);
            server.SessionJoined += s => Log("join   " + s.Name + " (session " + s.SessionId + ") at tick " + s.JoinedTick);
            server.SessionLeft += (s, reason) => Log("leave  " + s.Name + " (session " + s.SessionId + "): " + reason);
            server.MoveCorrected += (s, reason) => Log("correct " + s.Name + ": " + reason);

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
                + ", region " + region.Id + ", seed " + seed + ", tick " + config.TickRate + " Hz");

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

            Stopwatch clock = Stopwatch.StartNew();
            double last = 0.0;
            bool running = true;
            while (running)
            {
                double now = clock.Elapsed.TotalSeconds;
                server.Update(now - last);
                last = now;

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
                                Log("paused");
                                break;
                            case "resume":
                                server.Paused = false;
                                Log("resumed");
                                break;
                            case "status":
                                Log("tick " + world.Tick + ", " + world.Clock.UtcText + ", players " + server.Sessions.Count
                                    + (server.Paused ? ", paused" : "") + ", dropped " + server.DroppedSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s");
                                break;
                            case "":
                                break;
                            default:
                                Log("unknown command '" + cmd + "' (status, pause, resume, stop)");
                                break;
                        }
                    }
                }

                Thread.Sleep(5);
            }

            Log("stopping");
            transport.Dispose();
            return 0;
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

        private static int Int(Dictionary<string, string> a, string key, int fallback)
        {
            string v;
            int parsed;
            return a.TryGetValue(key, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
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
