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

        /// <summary>
        /// The carrying frames (M1.5a), run by <c>-eg-scenario carry</c> with <c>-eg-items</c>: a stick lying within reach
        /// looked at, picked up and held, the carrying window open, the ground ahead aimed at, and the stick put down.
        /// </summary>
        public const string CarryScenario = "carry";

        /// <summary>
        /// The litter frames (M1.5b), run by <c>-eg-scenario litter</c>: a stick of the world's own litter within reach
        /// looked at, taken up, gone from the ground and in the hand, and put down again as an item.
        /// </summary>
        public const string LitterScenario = "litter";

        /// <summary>Whether a scenario is one of this recorder's, which write frames, rather than the runner's.</summary>
        public static bool IsKnown(string scenario) => scenario == Scenario || scenario == CarryScenario || scenario == LitterScenario;
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
        private string _scenario = Scenario;
        private GameClient _client;
        private VerbController _verbs;
        private readonly List<string> _answers = new List<string>();

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

        public void Begin(string dir, Camera camera, PlayerController player, ScriptedInputSource script, HudController hud, Func<long> serverTick, JsonObject header, Func<bool> ready = null, Func<int> trees = null, Func<double> standCpu = null,
                          string scenario = Scenario, GameClient client = null, VerbController verbs = null)
        {
            _scenario = IsKnown(scenario) ? scenario : Scenario;
            _client = client;
            _verbs = verbs;
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
            _log = RunLog.Open(Path.Combine(dir, "run.jsonl"), header.With("scenario", _scenario));
            Application.logMessageReceived += OnLog;
            _running = true;
            StartCoroutine(_scenario == CarryScenario ? RunCarry() : _scenario == LitterScenario ? RunLitter() : Run());
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
            // The founder wakes facing the morning sun (north-east), looking a little above the horizon, then walks.
            _script.YawTargetDeg = 60f;
            _script.PitchTargetDeg = -4f;
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
            _script.PitchTargetDeg = 2f;
            yield return Wait(2.5);
            yield return Capture("turn");
            string hold = LaunchArgs.Get("hold", null);
            if (!string.IsNullOrEmpty(hold) && double.TryParse(hold, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0.0)
                yield return Hold(seconds);
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North)));
            _running = false;
            Finish(_errors == 0 && _frames == 3 * Sizes.Length ? 0 : 1);
        }

        /// <summary>
        /// The carrying frames (M1.5a promise 9): a stick of <c>-eg-items</c> lying within reach looked at ("look"), picked
        /// up and held ("held"), the carrying window opened ("tab"), the ground a couple of metres ahead aimed at ("aim"),
        /// and the stick put down there, fallen and at rest ("put"); then a cobble is picked up and kept, so the world saved
        /// on the way out has something carried for save_check to read. The end record says whether each verb was done
        /// and every answer the server gave; the exit is 0 when every frame was written, every verb was done and nothing
        /// was logged as an error.
        /// </summary>
        private IEnumerator RunCarry()
        {
            if (_client == null || _verbs == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the carry scenario has no client or no verbs to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            _client.IntentAnswered += OnAnswered;
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            EntityView stick = null;
            double until = T + 20.0;
            while ((stick = Nearest(DefinitionCatalogue.Stick)) == null && T < until) yield return null;
            if (stick == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no stick of -eg-items lay within reach"));
                _running = false;
                Finish(1);
                yield break;
            }
            ulong id = stick.Id.Value;
            Face(stick.Position);
            until = T + 4.0;
            while (T < until && (_verbs.Target == null || _verbs.Target.Id.Value != id)) yield return null;
            yield return Wait(0.4);
            yield return Capture("look");
            _script.Use();
            until = T + 4.0;
            while (T < until && !Carries(id)) yield return null;
            bool picked = Carries(id);
            yield return Wait(0.6);
            yield return Capture("held");
            _script.ToggleCarrying();
            yield return Wait(0.4);
            yield return Capture("tab");
            _script.ToggleCarrying();
            // The ground a couple of metres ahead: well within reach of a standing founder's eye.
            _script.PitchTargetDeg = 36f;
            until = T + 4.0;
            while (T < until && !_verbs.Ground.HasValue) yield return null;
            yield return Wait(0.6);
            yield return Capture("aim");
            _script.Use();
            until = T + 6.0;
            bool put = false;
            while (T < until && !(put = Lies(id))) yield return null;
            // What is drawn is the mirrors' delay behind the server: the rest is seen a moment after it is told.
            yield return Wait(0.6);
            yield return Capture("put");
            bool kept = false;
            EntityView cobble = Nearest(DefinitionCatalogue.Cobble);
            if (cobble != null)
            {
                ulong stone = cobble.Id.Value;
                Face(cobble.Position);
                until = T + 4.0;
                while (T < until && (_verbs.Target == null || _verbs.Target.Id.Value != stone)) yield return null;
                _script.Use();
                until = T + 4.0;
                while (T < until && !(kept = Carries(stone))) yield return null;
            }
            _client.IntentAnswered -= OnAnswered;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("picked_up", picked).With("put_down", put).With("kept_carried", kept).With("answers", string.Join(",", _answers))
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North)));
            _running = false;
            Finish(_errors == 0 && picked && put && kept && _frames == 5 * Sizes.Length ? 0 : 1);
        }

        /// <summary>
        /// The litter frames (M1.5b promise 7): a stick of the world's own litter within reach looked at ("litter-look"),
        /// taken up, gone from the ground and in the hand ("litter-held"), and put down on the ground ahead as an item
        /// ("litter-put"); then another is taken and kept, so the world saved on the way out has takings and something
        /// carried for save_check to read. The exit is 0 when every frame was written, every verb was done and nothing was
        /// logged as an error.
        /// </summary>
        private IEnumerator RunLitter()
        {
            if (_client == null || _verbs == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "the litter scenario has no client or no verbs to drive"));
                _running = false;
                Finish(1);
                yield break;
            }
            _client.IntentAnswered += OnAnswered;
            double from = T;
            while (_ready != null && !_ready() && T < from + ReadyTimeoutSeconds) yield return null;
            LyingNearby? found = null;
            double until = T + 20.0;
            while ((found = NearestLying(StandLayout.Kind.Stick, default)) == null && T < until) yield return null;
            if (found == null)
            {
                _errors++;
                _log.Record(T, Tick, "error", new JsonObject().With("message", "no stick of the litter lay within reach"));
                _running = false;
                Finish(1);
                yield break;
            }
            LyingThing first = found.Value.Thing;
            Face(At(found.Value));
            until = T + 4.0;
            while (T < until && !(_verbs.TargetLying.HasValue && _verbs.TargetLying.Value.Equals(first))) yield return null;
            yield return Wait(0.4);
            yield return Capture("litter-look");
            _script.Use();
            until = T + 4.0;
            while (T < until && !(_client.Taken.IsTaken(first) && InHand() != 0)) yield return null;
            ulong item = InHand();
            bool picked = _client.Taken.IsTaken(first) && item != 0;
            // The tile is placed again, less the stick, on a worker; a moment lets it be swapped in.
            yield return Wait(1.0);
            yield return Capture("litter-held");
            _script.PitchTargetDeg = 36f;
            until = T + 4.0;
            while (T < until && !_verbs.Ground.HasValue) yield return null;
            yield return Wait(0.6);
            _script.Use();
            until = T + 6.0;
            bool put = false;
            while (T < until && !(put = item != 0 && Lies(item))) yield return null;
            yield return Wait(0.8);
            yield return Capture("litter-put");
            bool kept = false;
            LyingNearby? second = NearestLying(StandLayout.Kind.Stick, first);
            if (second != null)
            {
                LyingThing thing = second.Value.Thing;
                Face(At(second.Value));
                until = T + 4.0;
                while (T < until && !(_verbs.TargetLying.HasValue && _verbs.TargetLying.Value.Equals(thing))) yield return null;
                _script.Use();
                until = T + 4.0;
                while (T < until && !(kept = _client.Taken.IsTaken(thing) && InHand() != 0)) yield return null;
            }
            _client.IntentAnswered -= OnAnswered;
            _log.Record(T, Tick, "end", WithFeet(new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("picked_up", picked).With("put_down", put).With("kept_carried", kept).With("answers", string.Join(",", _answers))
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North)));
            _running = false;
            Finish(_errors == 0 && picked && put && kept && _frames == 3 * Sizes.Length ? 0 : 1);
        }

        private readonly List<LyingNearby> _near = new List<LyingNearby>();

        /// <summary>The nearest thing of a kind lying in the litter well within reach of the founder's eye, other than one, or null.</summary>
        private LyingNearby? NearestLying(StandLayout.Kind kind, LyingThing except)
        {
            Double3 eye = _player.Eye;
            _near.Clear();
            LyingNear.Find(eye.X, eye.Z, Hands.ReachM, _client.Tiles, _client.Grid, _client.Taken, _near);
            LyingNearby? best = null;
            double bestM = Hands.ReachM - 0.5;
            foreach (LyingNearby n in _near)
            {
                if (n.Thing.Kind != kind || n.Thing.Equals(except)) continue;
                double d = Double3.Distance(eye, At(n));
                if (d > bestM) continue;
                bestM = d;
                best = n;
            }
            return best;
        }

        private static Double3 At(LyingNearby n) => new Double3(n.Instance.East, n.Instance.Up, n.Instance.North);

        /// <summary>The id of the thing in the hand, 0 when the hand is empty.</summary>
        private ulong InHand()
        {
            CarryingMessage carrying = _client.Carrying;
            if (carrying.Things != null && carrying.Hand != 0)
                foreach (CarriedThing t in carrying.Things)
                    if (t.Place == carrying.Hand) return t.Id;
            return 0;
        }

        private void OnAnswered(IntentResultMessage result) => _answers.Add(result.Outcome.ToString());

        /// <summary>An end record with the feet that fell in the run and what they fell on (M1.5c).</summary>
        private JsonObject WithFeet(JsonObject end)
        {
            ClientRuntime runtime = GetComponent<ClientRuntime>();
            return runtime == null ? end : end.With("footfalls", runtime.Footfalls).With("underfoot", runtime.HeardUnderfoot());
        }

        /// <summary>The nearest thing of a kind lying at rest well within reach of the founder's eye, or null.</summary>
        private EntityView Nearest(Definition kind)
        {
            Double3 eye = _player.Eye;
            EntityView best = null;
            double bestM = Hands.ReachM - 0.25;
            foreach (EntityView v in _client.Entities.Views.Values)
            {
                if (!ReferenceEquals(v.Definition, kind) || !v.Item.Resting) continue;
                double d = Double3.Distance(eye, v.Position);
                if (d > bestM) continue;
                bestM = d;
                best = v;
            }
            return best;
        }

        /// <summary>Turns the scripted founder to look at a point: yaw clockwise from north, pitch positive down, as the camera takes them.</summary>
        private void Face(Double3 at)
        {
            Double3 eye = _player.Eye;
            double dx = at.X - eye.X, dz = at.Z - eye.Z;
            _script.YawTargetDeg = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            _script.PitchTargetDeg = (float)(Math.Atan2(eye.Y - at.Y, Math.Sqrt(dx * dx + dz * dz)) * 180.0 / Math.PI);
        }

        private bool Carries(ulong id)
        {
            CarriedThing[] things = _client.Carrying.Things;
            if (things != null)
                foreach (CarriedThing t in things)
                    if (t.Id == id) return true;
            return false;
        }

        private bool Lies(ulong id) => _client.Entities.Views.TryGetValue(id, out EntityView v) && v.Item.Resting;

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
