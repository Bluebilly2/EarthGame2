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
            if (terrainMaterial == null || groundLayer == null || skyMaterial == null)
                Debug.LogError("[client] runtime assets missing under Resources/EarthGame; run EarthGame > Apply project checklist");

            if (_heightfield != null)
            {
                double half = TerrainTileBuilder.TileSizeM * 0.5;
                Terrain tile = TerrainTileBuilder.Build(_heightfield, welcome.SpawnEast - half, welcome.SpawnNorth - half, terrainMaterial, groundLayer, "Terrain tile (wake)");
                Debug.Log("[client] terrain tile at " + tile.transform.position + ", " + tile.terrainData.size
                          + "; material " + (tile.materialTemplate != null ? tile.materialTemplate.shader.name + " supported=" + tile.materialTemplate.shader.isSupported : "none")
                          + "; layers " + tile.terrainData.terrainLayers.Length
                          + (tile.terrainData.terrainLayers.Length > 0 && tile.terrainData.terrainLayers[0].diffuseTexture != null
                              ? " texture " + tile.terrainData.terrainLayers[0].diffuseTexture.width + "px tile " + tile.terrainData.terrainLayers[0].tileSize.x + " m" : " no texture")
                          + "; alphamap " + tile.terrainData.alphamapResolution + " weight0 " + tile.terrainData.GetAlphamaps(0, 0, 1, 1)[0, 0, 0]
                          + "; base pass shader " + (Shader.Find("Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)") != null)
                          + "; basemap gen shader " + (Shader.Find("Hidden/Universal Render Pipeline/Terrain/Lit (Basemap Gen)") != null));
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
            _camera.farClipPlane = 6000f;
            if (Application.isBatchMode) AudioListener.volume = 0f;

            Light sun = null;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                GameObject sunObject = new GameObject("Sun");
                sun = sunObject.AddComponent<Light>();
            }
            sun.name = "Sun";
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

        private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private void OnDestroy()
        {
            _client?.Disconnect("client destroyed");
            _transport?.Dispose();
        }
    }
}
