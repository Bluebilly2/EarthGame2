using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Shared;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EarthGame.Client
{
    /// <summary>
    /// The M1.B scenarios, run by <c>-eg-scenario walk|soak|rejoin|join</c> with <c>-eg-record &lt;dir&gt;</c>: the
    /// founder driven along <see cref="Routes.WakeLoop"/> through the scripted input seam, and <c>run.jsonl</c>
    /// (format <c>eg2.run</c>, version 1; the record kinds are in ARCHITECTURE §10) written beside whatever the
    /// run leaves. The clock starts when this component begins, which the bootstrap does just before the
    /// connection, so <c>t</c> in the <c>interactive</c> record is N1's time-to-interactive.
    ///
    /// <para><c>join</c> ends at interactive; <c>walk</c> runs ten minutes; <c>soak</c> thirty; <c>rejoin</c> walks,
    /// cuts the transport without a leave at 45 s and every 30 s after for <c>-eg-cycles</c> cycles, reconnecting
    /// three seconds after each cut. <c>-eg-seconds</c> overrides a scenario's length. The exit code is the
    /// verdict: 0 when the scenario answered its question and no exception was logged.</para>
    ///
    /// <para>Since 2026-09-16 the walker has a body to answer to (FP.1, FP.2): a founder the server has told is thirsty
    /// looks once a second for fresh water standing within <see cref="Drinking.SearchM"/> of them in the streamed tiles,
    /// leaves the loop for it, drinks through the use key until the word is gone (<see cref="Drinking"/>) and walks on
    /// from the waypoint they were bound for, writing a <c>drink</c> record; a founder the server tells has died writes a
    /// <c>died</c> record and, standing at the wake again, walks the loop from its approach. Every <c>sample</c> carries
    /// the water the server last told and the word for it.</para>
    /// </summary>
    public sealed class ScenarioRunner : MonoBehaviour
    {
        private const double CutAtSeconds = 45.0;
        private const double CutIntervalSeconds = 30.0;
        private const double CutLengthSeconds = 3.0;
        private const double JoinTimeoutSeconds = 120.0;
        /// <summary>How long the script is given to bring the crosshair to a pitch before the use key is pressed, s.</summary>
        private const double PitchSettleSeconds = 0.6;
        private const float WalkingPitchDeg = -2f;

        private string _scenario;
        private string _dir;
        private ClientRuntime _runtime;
        private RunLog _log;
        private Stopwatch _clock;
        private RouteFollower _route;
        private ScriptedInputSource _script;
        private PlayerController _player;
        private GameClient _client;
        private double _duration;
        private int _cycles;
        private int _cutsDone;
        private bool _severed;
        private double _reconnectAt = -1.0;
        private double _nextSample = 1.0;
        private int _samples;
        private int _errors;
        private int _corrections;
        private double _firstInteractive = -1.0;
        private double _lastRejoinInteractive = -1.0;
        private int _rejoinsInteractive;
        private string _segment = string.Empty;
        private bool _running;
        // The thirst (2026-09-16): the drink under way, its divert off the loop, and what the run has seen of the body.
        private bool _drinking;
        private Coroutine _drinkRoutine;
        private RouteFollower _divert;
        private string _loopSegment = string.Empty;
        private double _nextDrinkLook;
        private double _retryDrinkAt;
        private int _answers;
        private VerbOutcome _lastAnswer = VerbOutcome.NotNow;
        private int _drinks;
        private int _drinksDone;
        private int _deaths;
        private double _lowestWater = 1.0;

        public static bool IsKnown(string scenario) => scenario == "walk" || scenario == "soak" || scenario == "rejoin" || scenario == "join";

        public void Begin(string scenario, string dir, ClientRuntime runtime, JsonObject header)
        {
            _scenario = scenario;
            _dir = dir;
            _runtime = runtime;
            _clock = Stopwatch.StartNew();
            _cycles = Mathf.Max(1, LaunchArgs.GetInt("cycles", 1));
            switch (scenario)
            {
                case "join": _duration = JoinTimeoutSeconds; break;
                case "walk": _duration = 600.0; break;
                case "soak": _duration = 1800.0; break;
                case "rejoin": _duration = CutAtSeconds + _cycles * CutIntervalSeconds + 15.0; break;
                default: throw new ArgumentException("unknown scenario '" + scenario + "'", nameof(scenario));
            }
            int seconds = LaunchArgs.GetInt("seconds", 0);
            if (seconds > 0) _duration = seconds;
            // A batch-mode player spins as fast as it can; sixty frames a second is what a person's build does.
            if (Application.isBatchMode) Application.targetFrameRate = 60;
            Directory.CreateDirectory(dir);
            _log = RunLog.Open(Path.Combine(dir, "run.jsonl"), header.With("scenario", scenario).With("duration_s", _duration).With("cycles", _cycles));
            Application.logMessageReceived += OnLog;
            _runtime.Welcomed += OnWelcomed;
            _runtime.BecameInteractive += OnInteractive;
            _runtime.Dropped += OnDropped;
            _runtime.TileBuilt += OnTileBuilt;
            _running = true;
            Debug.Log("[scenario] " + scenario + " for " + _duration + " s, logging to " + dir);
        }

        private double T => _clock.Elapsed.TotalSeconds;
        private long Tick => _runtime.Client != null ? _runtime.Client.LastServerTick : -1;
        /// <summary>The founder's water as the server last told it, 1 full (FP.1); a client not yet told holds 1.</summary>
        private double Water => _runtime.Client != null ? _runtime.Client.LastWater01 : 1.0;

        /// <summary>What one tile cost to make ready to draw (M1.4e), as it lands.</summary>
        private void OnTileBuilt(ClientRuntime.TileBuildReport report)
        {
            _log?.Record(T, Tick, "build", new JsonObject().With("what", report.What)
                .With("ix", report.Id.Ix).With("iz", report.Id.Iz)
                .With("worker_ms", report.WorkerMs).With("main_ms", report.MainMs).With("frame_ms", report.FrameMs).With("before_ms", report.BeforeMs)
                .With("texture_ms", report.TextureMs).With("heights_ms", report.HeightsMs)
                .With("object_ms", report.ObjectMs).With("water_ms", report.WaterMs));
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            _errors++;
            _log?.Record(T, Tick, type == LogType.Exception ? "exception" : "error", new JsonObject().With("message", condition).With("stack", stackTrace ?? string.Empty));
        }

        private void OnWelcomed(WelcomeMessage welcome, bool rejoin)
        {
            _log.Record(T, welcome.Tick, "welcome", new JsonObject().With("rejoin", rejoin).With("session", welcome.SessionId).With("seed", welcome.Seed)
                .With("region", welcome.RegionId).With("tick_rate", (int)welcome.TickRate)
                .With("spawn_east", welcome.SpawnEast).With("spawn_up", welcome.SpawnUp).With("spawn_north", welcome.SpawnNorth)
                .With("world_total_hours", welcome.TotalHours));
            if (!rejoin)
            {
                _player = _runtime.Player;
                _script = _player.Script;
                _player.CorrectionApplied += OnCorrection;
                if (_scenario != "join") _route = new RouteFollower(Routes.WakeLoop(), loop: true);
            }
            Listen(_runtime.Client);
        }

        /// <summary>
        /// Hears the body's answers and its death from the client that is connected now: a rejoin connects a new one, so
        /// the runner listens again at every welcome and lets the old one go.
        /// </summary>
        private void Listen(GameClient client)
        {
            if (_client == client) return;
            if (_client != null)
            {
                _client.IntentAnswered -= OnAnswered;
                _client.Died -= OnDied;
            }
            _client = client;
            if (_client != null)
            {
                _client.IntentAnswered += OnAnswered;
                _client.Died += OnDied;
            }
        }

        private void OnInteractive(bool rejoin)
        {
            GameClient c = _runtime.Client;
            double since = T;
            if (rejoin)
            {
                since = Time.realtimeSinceStartupAsDouble - _runtime.ConnectedAtRealtime;
                _lastRejoinInteractive = since;
                _rejoinsInteractive++;
            }
            else if (_firstInteractive < 0.0) _firstInteractive = since;
            _log.Record(T, Tick, "interactive", new JsonObject().With("rejoin", rejoin).With("since_connect_s", since)
                .With("tiles_held", c.Tiles.Held.Count).With("tiles_from_cache", _runtime.TilesFromCache).With("tiles_refused", c.Tiles.RefusedCount)
                .With("tiles_built", _runtime.TilesBuilt).With("worst_streaming_ms", _runtime.WorstStreamingMs)
                .With("bytes_received", c.BytesReceived).With("bytes_sent", c.BytesSent).With("rtt_ms", c.LastRttMs)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North));
            if (_scenario == "join") Finish(0);
        }

        private void OnDropped(string reason)
        {
            _log.Record(T, Tick, "dropped", new JsonObject().With("reason", reason).With("severed", _severed));
        }

        private void OnCorrection(double displacementM, uint sequence, string reason)
        {
            _corrections++;
            _log.Record(T, Tick, "correction", new JsonObject().With("sequence", sequence).With("reason", reason).With("displacement_m", displacementM)
                .With("segment", _segment).With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North));
        }

        private void OnAnswered(IntentResultMessage result)
        {
            _answers++;
            _lastAnswer = result.Outcome;
        }

        /// <summary>
        /// The server has told this founder they died (FP.2): recorded with the sentence the screen shows, and the walk
        /// begun again. The new founder stands at the wake, where the loop's approach starts, so the route is taken from
        /// its first waypoint rather than from wherever the last founder was bound; a drink under way is given up.
        /// </summary>
        private void OnDied(Death death)
        {
            _deaths++;
            _log.Record(T, Tick, "died", new JsonObject().With("cause", death.Cause.ToString()).With("local_hour", death.LocalHour).With("clock", death.Clock)
                .With("water_loss", death.WaterLoss).With("core_c", death.CoreC).With("air_c", death.AirC).With("wind_ms", death.WindMs)
                .With("east", death.East).With("north", death.North).With("segment", _segment).With("sentence", death.Explain()));
            GiveUpDrinking();
            if (_route != null) _route = new RouteFollower(Routes.WakeLoop(), loop: true);
        }

        private void Update()
        {
            if (!_running) return;
            double t = T;
            if (_route != null && _player != null && _script != null && !_severed && !_drinking)
            {
                if (_route.Advance(_player.State.East, _player.State.North, Time.unscaledDeltaTime, out double yaw, out bool sprint))
                {
                    _script.YawTargetDeg = (float)yaw;
                    _script.PitchTargetDeg = WalkingPitchDeg;
                    _script.Move = new Vector2(0f, 1f);
                    _script.Sprint = sprint;
                }
                else
                {
                    _script.Move = Vector2.zero;
                }
                if (_route.Segment != _segment) SetSegment(_route.Segment);
                if (t >= _nextDrinkLook)
                {
                    _nextDrinkLook = t + 1.0;
                    LookForWater(t);
                }
            }
            if (_player != null) _lowestWater = Math.Min(_lowestWater, Water);

            if (_scenario == "rejoin" && _player != null)
            {
                double nextCut = CutAtSeconds + _cutsDone * CutIntervalSeconds;
                if (!_severed && _cutsDone < _cycles && t >= nextCut)
                {
                    _severed = true;
                    _cutsDone++;
                    _reconnectAt = t + CutLengthSeconds;
                    GiveUpDrinking();
                    if (_script != null) _script.Move = Vector2.zero;
                    _log.Record(t, Tick, "cut", new JsonObject().With("cycle", _cutsDone).With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North)
                        .With("mirrors", _runtime.Client.Mirrors.Count));
                    _runtime.Sever();
                }
                else if (_severed && t >= _reconnectAt)
                {
                    _severed = false;
                    _log.Record(t, Tick, "rejoin", new JsonObject().With("cycle", _cutsDone));
                    _runtime.Reconnect();
                }
            }

            if (t >= _nextSample && _player != null)
            {
                _nextSample += 1.0;
                Sample(t);
            }

            if (t >= _duration) Finish(_scenario == "join" ? 1 : (_scenario == "rejoin" && _rejoinsInteractive < _cutsDone ? 1 : 0));
        }

        /// <summary>The segment the founder is on, and a record of the change: the loop's legs, or "water" while a drink takes them off it.</summary>
        private void SetSegment(string name)
        {
            _segment = name;
            _log.Record(T, Tick, "segment", new JsonObject().With("name", _segment).With("index", _route != null ? _route.Index : 0).With("lap", _route != null ? _route.Laps : 0)
                .With("east", _player.State.East).With("north", _player.State.North));
        }

        /// <summary>
        /// Once a second on the loop (2026-09-16): a founder the server has told is thirsty looks for fresh water standing
        /// within <see cref="Drinking.SearchM"/> in the tiles they hold, and goes to drink where they find it; a place that
        /// gave nothing is not looked for again for <see cref="Drinking.RetryAfterSeconds"/>.
        /// </summary>
        private void LookForWater(double t)
        {
            if (!_runtime.Interactive || t < _retryDrinkAt || !Drinking.Wants(Water)) return;
            GameClient c = _runtime.Client;
            if (c?.Grid == null || c.Tiles == null) return;
            MoverState s = _player.State;
            if (!Drinking.FreshWaterNear((layer, id) => c.Tiles.Holding(layer, id), c.Grid, s.East, s.North, Drinking.SearchM, out double east, out double north)) return;
            _drinking = true;
            _drinkRoutine = StartCoroutine(Drink(east, north));
        }

        /// <summary>
        /// The drink (2026-09-16): off the loop to within <see cref="Drinking.StandOffM"/> of the water, going round trunks as
        /// the loop's follower does; then facing it, the crosshair pitched down through <see cref="Drinking.Pitches"/> and the
        /// use key pressed at each until the server answers; a drink pressed again until the word is gone or the visit's
        /// presses are spent (<see cref="Drinking.PressAgain"/>); and one <c>drink</c> record of what came of it. The loop is
        /// then walked on from the waypoint the founder was bound for.
        /// </summary>
        private IEnumerator Drink(double east, double north)
        {
            _drinks++;
            double began = T;
            double before = Water;
            int presses = 0, drank = 0;
            VerbOutcome last = VerbOutcome.NotNow;
            string outcome = "unreached";
            _loopSegment = _segment;
            SetSegment("water");
            _divert = new RouteFollower(new[] { new Waypoint(east, north, "water", false) }, loop: false, reachM: Drinking.StandOffM, stuckSeconds: Drinking.WalkSeconds);
            while (_drinking && !_divert.Finished && _divert.Skipped == 0 && !_severed)
            {
                MoverState s = _player.State;
                if (_divert.Advance(s.East, s.North, Time.unscaledDeltaTime, out double yaw, out _))
                {
                    _script.YawTargetDeg = (float)yaw;
                    _script.PitchTargetDeg = WalkingPitchDeg;
                    _script.Move = new Vector2(0f, 1f);
                    _script.Sprint = false;
                }
                yield return null;
            }
            _script.Move = Vector2.zero;
            if (_drinking && _divert.Finished && !_severed)
            {
                Double3 feet = _player.State.Feet;
                _script.YawTargetDeg = (float)(Math.Atan2(east - feet.X, north - feet.Z) * 180.0 / Math.PI);
                outcome = "no answer";
                foreach (float pitch in Drinking.Pitches)
                {
                    if (!_drinking || _severed) break;
                    _script.PitchTargetDeg = pitch;
                    yield return Wait(PitchSettleSeconds);
                    bool answered = false;
                    do
                    {
                        int at = _answers;
                        // The server tells the body's new water before it answers the press, so what it was is taken first.
                        double was = Water;
                        _script.Use();
                        double from = T;
                        while (_drinking && _answers == at && T < from + Drinking.AnswerSeconds) yield return null;
                        if (_answers == at) break;                       // the crosshair met no water at this pitch
                        answered = true;
                        presses++;
                        last = _lastAnswer;
                        if (last == VerbOutcome.Done)
                        {
                            drank++;
                            // The next press is judged on the new water, once told.
                            double told = T;
                            while (_drinking && Water == was && T < told + Drinking.AnswerSeconds) yield return null;
                        }
                    } while (_drinking && Drinking.PressAgain(last, Water, presses));
                    if (answered)
                    {
                        outcome = drank > 0 ? "drank" : last.ToString();
                        break;
                    }
                }
            }
            MoverState end = _player.State;
            _log.Record(T, Tick, "drink", new JsonObject().With("outcome", outcome).With("last", last.ToString()).With("presses", presses).With("drinks", drank)
                .With("water_before", before).With("water_after", Water).With("seconds", T - began)
                .With("water_east", east).With("water_north", north).With("east", end.East).With("up", end.Up).With("north", end.North));
            _drinksDone += drank;
            if (drank == 0) _retryDrinkAt = T + Drinking.RetryAfterSeconds;
            _script.PitchTargetDeg = WalkingPitchDeg;
            _divert = null;
            _drinkRoutine = null;
            if (_drinking)
            {
                _drinking = false;
                SetSegment(_loopSegment);
            }
        }

        private IEnumerator Wait(double seconds)
        {
            double until = T + seconds;
            while (T < until) yield return null;
        }

        /// <summary>A drink under way is given up: at a cut, and at a death, when the founder is no longer where the water was.</summary>
        private void GiveUpDrinking()
        {
            if (!_drinking) return;
            _drinking = false;
            if (_drinkRoutine != null) StopCoroutine(_drinkRoutine);
            _drinkRoutine = null;
            _divert = null;
            _retryDrinkAt = 0.0;
            if (_script != null)
            {
                _script.Move = Vector2.zero;
                _script.PitchTargetDeg = WalkingPitchDeg;
            }
        }

        /// <summary>One second's record of the founder and of every mirror, the mirror's digest keyed by its newest tick.</summary>
        private void Sample(double t)
        {
            _samples++;
            GameClient c = _runtime.Client;
            MoverState s = _player.State;
            double water = Water;
            _log.Record(t, Tick, "sample", new JsonObject().With("east", s.East).With("up", s.Up).With("north", s.North)
                .With("grounded", s.Grounded).With("wading", s.Wading).With("speed", s.HorizontalSpeed).With("segment", _segment)
                .With("water", water).With("thirst", Hydration.WordFor(Hydration.LevelOf(water))).With("drinking", _drinking)
                .With("remotes", c.Mirrors.Count).With("entities_shared", SharedEntities(c, s)).With("rtt_ms", c.LastRttMs).With("bytes_sent", c.BytesSent).With("bytes_received", c.BytesReceived)
                .With("corrections", _corrections).With("interactive", _runtime.Interactive).With("connection", _runtime.Joins)
                .With("fps", Time.unscaledDeltaTime > 0f ? 1.0 / Time.unscaledDeltaTime : 0.0));
            long nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
            foreach (KeyValuePair<uint, RemoteMirror> pair in c.Mirrors)
            {
                if (pair.Value.Count == 0) continue;
                MirrorSample m;
                bool sampled = c.TrySampleMirror(pair.Key, nowMs, out m);
                PlayerStateMessage latest = pair.Value.Latest;
                _log.Record(t, Tick, "mirror", new JsonObject().With("session", pair.Key).With("latest_tick", latest.ServerTick).With("digest", c.MirrorDigest(pair.Key))
                    .With("latest_east", latest.Body.East).With("latest_up", latest.Body.Up).With("latest_north", latest.Body.North)
                    .With("east", sampled ? m.East : latest.Body.East).With("up", sampled ? m.Up : latest.Body.Up).With("north", sampled ? m.North : latest.Body.North)
                    .With("at_tick", sampled ? m.AtTick : (double)latest.ServerTick).With("interpolated", sampled && m.Interpolated)
                    .With("estimated_tick", c.EstimatedServerTick(nowMs)).With("states_held", pair.Value.Count));
            }
        }

        /// <summary>
        /// How many of the entities this client holds lie inside its founder's interest radius and inside every other
        /// player's it holds (M1.7a): what N4 asks both clients to agree on, by the radius the server's Welcome states, each
        /// other player where their newest state puts them. With no other player held, the ones inside the founder's own.
        /// </summary>
        private static int SharedEntities(GameClient c, MoverState self)
        {
            double r2 = c.Welcome.InterestRadiusM * c.Welcome.InterestRadiusM;
            int shared = 0;
            foreach (EntityView v in c.Entities.Views.Values)
            {
                if (!InsideRadius(v.Position, self.East, self.North, r2)) continue;
                bool everyone = true;
                foreach (RemoteMirror m in c.Mirrors.Values)
                    if (m.Count > 0 && !InsideRadius(v.Position, m.Latest.Body.East, m.Latest.Body.North, r2))
                    {
                        everyone = false;
                        break;
                    }
                if (everyone) shared++;
            }
            return shared;
        }

        private static bool InsideRadius(Double3 at, double east, double north, double r2)
        {
            double dx = at.X - east, dz = at.Z - north;
            return dx * dx + dz * dz <= r2;
        }

        private void Finish(int exitCode)
        {
            if (!_running) return;
            _running = false;
            GiveUpDrinking();
            _log.Record(T, Tick, "end", new JsonObject().With("exit", exitCode).With("samples", _samples).With("errors", _errors).With("corrections", _corrections)
                .With("interactive_s", _firstInteractive).With("rejoin_interactive_s", _lastRejoinInteractive).With("cuts", _cutsDone).With("rejoins_interactive", _rejoinsInteractive)
                .With("laps", _route != null ? _route.Laps : 0).With("skipped_waypoints", _route != null ? _route.Skipped : 0)
                .With("moves_sent", _player != null ? (int)_player.MovesSent : 0)
                .With("drinks", _drinks).With("drinks_done", _drinksDone).With("deaths", _deaths).With("lowest_water", _lowestWater)
                .With("east", _player != null ? _player.State.East : 0.0).With("up", _player != null ? _player.State.Up : 0.0).With("north", _player != null ? _player.State.North : 0.0));
            Application.logMessageReceived -= OnLog;
            Listen(null);
            _log.Dispose();
            _log = null;
            Debug.Log("[scenario] " + _scenario + " done: exit " + exitCode + ", " + _samples + " sample(s), " + _errors + " error(s), " + _corrections + " correction(s), "
                      + _drinks + " drink(s), " + _deaths + " death(s), " + _dir);
            Application.Quit(exitCode);
        }

        private void OnDestroy()
        {
            if (_running) Finish(1);
        }
    }
}
