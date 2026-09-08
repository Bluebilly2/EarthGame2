using System;
using System.Collections.Generic;
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
    /// real-time clock, and on Welcome builds everything that draws the world: the sun on the mirrored clock,
    /// the founder's body and camera, the HUD, the coarse region and the far skirt from the bake on disk, and a
    /// collidable terrain for every tile the server streams (M1.B). The other players are drawn from their
    /// mirrors. This assembly never references EarthGame.Server; the asmdef enforces it.
    ///
    /// <para>Interactive (N1) is: the client core says so (tiles answered, snapshot applied) and the tile under
    /// the founder's feet has been built, so there is collidable ground to stand on. A rejoin (N3) is a fresh
    /// transport and a fresh <see cref="GameClient"/> over the same view: the terrains and the cache stay.</para>
    /// </summary>
    public sealed class ClientRuntime : MonoBehaviour
    {
        /// <summary>The client's body starts this far above the server's spawn and lands: PhysX's ground and the raster's differ by centimetres, and a body that starts inside the terrain falls through it.</summary>
        private const double SpawnDropM = 0.5;
        private const float DefaultYawDeg = 60f;
        /// <summary>Posts per streamed tile's Terrain (2^n + 1, as Unity requires): 1.95 m over a kilometre, resampled from the tile's 4 m posts.</summary>
        private const int TilePosts = 513;
        private const float TileRequestIntervalSeconds = 2f;

        private Func<IClientTransport> _transportFactory;
        private string _address;
        private int _port;
        private string _playerName;
        private string _password;
        private Region _region;
        private string _recordDir;
        private string _scenario;

        private GameClient _client;
        private IClientTransport _transport;
        private ITileCache _tileCache;
        private TileHeightfield _ground;
        private readonly Dictionary<TileId, Terrain> _tileTerrains = new Dictionary<TileId, Terrain>();
        private readonly Dictionary<TileId, uint> _tileCrcs = new Dictionary<TileId, uint>();
        private readonly Queue<ReceivedTile> _tilesToBuild = new Queue<ReceivedTile>();
        private readonly Dictionary<uint, Transform> _mirrorBodies = new Dictionary<uint, Transform>();
        private readonly List<uint> _goneMirrors = new List<uint>();
        private Heightfield _bakedRegion;
        private Terrain _coarse;
        private Material _terrainMaterial;
        private TerrainLayer _groundLayer;
        private Material _mirrorMaterial;
        private WorldClock _clock;
        private SolarClock _solar;
        private PlayerController _player;
        private HudController _hud;
        private Camera _camera;
        private float _nextPingAt;
        private float _nextTileRequestAt;
        private double _lastRealtime;
        private double _connectedAt;
        private float _fpsSmoothed;

        public GameClient Client => _client;
        public PlayerController Player => _player;
        public TileHeightfield Ground => _ground;
        public bool ViewBuilt { get; private set; }
        /// <summary>True from the moment N1's three conditions held for the current connection.</summary>
        public bool Interactive { get; private set; }
        /// <summary>How many times this runtime has connected; two or more means a rejoin happened.</summary>
        public int Joins { get; private set; }
        /// <summary>Unity real time at the current connection's Connect.</summary>
        public double ConnectedAtRealtime => _connectedAt;
        public int TilesBuilt => _tileTerrains.Count;
        public int TilesFromCache { get; private set; }

        /// <summary>The Welcome of a connection; the flag says whether it was a rejoin.</summary>
        public event Action<WelcomeMessage, bool> Welcomed;
        /// <summary>Interactive reached for a connection; the flag says whether it was a rejoin.</summary>
        public event Action<bool> BecameInteractive;
        public event Action<string> Dropped;

        /// <summary>Attaches the client to a transport and starts the handshake. Called by the bootstrap.</summary>
        public void Attach(Func<IClientTransport> transportFactory, string address, int port, string playerName, string password,
                           Region region, string recordDir, string scenario)
        {
            _transportFactory = transportFactory;
            _address = address;
            _port = port;
            _playerName = playerName;
            _password = password;
            _region = region;
            _recordDir = string.IsNullOrEmpty(recordDir) ? null : recordDir;
            _scenario = scenario;
            _tileCache = new DiskTileCache(RegionDataLocator.TileCacheDir());
            Connect();
        }

        private void Connect()
        {
            _transport = _transportFactory();
            _client = new GameClient(_transport, _tileCache);
            _client.Welcomed += OnWelcomed;
            _client.Dropped += OnDropped;
            _client.TileReady += OnTileReady;
            _client.TileFailed += OnTileFailed;
            _client.PlayerLeft += OnPlayerLeft;
            Interactive = false;
            if (_player != null) _player.Frozen = true;
            Joins++;
            _client.Connect(_address, _port, _playerName, _password);
            _connectedAt = Time.realtimeSinceStartupAsDouble;
            _lastRealtime = _connectedAt;
            Debug.Log("[client] connecting to " + _address + ":" + _port + " as " + _playerName + " (connection " + Joins + ")");
        }

        /// <summary>
        /// The N3 cut: the socket dropped without a word to the server, the way a cable does. The client object
        /// is left where it is; <see cref="Reconnect"/> replaces it.
        /// </summary>
        public void Sever()
        {
            if (_transport is UdpTransportBase udp) udp.Sever();
            else _transport.Dispose();
            Debug.Log("[client] severed (connection " + Joins + ")");
        }

        /// <summary>A fresh transport and client over the same view; the tiles held on disk answer the join.</summary>
        public void Reconnect()
        {
            if (_client != null)
            {
                _client.Welcomed -= OnWelcomed;
                _client.Dropped -= OnDropped;
                _client.TileReady -= OnTileReady;
                _client.TileFailed -= OnTileFailed;
                _client.PlayerLeft -= OnPlayerLeft;
            }
            _transport?.Dispose();
            Connect();
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
            if (ViewBuilt)
            {
                if (_client.State == ClientState.Connected && Time.realtimeSinceStartup >= _nextTileRequestAt)
                {
                    _nextTileRequestAt = Time.realtimeSinceStartup + TileRequestIntervalSeconds;
                    _client.RequestTilesAround(_player.State.East, _player.State.North);
                }
                // One tile's Terrain per frame: nine in one frame stalled the client for most of a second on
                // 2026-09-08, long enough for the first mirror sample to find one state and an estimate fourteen
                // ticks past it. The tile under the founder is taken first, so interactive is not made to wait.
                if (_tilesToBuild.Count > 0) BuildTile(TakeTileToBuild());
                CheckInteractive();
                DrawMirrors(nowMs);
                UpdateHud(dt);
            }
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
            bool rejoin = ViewBuilt;
            Debug.Log("[client] welcomed: session " + welcome.SessionId + ", region " + welcome.RegionId + " (" + welcome.ExtentM + " m), seed " + welcome.Seed
                      + ", tick " + welcome.Tick + " at " + welcome.TickRate + " Hz, spawn " + F(welcome.SpawnEast) + " " + F(welcome.SpawnUp) + " " + F(welcome.SpawnNorth)
                      + (rejoin ? ", rejoin" : ""));
            if (_ground == null && _client.Grid != null) _ground = new TileHeightfield(_client.Grid);
            if (!rejoin)
            {
                _clock = new WorldClock(welcome.TotalHours);
                _solar = SolarClock.ForRegion(_region, _clock);
                BuildView(welcome);
            }
            else
            {
                // The clock and the sky keep running on the client's own copy: replacing them here would leave the
                // sun on a clock nothing advances. The body starts again where the server remembered it.
                _player.Rebind(_client, new Double3(welcome.SpawnEast, welcome.SpawnUp + SpawnDropM, welcome.SpawnNorth));
            }
            Welcomed?.Invoke(welcome, rejoin);
        }

        private void OnDropped(string reason)
        {
            Debug.Log("[client] " + (_client.State == ClientState.Refused ? "refused: " : "dropped: ") + reason);
            Dropped?.Invoke(reason);
        }

        private void OnTileFailed(TileId id, string why)
        {
            Debug.Log("[client] tile " + id + " not held: " + why);
        }

        private void OnPlayerLeft(uint sessionId)
        {
            Debug.Log("[client] player left: session " + sessionId);
        }

        /// <summary>A tile arrived (from the wire or the cache): the ground knows it now; its Terrain is built on a later frame.</summary>
        private void OnTileReady(ReceivedTile tile)
        {
            if (_ground == null) _ground = new TileHeightfield(_client.Grid);
            _ground.Add(tile);
            if (tile.FromCache) TilesFromCache++;
            uint held;
            if (_tileTerrains.ContainsKey(tile.Id) && _tileCrcs.TryGetValue(tile.Id, out held) && held == tile.Crc32)
            {
                CheckInteractive();
                return;
            }
            _tilesToBuild.Enqueue(tile);
        }

        /// <summary>The next tile to build: the one under the founder if it is queued, else the oldest.</summary>
        private ReceivedTile TakeTileToBuild()
        {
            if (_player != null && _ground != null && _tilesToBuild.Count > 1)
            {
                TileId under = _ground.Grid.ForPosition(_player.State.East, _player.State.North);
                if (!_tileTerrains.ContainsKey(under))
                {
                    ReceivedTile[] queued = _tilesToBuild.ToArray();
                    for (int i = 0; i < queued.Length; i++)
                    {
                        if (!queued[i].Id.Equals(under)) continue;
                        _tilesToBuild.Clear();
                        for (int j = 0; j < queued.Length; j++)
                            if (j != i) _tilesToBuild.Enqueue(queued[j]);
                        return queued[i];
                    }
                }
            }
            return _tilesToBuild.Dequeue();
        }

        private void BuildTile(ReceivedTile tile)
        {
            Terrain previous;
            if (_tileTerrains.TryGetValue(tile.Id, out previous) && previous != null) Destroy(previous.gameObject);
            float sizeM = (float)((tile.Posts - 1) * tile.CellM);
            Terrain terrain = TerrainTileBuilder.Build(_ground, tile.OriginEast, tile.OriginNorth, sizeM, TilePosts,
                _terrainMaterial, _groundLayer, "Terrain tile " + tile.Id, true, 0f);
            _tileTerrains[tile.Id] = terrain;
            _tileCrcs[tile.Id] = tile.Crc32;
            if (_coarse != null)
                TerrainTileBuilder.CutHole(_coarse, -_region.HalfExtentM, -_region.HalfExtentM, tile.OriginEast, tile.OriginNorth, sizeM);
            Debug.Log("[client] tile " + tile.Id + (tile.FromCache ? " from cache" : " from the wire") + ": " + tile.Posts + " posts at " + tile.CellM + " m, origin "
                      + F(tile.OriginEast) + " " + F(tile.OriginNorth) + "; " + _tileTerrains.Count + " built");
            CheckInteractive();
        }

        private void CheckInteractive()
        {
            if (Interactive || _client == null || _player == null || !_client.IsInteractive) return;
            if (_ground == null || !_ground.HasGroundAt(_player.State.East, _player.State.North)) return;
            TileId under = _ground.Grid.ForPosition(_player.State.East, _player.State.North);
            if (!_tileTerrains.ContainsKey(under)) return;
            Interactive = true;
            _player.Frozen = false;
            double since = Time.realtimeSinceStartupAsDouble - _connectedAt;
            Debug.Log("[client] interactive " + since.ToString("0.00", CultureInfo.InvariantCulture) + " s after connect: " + _client.Tiles.Held.Count
                      + " tile(s) held, " + TilesFromCache + " from cache, " + _client.Tiles.RefusedCount + " refused, " + _client.BytesReceived + " bytes received");
            BecameInteractive?.Invoke(Joins > 1);
        }

        /// <summary>The other players as capsules at their mirrors, sampled the delay behind the estimated server tick.</summary>
        private void DrawMirrors(long nowMs)
        {
            foreach (KeyValuePair<uint, RemoteMirror> pair in _client.Mirrors)
            {
                MirrorSample sample;
                if (!_client.TrySampleMirror(pair.Key, nowMs, out sample)) continue;
                Transform body;
                if (!_mirrorBodies.TryGetValue(pair.Key, out body) || body == null)
                {
                    GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.name = "Player " + pair.Key;
                    capsule.layer = Layers.Player;
                    Collider collider = capsule.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);
                    capsule.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                    if (_mirrorMaterial != null) capsule.GetComponent<Renderer>().sharedMaterial = _mirrorMaterial;
                    body = capsule.transform;
                    _mirrorBodies[pair.Key] = body;
                }
                body.position = new Vector3((float)sample.East, (float)sample.Up + 0.9f, (float)sample.North);
                body.rotation = Quaternion.Euler(0f, sample.YawDeg, 0f);
            }
            _goneMirrors.Clear();
            foreach (KeyValuePair<uint, Transform> pair in _mirrorBodies)
                if (!_client.Mirrors.ContainsKey(pair.Key)) _goneMirrors.Add(pair.Key);
            foreach (uint id in _goneMirrors)
            {
                if (_mirrorBodies[id] != null) Destroy(_mirrorBodies[id].gameObject);
                _mirrorBodies.Remove(id);
            }
        }

        private void BuildView(WelcomeMessage welcome)
        {
            // The bake on disk is the coarse region under the tiles and the far skirt beyond it, and nothing
            // nearer: the ground the founder stands on comes from the server (M1.B). A joiner without the bake
            // has the tiles alone, and says so.
            _bakedRegion = RegionDataLocator.TryLoadHeightfield(_region, out string message);
            Debug.Log("[client] " + message);

            _terrainMaterial = Resources.Load<Material>("EarthGame/TerrainLit");
            _groundLayer = Resources.Load<TerrainLayer>("EarthGame/GroundLayer");
            Material skyMaterial = Resources.Load<Material>("EarthGame/Sky");
            Material seaMaterial = Resources.Load<Material>("EarthGame/Sea");
            if (_terrainMaterial == null || _groundLayer == null || skyMaterial == null || seaMaterial == null)
                Debug.LogError("[client] runtime assets missing under Resources/EarthGame; run EarthGame > Apply project checklist");
            _mirrorMaterial = new Material(seaMaterial != null ? seaMaterial.shader : Shader.Find("Universal Render Pipeline/Lit"));
            _mirrorMaterial.SetColor("_BaseColor", new Color(0.75f, 0.55f, 0.4f));
            _mirrorMaterial.SetFloat("_Smoothness", 0.3f);

            if (_bakedRegion != null)
            {
                // The whole region at coarse posts (collidable, so a walk past the streamed tiles does not fall
                // through; sunk under them), holes cut where tiles arrive; beyond it the 64 km surround as the
                // far skirt.
                _coarse = TerrainTileBuilder.Build(_bakedRegion, -_region.HalfExtentM, -_region.HalfExtentM, (float)_region.ExtentM,
                    TerrainTileBuilder.Posts, _terrainMaterial, _groundLayer, "Terrain (region coarse)", true, 1.5f);
                Heightfield surround = RegionDataLocator.TryLoadRaster(_region, "surround", out string surroundMessage);
                Debug.Log("[client] " + surroundMessage);
                if (surround != null)
                {
                    Terrain skirt = TerrainTileBuilder.Build(surround, -surround.HalfExtentM, -surround.HalfExtentM, (float)surround.Raster.ExtentM,
                        TerrainTileBuilder.Posts, _terrainMaterial, _groundLayer, "Terrain (surround skirt)", false, 15f);
                    TerrainTileBuilder.CutHole(skirt, -surround.HalfExtentM, -surround.HalfExtentM, -_region.HalfExtentM, -_region.HalfExtentM, _region.ExtentM);
                }
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
            _player.Attach(_client, new PhysxCollision(_ground), MoverConfig.Default, _region, _camera, input, spawn, DefaultYawDeg, 0f);
            _player.Ground = _ground;
            _player.Frozen = true;

            if (script == null && !Application.isBatchMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            ViewBuilt = true;

            // The first-frame scenario is the recorder's own; every other scenario is the runner's, which was
            // attached before the connection began so its clock starts at Connect.
            if (script != null && (_scenario == null || _scenario == Recorder.Scenario))
            {
                JsonObject header = new JsonObject()
                    .With("region", _region.Id).With("seed", welcome.Seed).With("session", welcome.SessionId)
                    .With("spawn_east", welcome.SpawnEast).With("spawn_up", welcome.SpawnUp).With("spawn_north", welcome.SpawnNorth)
                    .With("world_total_hours", welcome.TotalHours)
                    .With("unity", Application.unityVersion).With("product_version", Application.version)
                    .With("started_utc", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                    .With("terrain", _bakedRegion != null);
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
                               + "   rtt " + _client.LastRttMs + " ms   corrections " + _player.Corrections
                               + "   tiles " + _tileTerrains.Count + (Interactive ? "" : " (loading)") + "   others " + _client.Mirrors.Count
                               + "   " + _fpsSmoothed.ToString("0") + " fps");
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
