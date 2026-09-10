using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// Optional off-screen startup evidence (-eg-loading-record). The independent check reads eg2.loading
    /// v1 (ARCHITECTURE section 10) and the pixels, never this recorder's own verdict.
    /// </summary>
    public sealed class LoadingRecorder : MonoBehaviour
    {
        private LoadingController _screen;
        private string _dir, _error;
        private bool _ready, _returnedToMenu, _pending = true;
        private double _started, _nextSample, _preparedAt;
        private int _updates;
        private readonly List<object> _samples = new List<object>();
        private readonly List<object> _frames = new List<object>();
        private readonly List<object> _stages = new List<object>();

        public void Begin(string dir, LoadingController screen)
        {
            _dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(Path.Combine(_dir, "frames"));
            _screen = screen;
            _started = Time.realtimeSinceStartupAsDouble;
            StartCoroutine(Record());
        }

        public void Stage(string stage) => _stages.Add(stage);

        public void Prepared()
        {
            Sample();
            _preparedAt = Time.realtimeSinceStartupAsDouble - _started;
            _pending = false;
        }

        public void Ready() { _ready = true; }
        public void ReturnedToMenu() { _returnedToMenu = true; }
        public void Failed(string reason) { _error = reason; _pending = false; }

        private void Update()
        {
            if (!_pending) return;
            _updates++;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextSample) return;
            _nextSample = now + 0.25;
            Sample();
        }

        private void Sample()
        {
            _samples.Add(new JsonObject().With("t", Time.realtimeSinceStartupAsDouble - _started)
                .With("update", _updates).With("stage", _screen != null ? _screen.Stage : ""));
        }

        private IEnumerator Record()
        {
            // Give UI Toolkit a layout pass. Fast continuation may already be playable by this point;
            // its evidence needs no loading frame, and recording never holds back normal startup.
            yield return null;
            if (_screen != null && _error == null) yield return Capture("loading");
            while (!_ready && _error == null) yield return null;
            if (_error != null)
            {
                yield return Capture("failed");
                _screen.ClickBack();
                yield return null;
            }
            var evidence = new JsonObject().With("format", "eg2.loading").With("version", 1)
                .With("outcome", _error != null ? "failed" : "ready").With("error", _error ?? "")
                .With("returned_to_menu", _returnedToMenu).With("preparation_s", _preparedAt).With("updates", _updates)
                .With("samples", _samples).With("stages", _stages).With("frames", _frames);
            File.WriteAllText(Path.Combine(_dir, "loading.json"), Json.Write(evidence, true));
            if (_error != null) Application.Quit(1);
        }

        private IEnumerator Capture(string name)
        {
            foreach (var size in new[] { (Width: 2560, Height: 1440), (Width: 1920, Height: 1080) })
            {
                if (_screen == null || _screen.Panel == null) yield break;
                var panel = _screen.Panel;
                RenderTexture previousTarget = panel.targetTexture;
                bool previousClear = panel.clearColor;
                RenderTexture rt = new RenderTexture(size.Width, size.Height, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                GameObject cameraObject = new GameObject("Loading capture");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                Texture2D pixels = null;
                try
                {
                    panel.targetTexture = rt;
                    panel.clearColor = false;
                    var request = new RenderPipeline.StandardRequest { destination = rt };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request))
                        throw new InvalidOperationException("The pipeline refused a loading capture.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    yield return new WaitForEndOfFrame();
                    pixels = new Texture2D(size.Width, size.Height, TextureFormat.RGB24, false);
                    RenderTexture previousActive = RenderTexture.active;
                    RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0, 0, size.Width, size.Height), 0, 0);
                    pixels.Apply(false);
                    RenderTexture.active = previousActive;
                    string file = "frames/" + name + "_" + size.Height + "p.png";
                    File.WriteAllBytes(Path.Combine(_dir, file), pixels.EncodeToPNG());
                    _frames.Add(new JsonObject().With("file", file).With("width", size.Width)
                        .With("height", size.Height).With("stage", _screen != null ? _screen.Stage : "")
                        .With("t", Time.realtimeSinceStartupAsDouble - _started));
                }
                finally
                {
                    if (panel != null) { panel.targetTexture = previousTarget; panel.clearColor = previousClear; }
                    if (pixels != null) Destroy(pixels);
                    rt.Release();
                    Destroy(rt);
                    Destroy(cameraObject);
                }
            }
        }
    }
}
