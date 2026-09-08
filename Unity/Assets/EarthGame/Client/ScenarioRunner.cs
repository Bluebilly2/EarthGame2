using System;
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
    /// </summary>
    public sealed class ScenarioRunner : MonoBehaviour
    {
        private const double CutAtSeconds = 45.0;
        private const double CutIntervalSeconds = 30.0;
        private const double CutLengthSeconds = 3.0;
        private const double JoinTimeoutSeconds = 120.0;

        private string _scenario;
        private string _dir;
        private ClientRuntime _runtime;
        private RunLog _log;
        private Stopwatch _clock;
        private RouteFollower _route;
        private ScriptedInputSource _script;
        private PlayerController _player;
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
            _running = true;
            Debug.Log("[scenario] " + scenario + " for " + _duration + " s, logging to " + dir);
        }

        private double T => _clock.Elapsed.TotalSeconds;
        private long Tick => _runtime.Client != null ? _runtime.Client.LastServerTick : -1;

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
                .With("tiles_built", _runtime.TilesBuilt).With("bytes_received", c.BytesReceived).With("bytes_sent", c.BytesSent).With("rtt_ms", c.LastRttMs)
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

        private void Update()
        {
            if (!_running) return;
            double t = T;
            if (_route != null && _player != null && _script != null && !_severed)
            {
                if (_route.Advance(_player.State.East, _player.State.North, Time.unscaledDeltaTime, out double yaw, out bool sprint))
                {
                    _script.YawTargetDeg = (float)yaw;
                    _script.PitchTargetDeg = 2f;
                    _script.Move = new Vector2(0f, 1f);
                    _script.Sprint = sprint;
                }
                else
                {
                    _script.Move = Vector2.zero;
                }
                if (_route.Segment != _segment)
                {
                    _segment = _route.Segment;
                    _log.Record(t, Tick, "segment", new JsonObject().With("name", _segment).With("index", _route.Index).With("lap", _route.Laps)
                        .With("east", _player.State.East).With("north", _player.State.North));
                }
            }

            if (_scenario == "rejoin" && _player != null)
            {
                double nextCut = CutAtSeconds + _cutsDone * CutIntervalSeconds;
                if (!_severed && _cutsDone < _cycles && t >= nextCut)
                {
                    _severed = true;
                    _cutsDone++;
                    _reconnectAt = t + CutLengthSeconds;
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

        /// <summary>One second's record of the founder and of every mirror, the mirror's digest keyed by its newest tick.</summary>
        private void Sample(double t)
        {
            _samples++;
            GameClient c = _runtime.Client;
            MoverState s = _player.State;
            _log.Record(t, Tick, "sample", new JsonObject().With("east", s.East).With("up", s.Up).With("north", s.North)
                .With("grounded", s.Grounded).With("wading", s.Wading).With("speed", s.HorizontalSpeed).With("segment", _segment)
                .With("remotes", c.Mirrors.Count).With("rtt_ms", c.LastRttMs).With("bytes_sent", c.BytesSent).With("bytes_received", c.BytesReceived)
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

        private void Finish(int exitCode)
        {
            if (!_running) return;
            _running = false;
            _log.Record(T, Tick, "end", new JsonObject().With("exit", exitCode).With("samples", _samples).With("errors", _errors).With("corrections", _corrections)
                .With("interactive_s", _firstInteractive).With("rejoin_interactive_s", _lastRejoinInteractive).With("cuts", _cutsDone).With("rejoins_interactive", _rejoinsInteractive)
                .With("laps", _route != null ? _route.Laps : 0).With("skipped_waypoints", _route != null ? _route.Skipped : 0)
                .With("moves_sent", _player != null ? (int)_player.MovesSent : 0)
                .With("east", _player != null ? _player.State.East : 0.0).With("up", _player != null ? _player.State.Up : 0.0).With("north", _player != null ? _player.State.North : 0.0));
            Application.logMessageReceived -= OnLog;
            _log.Dispose();
            _log = null;
            Debug.Log("[scenario] " + _scenario + " done: exit " + exitCode + ", " + _samples + " sample(s), " + _errors + " error(s), " + _corrections + " correction(s), " + _dir);
            Application.Quit(exitCode);
        }

        private void OnDestroy()
        {
            if (_running) Finish(1);
        }
    }
}
