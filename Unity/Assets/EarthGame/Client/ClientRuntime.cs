using System;
using System.Globalization;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Shared;
using EarthGame.Transport;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The Unity wrapper around the engine-free <see cref="GameClient"/>: pumps it once per frame against Unity's
    /// real-time clock, and on Welcome builds everything that draws the world: the terrain tile around the
    /// spawn, the sun on the mirrored clock, the founder's body and camera, the HUD, and the recorder when a
    /// scenario asked for one. This assembly never references EarthGame.Server; the asmdef enforces it.
    /// </summary>
    public sealed class ClientRuntime : MonoBehaviour
    {
        /// <summary>The client's body starts this far above the server's spawn and lands: PhysX's ground and the raster's differ by centimetres, and a body that starts inside the terrain falls through it.</summary>
        private const double SpawnDropM = 0.5;
        private const float DefaultYawDeg = 60f;

        private GameClient _client;
        private IClientTransport _transport;
        private Region _region;
        private string _recordDir;
        private Heightfield _heightfield;
        private WorldClock _clock;
        private SolarClock _solar;
        private PlayerController _player;
        private HudController _hud;
        private Camera _camera;
        private float _nextPingAt;
        private double _lastRealtime;
        private float _fpsSmoothed;

        public GameClient Client => _client;
        public PlayerController Player => _player;
        public bool ViewBuilt { get; private set; }

        /// <summary>Attaches the client to a transport and starts the handshake. Called by the bootstrap.</summary>
        public void Attach(IClientTransport transport, string address, int port, string playerName, string password, Region region, string recordDir)
        {
            _transport = transport;
            _region = region;
            _recordDir = string.IsNullOrEmpty(recordDir) ? null : recordDir;
            _client = new GameClient(transport);
            _client.Welcomed += OnWelcomed;
            _client.Dropped += OnDropped;
            _client.Connect(address, port, playerName, password);
            _lastRealtime = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            if (_client == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            double dt = now - _lastRealtime;
            _lastRealtime = now;
            long nowMs = (long)(now * 1000.0);
            _client.Update(nowMs);
            if (_client.State == ClientState.Connected && Time.realtimeSinceStartup >= _nextPingAt)
            {
                _client.Ping(nowMs);
                _nextPingAt = Time.realtimeSinceStartup + 1.0f;
            }
            if (_clock != null && dt > 0.0) _clock.Advance(dt);
            if (ViewBuilt) UpdateHud(dt);
            if (ViewBuilt && !Application.isBatchMode && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.f12Key.wasPressedThisFrame)
            {
                // What the screen shows, saved for a look by someone who was not in the room.
                string dir = System.IO.Path.Combine(RegionDataLocator.SavesDir(), "screenshots");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png");
                ScreenCapture.CaptureScreenshot(file);
                Debug.Log("[client] screenshot " + file);
                _hud?.SetVerb("screenshot saved");
            }
        }

        private void OnWelcomed(WelcomeMessage welcome)
        {
            Debug.Log("[client] welcomed: session " + welcome.SessionId + ", region " + welcome.RegionId + ", seed " + welcome.Seed
                      + ", tick " + welcome.Tick + " at " + welcome.TickRate + " Hz, spawn " + F(welcome.SpawnEast) + " " + F(welcome.SpawnUp) + " " + F(welcome.SpawnNorth));
            _clock = new WorldClock(welcome.TotalHours);
            _solar = SolarClock.ForRegion(_region, _clock);
            BuildView(welcome);
        }

        private void OnDropped(string reason)
        {
            Debug.Log("[client] " + (_client.State == ClientState.Refused ? "refused: " : "dropped: ") + reason);
        }

        private void BuildView(WelcomeMessage welcome)
        {
            _heightfield = RegionDataLocator.TryLoadHeightfield(_region, out string message);
            Debug.Log("[client] " + message);

            Material terrainMaterial = Resources.Load<Material>("EarthGame/TerrainLit");
            TerrainLayer groundLayer = Resources.Load<TerrainLayer>("EarthGame/GroundLayer");
            Material skyMaterial = Resources.Load<Material>("EarthGame/Sky");
            Material seaMaterial = Resources.Load<Material>("EarthGame/Sea");
            if (terrainMaterial == null || groundLayer == null || skyMaterial == null || seaMaterial == null)
                Debug.LogError("[client] runtime assets missing under Resources/EarthGame; run EarthGame > Apply project checklist");

            if (_heightfield != null)
            {
                double half = TerrainTileBuilder.TileSizeM * 0.5;
                double nearEast = welcome.SpawnEast - half;
                double nearNorth = welcome.SpawnNorth - half;
                Terrain tile = TerrainTileBuilder.Build(_heightfield, nearEast, nearNorth, terrainMaterial, groundLayer, "Terrain tile (wake)");

                // Under and around the near tile, the whole region at coarse posts (collidable, so a walk past
                // the kilometre does not fall through; M1.4's streaming replaces this), with a hole where the
                // near tile sits; beyond the region, the 64 km surround as the far skirt; and the sea at zero.
                Terrain regionCoarse = TerrainTileBuilder.Build(_heightfield, -_region.HalfExtentM, -_region.HalfExtentM, (float)_region.ExtentM,
                    TerrainTileBuilder.Posts, terrainMaterial, groundLayer, "Terrain (region coarse)", true, 1.5f);
                TerrainTileBuilder.CutHole(regionCoarse, -_region.HalfExtentM, -_region.HalfExtentM, nearEast, nearNorth, TerrainTileBuilder.TileSizeM);
                Heightfield surround = RegionDataLocator.TryLoadRaster(_region, "surround", out string surroundMessage);
                Debug.Log("[client] " + surroundMessage);
                if (surround != null)
                {
                    Terrain skirt = TerrainTileBuilder.Build(surround, -surround.HalfExtentM, -surround.HalfExtentM, (float)surround.Raster.ExtentM,
                        TerrainTileBuilder.Posts, terrainMaterial, groundLayer, "Terrain (surround skirt)", false, 15f);
                    TerrainTileBuilder.CutHole(skirt, -surround.HalfExtentM, -surround.HalfExtentM, -_region.HalfExtentM, -_region.HalfExtentM, _region.ExtentM);
                }
                GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
                sea.name = "Sea";
                sea.layer = Layers.Water;
                Destroy(sea.GetComponent<Collider>());
                // A single quad tens of kilometres across loses depth precision where it matters, under the
                // feet: at 64 km the sea drew over ground thirteen metres above it (2026-09-08). Kept to a few
                // kilometres around the founder; the far water beyond it is the skirt's own sea-floor colour for now.
                const float seaSpan = 4000f;
                sea.transform.position = new Vector3((float)welcome.SpawnEast, 0f, (float)welcome.SpawnNorth);
                sea.transform.localScale = new Vector3(seaSpan / 10f, 1f, seaSpan / 10f);
                Renderer seaRenderer = sea.GetComponent<Renderer>();
                if (seaMaterial != null) seaRenderer.sharedMaterial = seaMaterial;
                seaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Debug.Log("[client] terrain tile at " + tile.transform.position + ", " + tile.terrainData.size
                          + "; material " + (tile.materialTemplate != null ? tile.materialTemplate.shader.name + " supported=" + tile.materialTemplate.shader.isSupported : "none")
                          + "; layers " + tile.terrainData.terrainLayers.Length
                          + (tile.terrainData.terrainLayers.Length > 0 && tile.terrainData.terrainLayers[0].diffuseTexture != null
                              ? " texture " + tile.terrainData.terrainLayers[0].diffuseTexture.width + "px tile " + tile.terrainData.terrainLayers[0].tileSize.x + " m" : " no texture")
                          + "; alphamap " + tile.terrainData.alphamapResolution + " weight0 " + tile.terrainData.GetAlphamaps(0, 0, 1, 1)[0, 0, 0]
                          + "; base pass shader " + (Shader.Find("Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)") != null)
                          + "; basemap gen shader " + (Shader.Find("Hidden/Universal Render Pipeline/Terrain/Lit (Basemap Gen)") != null));
            }

            // -eg-probe: a white sphere and a cube three metres north-east of the spawn, lit by the ordinary Lit
            // shader, so a frame shows where the sun is and whether it reaches anything at all.
            if (LaunchArgs.Has("probe"))
            {
                Material probeMaterial = new Material(seaMaterial != null ? seaMaterial.shader : Shader.Find("Universal Render Pipeline/Lit"));
                probeMaterial.SetColor("_BaseColor", Color.white);
                probeMaterial.SetFloat("_Smoothness", 0.2f);
                // South-south-west of the spawn: in the scenario's turn frame the sun is behind the camera, so a
                // lit sphere shows its bright side and drops a shadow to the south-west; an unlit one is the proof.
                Vector3 at = new Vector3((float)welcome.SpawnEast - 1.0f, (float)welcome.SpawnUp + 1.2f, (float)welcome.SpawnNorth - 2.8f);
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Probe sphere";
                sphere.transform.position = at;
                sphere.GetComponent<Renderer>().sharedMaterial = probeMaterial;
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Probe cube";
                cube.transform.position = at + new Vector3(1.5f, -0.5f, 0.5f);
                cube.GetComponent<Renderer>().sharedMaterial = probeMaterial;
                Debug.Log("[client] -eg-probe: sphere and cube at " + at);
            }

            // -eg-hide a,b,c: objects by name switched off after the view is built, to bisect what is drawn.
            string hide = LaunchArgs.Get("hide", null);
            if (!string.IsNullOrEmpty(hide))
            {
                foreach (string name in hide.Split(','))
                {
                    GameObject victim = GameObject.Find(name.Trim());
                    Debug.Log("[client] -eg-hide " + name.Trim() + ": " + (victim != null ? "hidden" : "not found"));
                    if (victim != null) victim.SetActive(false);
                }
            }
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 at = new Vector3((float)welcome.SpawnEast, 0f, (float)welcome.SpawnNorth);
                Debug.Log("[client] terrain '" + t.name + "' at " + t.transform.position + " size " + t.terrainData.size
                          + " ground under spawn " + (t.SampleHeight(at) + t.transform.position.y).ToString("0.00")
                          + " holes " + t.terrainData.holesResolution + " collider " + (t.GetComponent<TerrainCollider>() != null));
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                GameObject cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                _camera = cam.AddComponent<Camera>();
            }
            _camera.fieldOfView = 65f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 40000f;
            if (Application.isBatchMode) AudioListener.volume = 0f;
            if (LaunchArgs.Has("plain")) RenderPlainly();

            // The sun is always this component's own light, so no scene setting can quietly change what the
            // almanac drives; whatever lights the scene template carries are switched off.
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                l.enabled = false;
            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            SunAndSky sky = gameObject.AddComponent<SunAndSky>();
            sky.Attach(_solar, sun, skyMaterial);

            _hud = gameObject.AddComponent<HudController>();
            _hud.Build();

            GameObject body = new GameObject("Player");
            body.layer = Layers.Player;
            _player = body.AddComponent<PlayerController>();
            ScriptedInputSource script = _recordDir != null ? new ScriptedInputSource() : null;
            IPlayerInputSource input = script != null ? script : new InputSystemSource();
            Double3 spawn = new Double3(welcome.SpawnEast, welcome.SpawnUp + SpawnDropM, welcome.SpawnNorth);
            _player.Attach(_client, new PhysxCollision(_heightfield), MoverConfig.Default, _region, _camera, input, spawn, DefaultYawDeg, 0f);

            if (script == null && !Application.isBatchMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ViewBuilt = true;

            if (script != null)
            {
                JsonObject header = new JsonObject()
                    .With("region", _region.Id).With("seed", welcome.Seed).With("session", welcome.SessionId)
                    .With("spawn_east", welcome.SpawnEast).With("spawn_up", welcome.SpawnUp).With("spawn_north", welcome.SpawnNorth)
                    .With("world_total_hours", welcome.TotalHours)
                    .With("unity", Application.unityVersion).With("product_version", Application.version)
                    .With("started_utc", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                    .With("terrain", _heightfield != null);
                Recorder recorder = gameObject.AddComponent<Recorder>();
                recorder.Begin(_recordDir, _camera, _player, script, _hud, () => _client.LastServerTick, header);
            }
        }

        private void UpdateHud(double dt)
        {
            if (_hud == null || _player == null) return;
            if (dt > 0.0)
            {
                float fps = (float)(1.0 / dt);
                _fpsSmoothed = _fpsSmoothed <= 0f ? fps : Mathf.Lerp(_fpsSmoothed, fps, 0.05f);
            }
            _hud.SetClock(_solar.ClockText + "   day " + (_solar.DaysElapsed + 1));
            MoverState s = _player.State;
            _hud.SetDiagnostic("E " + F(s.East) + "  N " + F(s.North) + "  up " + F(s.Up) + "   " + s.HorizontalSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " m/s"
                               + (s.Grounded ? "  ground" : "  air") + (s.Wading ? "  wading" : "")
                               + "   rtt " + _client.LastRttMs + " ms   corrections " + _player.Corrections + "   " + _fpsSmoothed.ToString("0") + " fps");
        }

        /// <summary>
        /// -eg-plain: the checklist's rendering features switched off for this run only (no temporal upscaler,
        /// render scale one, no GPU Resident Drawer, so no GPU occlusion), to bisect a screen that shows nothing
        /// where the headless recording shows a world. Diagnostic; never the shipped path.
        /// </summary>
        private static void RenderPlainly()
        {
            UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp =
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp == null)
            {
                Debug.LogWarning("[client] -eg-plain: no URP asset is active");
                return;
            }
            urp.upscalingFilter = UnityEngine.Rendering.Universal.UpscalingFilterSelection.Linear;
            urp.renderScale = 1f;
            urp.gpuResidentDrawerMode = UnityEngine.Rendering.GPUResidentDrawerMode.Disabled;
            Debug.Log("[client] -eg-plain: upscaler linear, render scale 1, GPU Resident Drawer off");
        }

        private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private void OnDestroy()
        {
            _client?.Disconnect("client destroyed");
            _transport?.Dispose();
        }
    }
}
