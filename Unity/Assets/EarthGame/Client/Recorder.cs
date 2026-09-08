using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using EarthGame.ClientCore;
using EarthGame.Engine;
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
    /// logged, 1 otherwise; the last record says which.
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

        public void Begin(string dir, Camera camera, PlayerController player, ScriptedInputSource script, HudController hud, Func<long> serverTick, JsonObject header)
        {
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
            _log.Record(T, Tick, "end", new JsonObject().With("frames", _frames).With("errors", _errors)
                .With("corrections", _player.Corrections).With("moves_sent", (int)_player.MovesSent)
                .With("east", _player.State.East).With("up", _player.State.Up).With("north", _player.State.North));
            _running = false;
            Finish(_errors == 0 && _frames == 3 * Sizes.Length ? 0 : 1);
        }

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
