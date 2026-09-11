using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace EarthGame.Client
{
    /// <summary>
    /// The first-frame scenario, run by <c>-eg-record &lt;dir&gt;</c>: a scripted founder at the wake, frames of the
    /// main camera rendered off-screen at 1440p and 1080p at stated instants, and <c>run.jsonl</c> (format
    /// <c>eg2.run</c>, version 1) beside them. The process shows no window (the launcher runs it with -batchmode)
    /// and makes no sound. The exit code is the verdict: 0 when every frame was written and no exception was
    /// logged, 1 otherwise; the last record says which. With <c>-eg-hold &lt;seconds&gt;</c> it then measures what a
    /// frame costs (<see cref="Hold"/>).
    /// </summary>
    public sealed class Recorder : MonoBehaviour
    {
        public const string Scenario = "first-frame";
        private static readonly (int Width, int Height, string Tag)[] Sizes = { (2560, 1440, "1440p"), (1920, 1080, "1080p") };

        private string _dir;
        private RunLog _log;
        private Camera _camera;
        private PlayerController _player;
        private ScriptedInputSource _script;
        private HudController _hud;
        private Func<long> _tick;
        private Stopwatch _clock;
        private int _frames;
        private int _errors;
        private bool _running;

        /// <summary>What one tile cost to make ready to draw (M1.4e); the runtime reports each one as it lands.</summary>
        public void RecordBuild(ClientRuntime.TileBuildReport report)
        {
            _log?.Record(T, Tick, "build", new JsonObject().With("what", report.What)
                .With("ix", report.Id.Ix).With("iz", report.Id.Iz)
                .With("worker_ms", report.WorkerMs).With("main_ms", report.MainMs).With("frame_ms", report.FrameMs).With("before_ms", report.BeforeMs)
                .With("texture_ms", report.TextureMs).With("heights_ms", report.HeightsMs)
                .With("object_ms", report.ObjectMs).With("water_ms", report.WaterMs));
        }

        /// <summary>
        /// The longest the first frame waits for the country to be ready, s. What stands on the ground is placed on a
        /// worker once its tiles arrive (M1.6a); a world made before it has nothing to place, and waits this long.
        /// </summary>
        public const double ReadyTimeoutSeconds = 15.0;

        private Func<bool> _ready;
        private Func<int> _trees;
        private Func<double> _standCpu;

        public void Begin(string dir, Camera camera, PlayerController player, ScriptedInputSource script, HudController hud, Func<long> serverTick, JsonObject header, Func<bool> ready = null, Func<int> trees = null, Func<double> standCpu = null)
        {
            _ready = ready;
            _trees = trees;
            _standCpu = standCpu;
            _dir = dir;
            _camera = camera;
            _player = player;
            _script = script;
            _hud = hud;
            _tick = serverTick;
            _clock = Stopwatch.StartNew();
            Directory.CreateDirectory(Path.Combine(dir, "frames"));
            _log = RunLog.Open(Path.Combine(dir, "run.jsonl"), header.With("scenario", Scenario));
            Application.logMessageReceived += OnLog;
            _running = true;
            StartCoroutine(Run());
        }

        private double T => _clock.Elapsed.TotalSeconds;
        private long Tick => _tick != null ? _tick() : -1;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            _errors++;
            _log?.Record(T, Tick, type == LogType.Exception ? "exception" : "error", new JsonObject().With("message", condition).With("stack", stackTrace ?? string.Empty));
        }

        private IEnumerator Run()
        {
            // The founder wakes facing the morning sun (north-east), looks at the ground ahead, then walks.
            _script.YawTargetDeg = 60f;
            _script.PitchTargetDeg = 4f;
            // The frames are of the country, so the first waits until what stands on it is placed (M1.6a).
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            UnityEngine.Debug.Log("[recorder] the country was ready after " + (T - from).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s"
                                  + (_ready == null || _ready() ? "" : "; it was not, and the frames go ahead"));
            yield return Wait(1.5);
            yield return Capture("wake");
            _script.Move = new Vector2(0f, 1f);
            _script.Sprint = false;
            yield return Wait(4.0);
            yield return Capture("walk");
            _script.Move = Vector2.zero;
            _script.YawTargetDeg = 200f;
            _script.PitchTargetDeg = -2f;
            yield return Wait(2.5);
            yield return Capture("turn");
            string hold = LaunchArgs.Get("hold", null);
            if (!string.IsNullOrEmpty(hold) && double.TryParse(hold, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0.0)
                yield return Hold(seconds);
            _log.Record(T, Tick, "end", new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North));
            _running = false;
            Finish(_errors == 0 && _frames == 3 * Sizes.Length ? 0 : 1);
        }

        /// <summary>
        /// What a frame costs, measured without a window (M1.6a): the founder turns a full circle over the given seconds,
        /// and every frame the world is rendered into a 1080p target and the GPU is waited for with a one-pixel read, so
        /// a frame's time is the CPU's work and then the GPU's, never overlapped. One <c>timing</c> record gives the
        /// median, the 95th percentile and the worst, and the median of what choosing and handing over the stand cost
        /// the main thread; runs with and without <c>-eg-hide trees</c> (or <c>near</c>, <c>far</c>) and
        /// <c>-eg-hide loose</c> part what those cost from the rest. ARCHITECTURE §8's protocol asks for a visible
        /// window, which no automated run opens; this is the measure that can be taken without one.
        /// </summary>
        private IEnumerator Hold(double seconds)
        {
            const int width = 1920, height = 1080;
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = rt };
            List<double> times = new List<double>();
            List<double> standCpu = new List<double>();
            float yaw = _script.YawTargetDeg;
            double from = T, last = T;
            while (T < from + seconds)
            {
                _script.YawTargetDeg = yaw + (float)(360.0 * (T - from) / seconds);
                yield return new WaitForEndOfFrame();
                if (_standCpu != null) standCpu.Add(_standCpu());
                RenderPipeline.SubmitRenderRequest(_camera, request);
                RenderTexture.active = rt;
                pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                RenderTexture.active = null;
                double now = T;
                times.Add((now - last) * 1000.0);
                last = now;
            }
            // The first interval began before the circle did.
            if (times.Count > 1) times.RemoveAt(0);
            times.Sort();
            standCpu.Sort();
            _log.Record(T, Tick, "timing", new JsonObject().With("seconds", seconds).With("frames", times.Count)
                .With("stand_cpu_ms", Rank(standCpu, 0.5))
                .With("width", width).With("height", height)
                .With("median_ms", Rank(times, 0.5)).With("p95_ms", Rank(times, 0.95)).With("worst_ms", times.Count > 0 ? times[times.Count - 1] : 0.0)
                .With("hidden", LaunchArgs.Get("hide", "")).With("trees", _trees != null ? _trees() : -1));
            Destroy(pixel);
            rt.Release();
            Destroy(rt);
        }

        /// <summary>The value at a share of the way up a sorted list, by nearest rank.</summary>
        private static double Rank(List<double> sorted, double share) =>
            sorted.Count == 0 ? 0.0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Round(share * (sorted.Count - 1)))];

        private IEnumerator Wait(double seconds)
        {
            double until = T + seconds;
            while (T < until) yield return null;
        }

        private IEnumerator Capture(string name)
        {
            foreach (var size in Sizes)
            {
                RenderTexture rt = new RenderTexture(size.Width, size.Height, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 1;
                rt.Create();
                PanelSettings panel = _hud != null ? _hud.Panel : null;
                RenderTexture previousPanelTarget = null;
                bool previousPanelClear = true;
                if (panel != null)
                {
                    previousPanelTarget = panel.targetTexture;
                    previousPanelClear = panel.clearColor;
                    panel.targetTexture = rt;
                    panel.clearColor = false;
                }
                // A batch-mode player renders nothing to a screen it does not have, so the world is asked for
                // explicitly, into the texture; the HUD panel then draws over it at the end of this same frame.
                RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest();
                request.destination = rt;
                if (RenderPipeline.SupportsRenderRequest(_camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(_camera, request);
                }
                else
                {
                    _errors++;
                    _log.Record(T, Tick, "error", new JsonObject().With("message", "the render pipeline refused a StandardRequest for the main camera"));
                }
                yield return new WaitForEndOfFrame();
                Texture2D tex = new Texture2D(size.Width, size.Height, TextureFormat.RGB24, false);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, size.Width, size.Height), 0, 0);
                tex.Apply(false);
                RenderTexture.active = null;
                if (panel != null)
                {
                    panel.targetTexture = previousPanelTarget;
                    panel.clearColor = previousPanelClear;
                }
                string file = "frames/" + name + "_" + size.Tag + ".png";
                File.WriteAllBytes(Path.Combine(_dir, file), tex.EncodeToPNG());
                Destroy(tex);
                rt.Release();
                Destroy(rt);
                _frames++;
                _log.Record(T, Tick, "frame", new JsonObject().With("file", file).With("width", size.Width).With("height", size.Height)
                    .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North)
                    .With("yaw_deg", (double)_player.YawDeg).With("pitch_deg", (double)_player.PitchDeg)
                    .With("grounded", _player.State.Grounded).With("corrections", _player.Corrections));
            }
        }

        private void Finish(int exitCode)
        {
            Application.logMessageReceived -= OnLog;
            _log?.Dispose();
            _log = null;
            Debug.Log("[recorder] done: " + _frames + " frame(s), " + _errors + " error(s), exit " + exitCode + ", " + _dir);
            Application.Quit(exitCode);
        }

        private void OnDestroy()
        {
            if (_running) Finish(1);
        }
    }
}
