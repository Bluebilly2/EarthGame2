using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
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
        private const float TileRequestIntervalSeconds = 2f;
        /// <summary>What the main thread may spend on streaming work in one frame (ARCHITECTURE section 8).</summary>
        private const double StreamingBudgetMs = 1.5;
        /// <summary>
        /// How far the client's clock may slip from the server's, as each pong carries it, before it is set to it (M1.D): three
        /// minutes of the world's time, more than the round trip's worth and less than a developer's move of the clock.
        /// </summary>
        private const double ClockSlipHours = 0.05;

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
        /// <summary>The water's depth over the ground, from the tiles the server streams (M1.4b): what the founder wades in since M1.5d.</summary>
        private TileHeightfield _depth;
        private readonly Dictionary<TileId, Terrain> _tileTerrains = new Dictionary<TileId, Terrain>();
        private readonly Dictionary<TileId, uint> _tileCrcs = new Dictionary<TileId, uint>();
        private readonly Dictionary<TileId, GameObject> _waterTiles = new Dictionary<TileId, GameObject>();
        private readonly Dictionary<TileId, long> _waterFrom = new Dictionary<TileId, long>();
        private readonly Dictionary<TileId, TerrainLayer> _coverLayers = new Dictionary<TileId, TerrainLayer>();
        private readonly Dictionary<TileId, uint> _coverFrom = new Dictionary<TileId, uint>();
        private readonly Dictionary<TileId, Task<PreparedTile>> _preparing = new Dictionary<TileId, Task<PreparedTile>>();
        private readonly Dictionary<TileId, Task<byte[]>> _colouring = new Dictionary<TileId, Task<byte[]>>();
        private readonly List<TileId> _finished = new List<TileId>();
        private FrameBudget _budget;
        private readonly System.Diagnostics.Stopwatch _clockMs = System.Diagnostics.Stopwatch.StartNew();
        private readonly Dictionary<uint, Transform> _mirrorBodies = new Dictionary<uint, Transform>();
        private readonly List<uint> _goneMirrors = new List<uint>();
        private Heightfield _bakedRegion;
        private Terrain _coarse;
        /// <summary>The 64 km surround beyond the region, held so that leaving the world frees it (M1.4f).</summary>
        private Terrain _skirt;
        private Material _terrainMaterial;
        private TerrainLayer _groundLayer;
        private Material _mirrorMaterial;
        private Material _seaMaterial;
        private EntityViews _entityViews;
        private StandViews _stand;
        private UnderstoreyViews _understorey;
        private VerbController _verbs;
        /// <summary>The developer's panel (M1.D); null in a game not for development.</summary>
        private DevPanelController _devPanel;
        private HandView _hand;
        private Sounds _sounds;
        private TrunkBodies _trunks;
        /// <summary>How many feet have fallen on each ground (M1.5c), for a run log.</summary>
        private readonly Dictionary<FootingSound, int> _heard = new Dictionary<FootingSound, int>();
        /// <summary>How many times something has been taken from each tile's cells (M1.5b), so the stand knows to place it again.</summary>
        private readonly Dictionary<TileId, int> _takenVersions = new Dictionary<TileId, int>();
        private Light _sun;
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
        /// <summary>What grows underfoot (M1.6c), for a run to count and a scenario to hide.</summary>
        public UnderstoreyViews Understorey => _understorey;
        /// <summary>How many far trees are placed over the whole region (M1.6d), each tile's drawn while its stand is not held, for a run's record.</summary>
        public int FarTrees => _stand != null ? _stand.RingTrees : 0;
        /// <summary>True from the moment N1's three conditions held for the current connection.</summary>
        public bool Interactive { get; private set; }
        /// <summary>How many times this runtime has connected; two or more means a rejoin happened.</summary>
        public int Joins { get; private set; }
        /// <summary>Unity real time at the current connection's Connect.</summary>
        public double ConnectedAtRealtime => _connectedAt;
        public int TilesBuilt => _tileTerrains.Count;

        /// <summary>How many feet have fallen since the view was built (M1.5c), for a run log.</summary>
        public int Footfalls { get; private set; }

        /// <summary>How many strokes have been swum since the view was built (M1.5e).</summary>
        public int Strokes { get; private set; }

        /// <summary>The worst frame of streaming work on the main thread since the client started, milliseconds.</summary>
        public double WorstStreamingMs => _budget != null ? _budget.WorstFrameMs : 0.0;

        /// <summary>What one tile cost to make ready to draw (M1.4e), for a run log to record.</summary>
        public struct TileBuildReport
        {
            public TileId Id;
            /// <summary>What the worker spent sampling and colouring it, milliseconds; zero when only Unity was involved.</summary>
            public double WorkerMs;
            /// <summary>What the main thread spent on it, milliseconds.</summary>
            public double MainMs;
            /// <summary>What the whole frame's streaming work spent by the time this landed, milliseconds.</summary>
            public double FrameMs;
            /// <summary>What the frame had already spent when this began, milliseconds: what the budget decided on.</summary>
            public double BeforeMs;
            /// <summary>What was made: "ground" or "colour".</summary>
            public string What;
            /// <summary>The main thread's cost split by step, milliseconds, so a budget that is missed says where.</summary>
            public double HeightsMs, ObjectMs, TextureMs, WaterMs;
        }

        public event Action<TileBuildReport> TileBuilt;
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
            _client.TileDropped += OnTileDropped;
            _client.PlayerLeft += OnPlayerLeft;
            _client.LooseTakenChanged += OnLooseTaken;
            _client.FounderStateChanged += OnFounderState;
            _client.Died += OnDied;
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
        /// <summary>The founder's body as the server tells it (FP.1): the word under the clock, and the walk's capacity.</summary>
        private void OnFounderState(FounderStateMessage founder)
        {
            // The cold's word and the thirst's, together when both have one (FP.2).
            string cold = Warmth.WordFor(Warmth.LevelOf(founder.CoreC)), thirst = Hydration.WordFor(Hydration.LevelOf(founder.Water01));
            _hud?.SetCondition(cold.Length > 0 && thirst.Length > 0 ? cold + ", " + thirst : cold + thirst);
            _player?.SetWorkCapacity(Hydration.CapacityOf(founder.Water01));
        }

        /// <summary>The founder died (FP.2): the one sentence, on the screen for a while and in the log; the new body comes by the server's correction.</summary>
        private void OnDied(Death death)
        {
            string sentence = death.Explain();
            _hud?.SetNotice(sentence, 20f);
            Debug.Log("[founder] " + sentence);
        }

        public void Reconnect()
        {
            if (_client != null)
            {
                _client.Welcomed -= OnWelcomed;
                _client.Dropped -= OnDropped;
                _client.TileReady -= OnTileReady;
                _client.TileFailed -= OnTileFailed;
                _client.TileDropped -= OnTileDropped;
                _client.PlayerLeft -= OnPlayerLeft;
                _client.LooseTakenChanged -= OnLooseTaken;
                _client.FounderStateChanged -= OnFounderState;
                _client.Died -= OnDied;
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
            // The server's clock is the one clock (WorldClock): this client's runs between pongs at the server's rate and
            // follows it when it slips.
            if (_clock != null) _clock.Scale = _client.LastClockScale;
            if (_clock != null && dt > 0.0) _clock.Advance(dt);
            if (_clock != null && !double.IsNaN(_client.LastServerTotalHours) && Math.Abs(_client.LastServerTotalHours - _clock.TotalHours) > ClockSlipHours)
                _clock.SetTotalHours(_client.LastServerTotalHours);
            if (ViewBuilt)
            {
                if (_client.State == ClientState.Connected && Time.realtimeSinceStartup >= _nextTileRequestAt)
                {
                    _nextTileRequestAt = Time.realtimeSinceStartup + TileRequestIntervalSeconds;
                    _client.RequestTilesAround(_player.State.East, _player.State.North);
                }
                DrainPreparations();
                // The trunks round the body are given their capsules before the mover's next step walks into them (M1.6b).
                _trunks?.Follow(_player.State.East, _player.State.North, _client.Tiles, _client.Grid);
                _understorey?.Follow(_player.State.East, _player.State.North, _client.Tiles, _client.Grid);
                if (_stand != null && _camera != null) _stand.Draw(_camera, _sun);
                if (_camera != null) _understorey?.Draw(_camera);
                // Things are drawn where they were a stated delay ago, between the positions the server stated, as the
                // other bodies are (M1.5a).
                _entityViews?.Draw(_client.EstimatedServerTick(nowMs) - _client.MirrorDelayTicks);
                CheckInteractive();
                DrawMirrors(nowMs);
                ControlsFrame presses = _player.TakePresses();
                if (presses.Screenshot && !Application.isBatchMode) Screenshot();
                if (_devPanel != null && _devPanel.Open)
                {
                    // The panel has the mouse: its key or Escape gives it back, and the hands rest meanwhile (M1.D).
                    if (presses.DevPanel || presses.Menu) _devPanel.Hide();
                    else _devPanel.Tick();
                }
                else if (presses.DevPanel && _devPanel != null) _devPanel.Show();
                else _verbs?.Tick(presses, Time.realtimeSinceStartup);
                // The hand moves with the head and the stride every frame (M1.5c); off the ground it only follows.
                _hand?.Place(Time.deltaTime, _player.Frozen || !_player.State.Grounded ? 0.0 : _player.State.HorizontalSpeed, _player.PitchDeg);
                UpdateHud(dt);
            }
        }

        /// <summary>What the screen shows, saved for a look by someone who was not in the room.</summary>
        private void Screenshot()
        {
            string dir = System.IO.Path.Combine(RegionDataLocator.SavesDir(), "screenshots");
            System.IO.Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png");
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("[client] screenshot " + file);
            _verbs?.Note("screenshot saved");
        }

        private void OnWelcomed(WelcomeMessage welcome)
        {
            bool rejoin = ViewBuilt;
            Debug.Log("[client] welcomed: session " + welcome.SessionId + ", region " + welcome.RegionId + " (" + welcome.ExtentM + " m), seed " + welcome.Seed
                      + ", tick " + welcome.Tick + " at " + welcome.TickRate + " Hz, spawn " + F(welcome.SpawnEast) + " " + F(welcome.SpawnUp) + " " + F(welcome.SpawnNorth)
                      + (rejoin ? ", rejoin" : ""));
            if (_ground == null && _client.Grid != null) _ground = new TileHeightfield(_client.Grid);
            if (_depth == null && _client.Grid != null) _depth = new TileHeightfield(_client.Grid);
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
                // Each connection has its own mirror: the things are drawn from the new one and the verbs ask the new
                // client (until 2026-09-11 a rejoin went on drawing the first connection's things and none of its own).
                ViewEntities();
                _verbs?.Rebind(_client, _entityViews);
                _devPanel?.Rebind(_client);
            }
            Welcomed?.Invoke(welcome, rejoin);
        }

        private void OnDropped(string reason)
        {
            Debug.Log("[client] " + (_client.State == ClientState.Refused ? "refused: " : "dropped: ") + reason);
            Dropped?.Invoke(reason);
        }

        private void OnTileFailed(TileLayer layer, TileId id, string why)
        {
            Debug.Log("[client] tile " + id + " not held: " + why);
        }

        private void OnPlayerLeft(uint sessionId)
        {
            Debug.Log("[client] player left: session " + sessionId);
        }

        /// <summary>A tile arrived (from the wire or the cache): the ground knows it now; its Terrain is built on a later frame.</summary>
        /// <summary>
        /// The standing water of a tile, drawn from the depth the server streamed over that tile's own ground
        /// (M1.4c). The sea keeps the plane it has had since 2026-09-08, so only water above the datum is drawn
        /// here and the two never contend for one surface.
        /// </summary>
        private void BuildWater(TileId id)
        {
            if (_client?.Tiles == null) return;
            ReceivedTile ground = _client.Tiles.Holding(TileLayer.Ground, id);
            ReceivedTile depth = _client.Tiles.Holding(TileLayer.WaterDepth, id);
            if (ground == null || depth == null) return;
            // Either layer arriving asks for the pair, so the same mesh would be built twice on a join; the two
            // checksums together name what it was built from.
            long from = ((long)ground.Crc32 << 32) | depth.Crc32;
            if (_waterFrom.TryGetValue(id, out long built) && built == from && _waterTiles.ContainsKey(id)) return;
            if (_waterTiles.TryGetValue(id, out GameObject previous)) WaterTileBuilder.Free(previous);
            _waterTiles.Remove(id);
            _waterFrom[id] = from;
            System.Collections.Generic.List<WaterQuad> quads = WaterSurface.Build(ground, depth, Heightfield.SeaLevelM);
            GameObject water = WaterTileBuilder.Build(quads, _seaMaterial, "Water tile " + id);
            if (water == null) return;
            _waterTiles[id] = water;
            Debug.Log("[client] water on tile " + id + ": " + quads.Count + " rectangle(s) above the datum");
        }

        /// <summary>
        /// The colour of a tile's ground, from the cover the server streamed for it (M1.4d). The map is one
        /// texture over the tile and becomes that tile's own terrain layer; a tile whose cover has not arrived
        /// keeps the flat layer until it does, and a world that has no cover layer keeps it for good.
        /// </summary>
        /// <summary>
        /// A cover that arrived after its ground was built: the map is made on a worker like any other (M1.4e)
        /// and only the texture and the layer are made here.
        /// </summary>
        private void StartColouring(TileId id)
        {
            if (_client?.Tiles == null) return;
            ReceivedTile cover = _client.Tiles.Holding(TileLayer.GroundCover, id);
            if (cover?.Codes == null) return;
            if (_coverFrom.TryGetValue(id, out uint built) && built == cover.Crc32 && _coverLayers.ContainsKey(id)) return;
            if (_colouring.ContainsKey(id)) return;
            _coverFrom[id] = cover.Crc32;
            _colouring[id] = Task.Run(() => GroundColourMap.Build(cover));
        }

        private void BuildCover(TileId id, byte[] map, double before)
        {
            double started = _clockMs.Elapsed.TotalMilliseconds;
            if (!_tileTerrains.TryGetValue(id, out Terrain terrain) || terrain == null || terrain.terrainData == null) return;
            if (_coverLayers.TryGetValue(id, out TerrainLayer previous)) GroundLayerBuilder.Free(previous);
            _coverLayers.Remove(id);
            float sizeM = terrain.terrainData.size.x;
            TerrainLayer layer = GroundLayerBuilder.Build(map, GroundColourMap.Texels, sizeM, _groundLayer, "Ground cover " + id);
            if (layer == null) return;
            _coverLayers[id] = layer;
            terrain.terrainData.terrainLayers = new[] { layer };
            double mainMs = _clockMs.Elapsed.TotalMilliseconds - started;
            Debug.Log("[client] cover on tile " + id + ": " + GroundColourMap.Texels + " texels a side over " + F(sizeM) + " m, " + F(mainMs) + " ms drawn");
            TileBuilt?.Invoke(new TileBuildReport { Id = id, WorkerMs = 0.0, MainMs = mainMs, FrameMs = _budget.SpentMs, BeforeMs = before, What = "colour", TextureMs = mainMs });
        }

        /// <summary>A tile the client let go of: its ground leaves the collider, its Terrain and its water the scene.</summary>
        private void OnTileDropped(TileLayer layer, TileId id)
        {
            if (layer == TileLayer.Ground || layer == TileLayer.Stand) _stand?.Drop(id);
            if (layer == TileLayer.WaterDepth) _depth?.Remove(id);
            if (_waterTiles.TryGetValue(id, out GameObject drawn))
            {
                WaterTileBuilder.Free(drawn);
                _waterTiles.Remove(id);
                _waterFrom.Remove(id);
            }
            if (_coverLayers.TryGetValue(id, out TerrainLayer painted))
            {
                GroundLayerBuilder.Free(painted);
                _coverLayers.Remove(id);
                _coverFrom.Remove(id);
            }
            if (layer != TileLayer.Ground) return;
            _preparing.Remove(id);
            _colouring.Remove(id);
            _ground?.Remove(id);
            if (_tileTerrains.TryGetValue(id, out Terrain terrain) && terrain != null)
            {
                // The coarse ground shows again where the tile stood (M1.4f); until then a walk left a hole behind it. Each
                // terrain's south-west corner is its own position, so the cut and the fill are asked of the same cells.
                if (_coarse != null)
                    TerrainTileBuilder.FillHole(_coarse, _coarse.transform.position.x, _coarse.transform.position.z,
                        terrain.transform.position.x, terrain.transform.position.z, terrain.terrainData.size.x);
                TerrainTileBuilder.Free(terrain);
            }
            _tileTerrains.Remove(id);
            _tileCrcs.Remove(id);
        }

        /// <summary>
        /// Whether what stands on the tiles round the founder is placed (M1.6a): every stand tile the client holds round
        /// them is drawn, and there is at least one. A world without the layer is never settled, and the recorder waits
        /// out its timeout.
        /// </summary>
        private bool StandSettled()
        {
            if (_stand == null || _client?.Tiles == null || _ground == null || _player == null) return false;
            TileId under = _ground.Grid.ForPosition(_player.State.East, _player.State.North);
            int held = 0;
            foreach (TileId id in _ground.Grid.Around(under))
            {
                if (_client.Tiles.Holding(TileLayer.Stand, id) == null) continue;
                if (!_stand.Holds(id)) return false;
                held++;
            }
            return held > 0;
        }

        /// <summary>
        /// What stands and lies on a tile (M1.6a) is placed once its stand and its ground are held, less what has been taken
        /// from it (M1.5b); any of its layers arriving asks, and so does a taking.
        /// </summary>
        private void WantStand(TileId id)
        {
            if (_stand == null || _client?.Tiles == null) return;
            ReceivedTile loose = _client.Tiles.Holding(TileLayer.Loose, id);
            _takenVersions.TryGetValue(id, out int version);
            _stand.Want(_client.Tiles.Holding(TileLayer.Stand, id), loose, _client.Tiles.Holding(TileLayer.Ground, id),
                        StandPreparation.TakenIn(_client.Taken, loose, _client.Grid), version);
        }

        /// <summary>
        /// Something was taken from a cell of the loose layer (M1.5b): every tile held whose posts stand for the cell is placed
        /// again less it. A tile shares its edge posts with the next, so a cell on an edge is two tiles'.
        /// </summary>
        private void OnLooseTaken(LooseTaken.Cell cell)
        {
            if (_client?.Tiles == null || _client.Grid == null) return;
            foreach (TileId id in new List<TileId>(_client.Tiles.Held.Keys))
            {
                ReceivedTile loose = _client.Tiles.Holding(TileLayer.Loose, id);
                if (loose == null || !StandPreparation.Covers(loose, _client.Grid, cell.Row, cell.Col)) continue;
                _takenVersions[id] = (_takenVersions.TryGetValue(id, out int version) ? version : 0) + 1;
                WantStand(id);
            }
        }

        /// <summary>
        /// The far forest of a tile (M1.6d), once both its far layers are held, placed on the region's baked ground, which is
        /// what the ring beyond the stand tiles is drawn from.
        /// </summary>
        private void WantRing(TileId id)
        {
            if (_stand == null || _bakedRegion == null || _client?.Tiles == null) return;
            _stand.WantRing(_client.Tiles.Holding(TileLayer.FarStand, id), _client.Tiles.Holding(TileLayer.FarCount, id), _bakedRegion);
        }

        /// <summary>The far forest of every tile whose far layers arrived before there was a stand view to draw it.</summary>
        private void WantRings()
        {
            if (_client?.Grid == null) return;
            for (int iz = 0; iz < _client.Grid.TilesPerSide; iz++)
                for (int ix = 0; ix < _client.Grid.TilesPerSide; ix++) WantRing(new TileId(ix, iz));
        }

        private void OnTileReady(ReceivedTile tile)
        {
            if (TileLayers.IsFar(tile.Layer))
            {
                WantRing(tile.Id);
                return;
            }
            if (tile.Layer == TileLayer.Stand || tile.Layer == TileLayer.Loose || tile.Layer == TileLayer.Ground) WantStand(tile.Id);
            // The water a tile carries is drawn as its own mesh (M1.4c); the ground is what a Terrain is built
            // from. Either can arrive first, so both paths ask for the pair.
            if (tile.Layer != TileLayer.Ground)
            {
                if (tile.Layer == TileLayer.WaterDepth)
                {
                    // The depth moves the founder's legs as well as drawing the water (M1.5d).
                    if (_depth == null) _depth = new TileHeightfield(_client.Grid);
                    _depth.Add(tile);
                    BuildWater(tile.Id);
                }
                else if (tile.Layer == TileLayer.GroundCover && _tileTerrains.ContainsKey(tile.Id)) StartColouring(tile.Id);
                return;
            }
            if (_ground == null) _ground = new TileHeightfield(_client.Grid);
            _ground.Add(tile);
            if (tile.FromCache) TilesFromCache++;
            uint held;
            if (_tileTerrains.ContainsKey(tile.Id) && _tileCrcs.TryGetValue(tile.Id, out held) && held == tile.Crc32)
            {
                CheckInteractive();
                return;
            }
            // Sampling the posts and building the colour map are pure over this tile alone, so they go to a
            // worker and the main thread is left with what only Unity can do (M1.4e).
            if (_preparing.ContainsKey(tile.Id)) return;
            ReceivedTile cover = _client.Tiles.Holding(TileLayer.GroundCover, tile.Id);
            if (cover != null) _coverFrom[tile.Id] = cover.Crc32;
            System.Diagnostics.Stopwatch clock = _clockMs;
            _preparing[tile.Id] = Task.Run(() => TilePreparation.Prepare(tile, TerrainTileBuilder.TilePosts, cover,
                GroundColourMap.Texels, () => clock.Elapsed.TotalMilliseconds));
        }

        /// <summary>The next tile to build: the one under the founder if it is queued, else the oldest.</summary>
        /// <summary>
        /// The finished preparations, taken while this frame's streaming budget has time left (M1.4e). What is
        /// left waits for the next frame: nine tiles built in one frame stalled the client for most of a second
        /// on 2026-09-08, long enough for the first mirror sample to find one state and an estimate fourteen
        /// ticks past it. The tile under the founder is taken first, so interactive is not made to wait.
        /// </summary>
        private void DrainPreparations()
        {
            if (_budget == null) return;
            _budget.BeginFrame();
            while (_budget.TryStart(out double spent) && TakeGround(out PreparedTile prepared)) BuildTile(prepared, spent);
            while (_budget.TryStart(out double left) && TakeColour(out TileId id, out byte[] map)) BuildCover(id, map, left);
            while (_stand != null && _budget.TryStart(out double _) && _stand.TakeOne()) { }
            _budget.EndFrame();
        }

        /// <summary>The next prepared ground, the one under the founder first; false when none has finished.</summary>
        private bool TakeGround(out PreparedTile prepared)
        {
            prepared = null;
            if (_preparing.Count == 0) return false;
            TileId? under = _player != null && _ground != null
                ? _ground.Grid.ForPosition(_player.State.East, _player.State.North)
                : (TileId?)null;
            TileId chosen = default;
            bool found = false;
            foreach (KeyValuePair<TileId, Task<PreparedTile>> pair in _preparing)
            {
                if (!pair.Value.IsCompleted) continue;
                if (!found || (under.HasValue && pair.Key.Equals(under.Value)))
                {
                    chosen = pair.Key;
                    found = true;
                    if (under.HasValue && pair.Key.Equals(under.Value)) break;
                }
            }
            if (!found) return false;
            Task<PreparedTile> task = _preparing[chosen];
            _preparing.Remove(chosen);
            if (task.IsFaulted)
            {
                Debug.LogError("[client] preparing tile " + chosen + " failed: " + task.Exception?.GetBaseException().Message);
                return false;
            }
            // A tile the client let go of while it was being prepared is not built.
            if (_ground == null || !_ground.Holds(chosen)) return false;
            prepared = task.Result;
            return prepared != null;
        }

        private bool TakeColour(out TileId id, out byte[] map)
        {
            id = default;
            map = null;
            bool found = false;
            foreach (KeyValuePair<TileId, Task<byte[]>> pair in _colouring)
            {
                if (!pair.Value.IsCompleted) continue;
                id = pair.Key;
                map = pair.Value.IsFaulted ? null : pair.Value.Result;
                found = true;
                break;
            }
            if (!found) return false;
            _colouring.Remove(id);
            return map != null && _tileTerrains.ContainsKey(id);
        }

        private void BuildTile(PreparedTile prepared, double before)
        {
            double started = _clockMs.Elapsed.TotalMilliseconds;
            double at = started;
            Terrain previous;
            if (_tileTerrains.TryGetValue(prepared.Id, out previous)) TerrainTileBuilder.Free(previous);
            TerrainLayer painted = _groundLayer;
            if (prepared.ColourMap != null)
            {
                if (_coverLayers.TryGetValue(prepared.Id, out TerrainLayer stale)) GroundLayerBuilder.Free(stale);
                TerrainLayer made = GroundLayerBuilder.Build(prepared.ColourMap, prepared.ColourTexels, prepared.SizeM,
                    _groundLayer, "Ground cover " + prepared.Id);
                if (made != null)
                {
                    _coverLayers[prepared.Id] = made;
                    _coverFrom[prepared.Id] = prepared.CoverCrc;
                    painted = made;
                }
            }
            else if (_coverLayers.TryGetValue(prepared.Id, out TerrainLayer own))
            {
                painted = own;
            }
            double textureMs = Since(ref at);
            TerrainData data = TerrainTileBuilder.BuildData(prepared, painted);
            double heightsMs = Since(ref at);
            Terrain terrain = TerrainTileBuilder.Place(data, prepared, _terrainMaterial, "Terrain tile " + prepared.Id);
            double objectMs = Since(ref at);
            _tileTerrains[prepared.Id] = terrain;
            _tileCrcs[prepared.Id] = prepared.GroundCrc;
            BuildWater(prepared.Id);
            if (_coarse != null)
                TerrainTileBuilder.CutHole(_coarse, _coarse.transform.position.x, _coarse.transform.position.z, prepared.OriginEast, prepared.OriginNorth, prepared.SizeM);
            double waterMs = Since(ref at);
            // A cover that arrived before this tile was built is asked for now: on a join over the in-memory
            // transport the small cover tile lands first, and the frames of 2026-09-10 came back one flat tan
            // because nothing on either path then asked for it.
            if (prepared.ColourMap == null) StartColouring(prepared.Id);
            double mainMs = _clockMs.Elapsed.TotalMilliseconds - started;
            Debug.Log("[client] tile " + prepared.Id + ": " + prepared.Posts + " posts, " + F(prepared.WorkerMs) + " ms prepared, "
                      + F(mainMs) + " ms drawn (texture " + F(textureMs) + ", heights " + F(heightsMs) + ", object " + F(objectMs)
                      + ", water " + F(waterMs) + "); " + _tileTerrains.Count + " built");
            TileBuilt?.Invoke(new TileBuildReport
            {
                Id = prepared.Id, WorkerMs = prepared.WorkerMs, MainMs = mainMs, FrameMs = _budget.SpentMs, BeforeMs = before, What = "ground",
                TextureMs = textureMs, HeightsMs = heightsMs, ObjectMs = objectMs, WaterMs = waterMs,
            });
            CheckInteractive();
        }

        /// <summary>Milliseconds since the mark, and moves the mark to now.</summary>
        private double Since(ref double mark)
        {
            double now = _clockMs.Elapsed.TotalMilliseconds;
            double spent = now - mark;
            mark = now;
            return spent;
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
            Material seaMaterial = _seaMaterial = Resources.Load<Material>("EarthGame/Sea");
            if (_terrainMaterial == null || _groundLayer == null || skyMaterial == null || seaMaterial == null)
                Debug.LogError("[client] runtime assets missing under Resources/EarthGame; run EarthGame > Apply project checklist");
            _mirrorMaterial = new Material(seaMaterial != null ? seaMaterial.shader : Shader.Find("Universal Render Pipeline/Lit"));
            _mirrorMaterial.SetColor("_BaseColor", new Color(0.75f, 0.55f, 0.4f));
            _mirrorMaterial.SetFloat("_Smoothness", 0.3f);

            // What stands and lies on the ground (M1.6a), drawn from the stand and loose tiles the server streams. The
            // material is an asset so that the build keeps its shader's instanced variants (ProjectSetup). A player
            // with no graphics device (-nographics, as the shaped join runs) cannot draw instanced and makes no view:
            // the first build drew anyway, and every frame of that join threw (2026-09-11).
            Material standMaterial = Resources.Load<Material>("EarthGame/StandLit");
            if (!SystemInfo.supportsInstancing) Debug.Log("[client] this device draws nothing instanced; what stands on the ground is streamed and not drawn");
            else if (standMaterial == null) Debug.LogError("[client] no stand material under Resources/EarthGame/StandLit; nothing will stand on the ground");
            else if (_client.Grid != null && _stand == null)
            {
                _stand = new StandViews(standMaterial, _client.Grid);
                // What grows underfoot (M1.6c), from the cover tiles this client already holds.
                _understorey = new UnderstoreyViews(standMaterial);
                WantRings();
            }

            // The entities the server shows this client (M1.3), drawn from the stand's own meshes in the material its
            // loose sticks and cobbles are drawn in (M1.5a), so a stick put down is drawn as the ones lying in the litter.
            ViewEntities();

            if (_bakedRegion != null)
            {
                // The whole region at coarse posts (collidable, so a walk past the streamed tiles does not fall
                // through; sunk under them), holes cut where tiles arrive; beyond it the 64 km surround as the
                // far skirt.
                _coarse = TerrainTileBuilder.Build(_bakedRegion, -_region.HalfExtentM, -_region.HalfExtentM, (float)_region.ExtentM,
                    TerrainTileBuilder.CoarsePosts, _terrainMaterial, _groundLayer, "Terrain (region coarse)", true, 1.5f);
                Heightfield surround = RegionDataLocator.TryLoadRaster(_region, "surround", out string surroundMessage);
                Debug.Log("[client] " + surroundMessage);
                if (surround != null)
                {
                    _skirt = TerrainTileBuilder.Build(surround, -surround.HalfExtentM, -surround.HalfExtentM, (float)surround.Raster.ExtentM,
                        TerrainTileBuilder.CoarsePosts, _terrainMaterial, _groundLayer, "Terrain (surround skirt)", false, 15f);
                    TerrainTileBuilder.CutHole(_skirt, -surround.HalfExtentM, -surround.HalfExtentM, -_region.HalfExtentM, -_region.HalfExtentM, _region.ExtentM);
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

            // -eg-hide a,b,c: objects by name switched off after the view is built, to bisect what is drawn. "trees"
            // ("near" and "far" for one band of them, "shadows" for the near trees' shadows alone), "loose" and
            // "understorey" switch off what stands, lies and grows on the ground, which is drawn without objects
            // (M1.6a, M1.6c).
            string hide = LaunchArgs.Get("hide", null);
            if (!string.IsNullOrEmpty(hide))
            {
                foreach (string listed in hide.Split(','))
                {
                    string name = listed.Trim();
                    if (name == "ring" && _stand != null)
                    {
                        _stand.DrawRing = false;
                        Debug.Log("[client] -eg-hide ring: hidden");
                        continue;
                    }
                    if (name == "understorey" && _understorey != null)
                    {
                        _understorey.Drawn = false;
                        Debug.Log("[client] -eg-hide understorey: hidden");
                        continue;
                    }
                    if (_stand != null && (name == "trees" || name == "near" || name == "far" || name == "loose" || name == "shadows"))
                    {
                        if (name == "trees" || name == "near") _stand.DrawNear = false;
                        if (name == "trees" || name == "far") _stand.DrawFar = false;
                        if (name == "loose") _stand.DrawLoose = false;
                        if (name == "shadows") _stand.DrawShadows = false;
                        Debug.Log("[client] -eg-hide " + name + ": hidden");
                        continue;
                    }
                    GameObject victim = GameObject.Find(name);
                    Debug.Log("[client] -eg-hide " + name + ": " + (victim != null ? "hidden" : "not found"));
                    if (victim != null) victim.SetActive(false);
                }
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                GameObject cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                _camera = cam.AddComponent<Camera>();
                cam.AddComponent<AudioListener>();
            }
            _camera.fieldOfView = 65f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 40000f;
            // Every automated run is muted: a windowless one, and any a script drives (the client has had sounds since M1.5c).
            if (Application.isBatchMode || _recordDir != null || _scenario != null) AudioListener.volume = 0f;
            if (LaunchArgs.Has("plain")) RenderPlainly();

            // The sun is always this component's own light, so no scene setting can quietly change what the
            // almanac drives; whatever lights the scene template carries are switched off.
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                l.enabled = false;
            GameObject sunObject = new GameObject("Sun");
            Light sun = _sun = sunObject.AddComponent<Light>();
            SunAndSky sky = gameObject.AddComponent<SunAndSky>();
            sky.Attach(_solar, sun, skyMaterial);

            _hud = gameObject.AddComponent<HudController>();
            _hud.Build();

            GameObject body = new GameObject("Player");
            body.layer = Layers.Player;
            _player = body.AddComponent<PlayerController>();
            // A recorded scenario drives the founder through the scripted seam, except the controls scenario, which presses
            // the controls themselves (M1.5d).
            ScriptedInputSource script = _recordDir != null && _scenario != Recorder.ControlsScenario ? new ScriptedInputSource() : null;
            IPlayerInputSource input = script != null ? script : new InputSystemSource();
            Double3 spawn = new Double3(welcome.SpawnEast, welcome.SpawnUp + SpawnDropM, welcome.SpawnNorth);
            _player.Attach(_client, new PhysxCollision(_ground != null ? new StreamedWater(_ground, _depth) : null), MoverConfig.Default, _region, _camera, input, spawn, DefaultYawDeg, 0f);
            _player.Ground = _ground;
            _player.Frozen = true;
            // -eg-still: the camera without the stride's dip and sway (M1.5c), for the owner to play against the one he
            // found good in ruling 18.
            _player.Still = LaunchArgs.Has("still");
            // -eg-dev: a development game, whose founder can fly (M1.5e); the bootstrap runs its SOLO server to allow it.
            _player.FlightAllowed = LaunchArgs.Has("dev");
            _player.Stepped += OnStepped;
            // The trunks a founder can walk into (M1.6b): the client's, since the server never runs the mover.
            _trunks = new TrunkBodies(transform);
            _sounds = new Sounds(_camera.transform);

            // The verbs (M1.5a): what the crosshair is on, the verb line, the carrying window and the thing in hand.
            if (_stand != null) _hand = new HandView(_camera, _stand.LooseMaterial);
            _verbs = new VerbController(_client, _entityViews, _player, _camera, _hud, _hand, _ground, _ground != null ? new StreamedWater(_ground, _depth) : null);
            // The developer's panel (M1.D): in a development game alone, on an object of its own, since an object holds one
            // UIDocument and the HUD's is on this one.
            if (_player.FlightAllowed)
            {
                _devPanel = new GameObject("Developer panel").AddComponent<DevPanelController>();
                _devPanel.Build(_client, _player, _region, welcome.Seed, _solar);
            }

            if (script == null && !Application.isBatchMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // ARCHITECTURE section 8: streaming is capped at 1.5 ms of CPU a frame, a stopwatch and never an
            // item count. What a piece of work costs once started is measured and reported, not pretended away.
            System.Diagnostics.Stopwatch clock = _clockMs;
            _budget = new FrameBudget(StreamingBudgetMs, () => clock.Elapsed.TotalMilliseconds);

            ViewBuilt = true;

            // The scenarios that write frames (first-frame, carry, litter, wade, controls) are the recorder's own; every other
            // scenario is the runner's, which was attached before the connection began so its clock starts at Connect.
            if (_recordDir != null && (_scenario == null || Recorder.IsKnown(_scenario)))
            {
                JsonObject header = new JsonObject()
                    .With("region", _region.Id).With("seed", welcome.Seed).With("session", welcome.SessionId)
                    .With("spawn_east", welcome.SpawnEast).With("spawn_up", welcome.SpawnUp).With("spawn_north", welcome.SpawnNorth)
                    .With("world_total_hours", welcome.TotalHours)
                    .With("unity", Application.unityVersion).With("product_version", Application.version)
                    .With("started_utc", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                    .With("terrain", _bakedRegion != null);
                Recorder recorder = gameObject.AddComponent<Recorder>();
                // The sky and the sun as this client works them out (M1.8a: a function of the seed and the clock), for the
                // records the night scenario keeps beside the body's words (FP.2); the wind in the open, the ground's openness
                // being the server's to read.
                Climate climate = Climate.ForRegion(_region);
                Synoptic synoptic = new Synoptic(welcome.Seed);
                recorder.Begin(_recordDir, _camera, _player, script, _hud, () => _client.LastServerTick, header, StandSettled,
                               () => _stand != null ? _stand.TreeCount : -1, () => _stand != null ? _stand.LastDrawMs : 0.0,
                               _scenario ?? Recorder.Scenario, _client, _verbs, _devPanel,
                               () => Weather.At(climate, synoptic, _solar, Math.Max(0.0, _player.State.Up), 1.0), () => _solar.SolarElevationDeg,
                               () => _clock.TotalHours);
                TileBuilt += recorder.RecordBuild;
            }
        }

        /// <summary>The things of the connection's mirror, drawn, and heard when they come down (M1.5c).</summary>
        private void ViewEntities()
        {
            _entityViews?.Dispose();
            _entityViews = new EntityViews(_client.Entities, _stand?.LooseMaterial);
            _entityViews.Landed += OnLanded;
        }

        private void OnLanded(Definition definition, Vector3 at, double joules) => _sounds?.Landing(definition, at, joules);

        /// <summary>
        /// A foot fell (M1.5c): it sounds of what the server says is underfoot, and is counted for a run log. A stroke swum
        /// (M1.5e) sounds of the water, and is counted apart.
        /// </summary>
        private void OnStepped(Footfall footfall)
        {
            if (footfall.Stroke)
            {
                _sounds?.Stroke(footfall);
                Strokes++;
                return;
            }
            FootingSound footing = Underfoot();
            _sounds?.Step(footfall, footing);
            Footfalls++;
            _heard[footing] = (_heard.TryGetValue(footing, out int n) ? n : 0) + 1;
        }

        /// <summary>What is under the founder's feet: the cover and the water the server streamed for the tile they stand on.</summary>
        private FootingSound Underfoot()
        {
            if (_client?.Tiles == null || _client.Grid == null || _player == null) return FootingSound.Soil;
            MoverState s = _player.State;
            TileId id = _client.Grid.ForPosition(s.East, s.North);
            return Footing.At(_client.Tiles.Holding(TileLayer.GroundCover, id), _client.Tiles.Holding(TileLayer.WaterDepth, id), s.East, s.North, s.Wading || s.Swimming);
        }

        /// <summary>The grounds the feet have fallen on, as <c>name:count</c> in the order the sounds are listed, for a run log (M1.5c).</summary>
        public string HeardUnderfoot()
        {
            List<string> heard = new List<string>();
            foreach (FootingSound footing in (FootingSound[])Enum.GetValues(typeof(FootingSound)))
                if (_heard.TryGetValue(footing, out int n)) heard.Add(footing.ToString().ToLowerInvariant() + ":" + n.ToString(CultureInfo.InvariantCulture));
            return string.Join(",", heard);
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
            // The velocity, its length and its three parts, where the speed across the ground alone was shown until 2026-09-14
            // (William: "change speed in the bottom left to velocity").
            _hud.SetDiagnostic("E " + F(s.East) + "  N " + F(s.North) + "  up " + F(s.Up)
                               + "   velocity " + F(s.Velocity.Length) + " m/s (E " + F(s.VelEast) + ", up " + F(s.VelUp) + ", N " + F(s.VelNorth) + ")"
                               + (s.Grounded ? "  ground" : "  air") + (s.Wading ? "  wading" : "") + (s.Swimming ? "  swimming" : "")
                               + (_player.Flying ? "  flying" : "")
                               + "   rtt " + _client.LastRttMs + " ms   corrections " + _player.Corrections
                               + "   tiles " + _tileTerrains.Count + (Interactive ? "" : " (loading)") + "   others " + _client.Mirrors.Count
                               + "   things " + (_entityViews != null ? _entityViews.Count : 0)
                               + "   trees " + (_stand != null ? _stand.TreeCount : 0)
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
            _verbs?.Dispose();
            _hand?.Dispose();
            _trunks?.Dispose();
            _understorey?.Dispose();
            _sounds?.Dispose();
            if (_player != null) _player.Stepped -= OnStepped;
            _entityViews?.Dispose();
            _stand?.Dispose();
            _client?.Disconnect("client destroyed");
            _transport?.Dispose();
            if (_devPanel != null) UnityObjects.Free(_devPanel.gameObject);
            // What the ground was drawn from goes with the world (M1.4f): the tiles, their water and colour, the coarse region and the skirt.
            foreach (Terrain tile in _tileTerrains.Values) TerrainTileBuilder.Free(tile);
            _tileTerrains.Clear();
            foreach (GameObject water in _waterTiles.Values) WaterTileBuilder.Free(water);
            _waterTiles.Clear();
            foreach (TerrainLayer layer in _coverLayers.Values) GroundLayerBuilder.Free(layer);
            _coverLayers.Clear();
            TerrainTileBuilder.Free(_coarse);
            TerrainTileBuilder.Free(_skirt);
        }
    }
}
