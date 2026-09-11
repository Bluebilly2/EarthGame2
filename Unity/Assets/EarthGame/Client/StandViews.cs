using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace EarthGame.Client
{
    /// <summary>
    /// What stands and lies on the ground, drawn (M1.6a). A held tile's trees, sticks and cobbles are placed on a worker
    /// (<see cref="StandPreparation"/>, then the matrices they are drawn by, made from whole arithmetic rather than
    /// Unity's native calls) and swapped in whole, so the main thread's share of a tile is one dictionary write; every
    /// frame they are drawn instanced from lists that do not change while the founder walks.
    ///
    /// <para>Two bands, one shader (<c>EarthGame/StandLit</c>). The trees of the 64 m blocks near the camera are drawn
    /// near, as the grown tree, and the trees of the blocks farther off are drawn far, a trunk under one clump; the
    /// shader draws each instance in exactly one of the two, by its distance against <see cref="SplitM"/>, so no tree
    /// moves from one list to the other as the founder walks. The distance is from the eye this view hands the shader
    /// each frame, not from whatever camera a pass renders for, so the shadow caster's pass puts every instance in the
    /// band the lit pass does. Sticks and cobbles are drawn near only. No per-draw property block: colour is in the
    /// meshes (ARCHITECTURE §8's rule).</para>
    ///
    /// <para>What a frame draws is chosen block by block (2026-09-11: drawing every tree of the nine tiles far every
    /// frame cost five to eight times the trees' share of the frame). A far block is drawn only when it lies in the
    /// view, because far trees cast no shadow; a near block out of the view is drawn into the shadows alone, because a
    /// tree behind the founder still shades what is in front. The view is taken wider than the camera's, because it is
    /// taken before the camera has settled for the frame and a frame may be rendered at another shape than the
    /// screen's.</para>
    ///
    /// <para>Farther off, a block draws one far tree in two, each crown spread to cover what two did, and beyond that one
    /// in four, so the canopy keeps its cover while the count falls with distance; and a near tree casts only when its
    /// shadow can reach the ground the shadows are drawn on, which the sun's height decides. Both after the far band
    /// alone took 9.5 ms of a frame beside Windermere (2026-09-11).</para>
    ///
    /// <para>The bands' materials copy the project's stand material rather than being made from the shader, because a
    /// build keeps a shader's instanced variants only when a material asset asks for them (ProjectSetup,
    /// 2026-09-11).</para>
    /// </summary>
    public sealed class StandViews
    {
        /// <summary>Where a tree stops being drawn grown and starts being drawn far, m from the camera.</summary>
        public const float SplitM = 250f;

        /// <summary>How far off sticks and cobbles are drawn, m.</summary>
        public const float LooseDrawM = 60f;

        /// <summary>
        /// A cobble's size, m: the item's own diameter (<see cref="DefinitionCatalogue.Cobble"/>), so a cobble lying in the
        /// litter and one put down are one size (M1.5a; until then the litter's were drawn at 0.12 m and the item's at 0.1).
        /// </summary>
        public static readonly float CobbleSizeM = (float)(2.0 * DefinitionCatalogue.Cobble.RadiusM);

        /// <summary>How far a trunk's foot is set into the ground, m, so a slope drawn by the terrain's own triangles shows no gap under it.</summary>
        public const float SinkM = 0.15f;

        /// <summary>Beyond this, m, a block's far trees are drawn one in two, each crown spread to cover what two did.</summary>
        public const float FarHalfM = 500f;

        /// <summary>Beyond this, m, one in four, each crown spread to cover what four did.</summary>
        public const float FarQuarterM = 1000f;

        /// <summary>The lowest the sun is taken to stand when a shadow's length is worked out, degrees.</summary>
        private const float LowestSunDeg = 10f;

        /// <summary>How much taller than the camera's the view a block is tested against is, degrees.</summary>
        private const float ViewMarginDeg = 12f;

        /// <summary>The widest a view is taken to be, width over height: wider than any screen the game is played on.</summary>
        private const float ViewAspect = 2.4f;

        private const float BlockM = 64f;
        private const int Chunk = 1023;
        private static readonly float BlockReach = BlockM * 0.7072f;
        private static readonly int EyeId = Shader.PropertyToID("_Eye");

        private readonly TileGrid _grid;
        private readonly int _tall;
        private readonly int _blocksPerSide;
        private readonly Mesh[] _near;
        private readonly float[] _widths;
        private readonly Mesh[] _far;
        private readonly Mesh[] _sticks;
        private readonly Mesh[] _cobbles;
        private readonly Material _nearMaterial;
        private readonly Material _farMaterial;
        private readonly Material _looseMaterial;
        private readonly Dictionary<TileId, TileStand> _held = new Dictionary<TileId, TileStand>();
        private readonly Dictionary<TileId, Task<TileStand>> _building = new Dictionary<TileId, Task<TileStand>>();
        private readonly Dictionary<TileId, (ReceivedTile Stand, ReceivedTile Loose, ReceivedTile Ground, LooseTaken Taken, int TakenVersion)> _wanted =
            new Dictionary<TileId, (ReceivedTile, ReceivedTile, ReceivedTile, LooseTaken, int)>();
        private readonly List<Matrix4x4>[] _nearGather;
        private readonly List<Matrix4x4>[] _shadowGather;
        private readonly List<Matrix4x4>[] _plainGather;
        private readonly List<Matrix4x4>[] _farGather;
        private readonly float _tallestM;
        private readonly List<Matrix4x4>[] _stickGather;
        private readonly List<Matrix4x4>[] _cobbleGather;
        private readonly Plane[] _view = new Plane[6];
        private readonly Stopwatch _clock = new Stopwatch();

        /// <summary>Trees in the tiles held, for the HUD's line.</summary>
        public int TreeCount { get; private set; }

        /// <summary>What the last <see cref="Draw"/> cost the main thread, ms: choosing the blocks and handing them over.</summary>
        public double LastDrawMs { get; private set; }

        /// <summary>Whether the near band is drawn; <c>-eg-hide near</c> (or <c>trees</c>) turns it off, so what it costs can be measured.</summary>
        public bool DrawNear { get; set; } = true;

        /// <summary>Whether the far band is drawn; <c>-eg-hide far</c> (or <c>trees</c>) turns it off.</summary>
        public bool DrawFar { get; set; } = true;

        /// <summary>Whether the sticks and cobbles are drawn; <c>-eg-hide loose</c> turns them off.</summary>
        public bool DrawLoose { get; set; } = true;

        /// <summary>Whether the near trees cast shadows; <c>-eg-hide shadows</c> turns them off, so what the shadows cost can be measured apart from the trees.</summary>
        public bool DrawShadows { get; set; } = true;

        /// <summary>The material the sticks and cobbles are drawn in, near only; an item's view and the thing in hand are drawn in it too (M1.5a).</summary>
        public Material LooseMaterial => _looseMaterial;

        private sealed class Block
        {
            public Vector3 Centre;
            /// <summary>The block's trees from the lowest foot to the highest crown, which the view is tested against.</summary>
            public Bounds Bounds;
            public List<Matrix4x4>[] Near;
            public List<Matrix4x4>[] Far;
            public List<Matrix4x4>[] FarHalf;
            public List<Matrix4x4>[] FarQuarter;
            public int Trees;
            public List<Matrix4x4>[] Sticks;
            public List<Matrix4x4>[] Cobbles;
            private bool _any;
            private Vector3 _min, _max;

            public void Grow(Vector3 min, Vector3 max)
            {
                _min = _any ? Vector3.Min(_min, min) : min;
                _max = _any ? Vector3.Max(_max, max) : max;
                _any = true;
            }

            public void Settle()
            {
                if (_any) Bounds = new Bounds((_min + _max) * 0.5f, _max - _min);
            }
        }

        private sealed class TileStand
        {
            public uint StandCrc;
            public uint LooseCrc;
            public uint GroundCrc;
            /// <summary>How many times something had been taken from the tile when it was placed (M1.5b).</summary>
            public int TakenVersion;
            public Block[] Blocks;
            public int Trees;
            /// <summary>All the tile's trees, foot to crown, when it has any: a whole tile out of the view and out of reach is passed over.</summary>
            public Bounds Bounds;
            public bool HasTrees;
            /// <summary>The tile's own ground, edge to edge: how near anything of it can be, its sticks and cobbles as well as its trees.</summary>
            public Bounds Ground;
        }

        public StandViews(Material template, TileGrid grid)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _tall = StandCodes.Tall.Count;
            _blocksPerSide = (int)Math.Ceiling(grid.TileSizeM / BlockM);
            int groups = _tall * StandPreparation.Variants;
            _near = new Mesh[groups];
            _widths = new float[groups];
            for (int t = 0; t < _tall; t++)
                for (int v = 0; v < StandPreparation.Variants; v++)
                {
                    int g = t * StandPreparation.Variants + v;
                    _near[g] = StandMeshes.Tree(t, v, out float width);
                    _widths[g] = Mathf.Max(0.05f, width);
                }
            _far = new Mesh[_tall];
            for (int t = 0; t < _tall; t++) _far[t] = StandMeshes.FarTree(t);
            _sticks = new Mesh[StandPreparation.Variants];
            _cobbles = new Mesh[StandPreparation.Variants];
            for (int v = 0; v < StandPreparation.Variants; v++)
            {
                _sticks[v] = StandMeshes.Stick(v);
                _cobbles[v] = StandMeshes.Cobble(v);
            }
            _nearMaterial = Band(template, "Stand near", 0f, SplitM);
            _farMaterial = Band(template, "Stand far", 1f, SplitM);
            _looseMaterial = Band(template, "Loose near", 0f, LooseDrawM);
            _nearGather = Lists(groups);
            _shadowGather = Lists(groups);
            _plainGather = Lists(groups);
            _farGather = Lists(_tall);
            foreach (PlantSpecies species in StandCodes.Tall) _tallestM = Mathf.Max(_tallestM, (float)species.MaxHeightM);
            _stickGather = Lists(StandPreparation.Variants);
            _cobbleGather = Lists(StandPreparation.Variants);
        }

        /// <summary>
        /// Asks for a tile's things to be placed once its stand and its ground are held. The loose layer may come later,
        /// and a tile is placed again when anything it was placed from changes, what was taken from it among them (M1.5b:
        /// <paramref name="taken"/> is the tile's takings, a copy the worker alone reads, and <paramref name="takenVersion"/>
        /// counts them); a request while one is running is kept and answered when that one is taken.
        /// </summary>
        public void Want(ReceivedTile stand, ReceivedTile loose, ReceivedTile ground, LooseTaken taken = null, int takenVersion = 0)
        {
            if (stand == null || stand.Codes == null || ground == null || ground.Heights == null) return;
            TileId id = stand.Id;
            _wanted[id] = (stand, loose, ground, taken, takenVersion);
            if (!_building.ContainsKey(id) && !IsBuiltFrom(id, stand, loose, ground, takenVersion)) Start(id);
        }

        /// <summary>Swaps in one finished tile; false when none has finished. A dictionary write, so it keeps inside the streaming budget.</summary>
        public bool TakeOne()
        {
            TileId done = default;
            bool found = false;
            foreach (KeyValuePair<TileId, Task<TileStand>> pair in _building)
            {
                if (!pair.Value.IsCompleted) continue;
                done = pair.Key;
                found = true;
                break;
            }
            if (!found) return false;
            Task<TileStand> task = _building[done];
            _building.Remove(done);
            if (task.IsFaulted)
            {
                Debug.LogWarning("[client] what stands on tile " + done + " could not be placed: " + task.Exception?.GetBaseException().Message);
                return true;
            }
            if (!_wanted.TryGetValue(done, out var want)) return true;
            _held[done] = task.Result;
            if (!IsBuiltFrom(done, want.Stand, want.Loose, want.Ground, want.TakenVersion)) Start(done);
            return true;
        }

        /// <summary>Whether a tile's things are placed and drawn, and not being placed again.</summary>
        public bool Holds(TileId id) => _held.ContainsKey(id) && !_building.ContainsKey(id);

        /// <summary>A tile the client let go of: its things leave with it.</summary>
        public void Drop(TileId id)
        {
            _held.Remove(id);
            _wanted.Remove(id);
        }

        /// <summary>
        /// Draws the near blocks' trees grown (into the shadows alone when out of the view, and casting only where their
        /// shadows can land), the far blocks in the view as far trees, thinned with distance, and the near blocks' sticks
        /// and cobbles. <paramref name="sun"/> is the light the shadows are cast by; without one every near tree casts.
        /// </summary>
        public void Draw(Camera camera, Light sun)
        {
            _clock.Restart();
            Vector3 eye = camera.transform.position;
            // A shadow is a tree's height over the tangent of the sun's elevation, and it is drawn only within the
            // pipeline's shadow distance: a near tree farther off than that and its longest shadow casts nothing seen.
            float shadowM = UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.shadowDistance : SplitM;
            float elevation = sun != null ? Mathf.Asin(Mathf.Clamp(-sun.transform.forward.y, -1f, 1f)) : 0f;
            float castM = shadowM + _tallestM / Mathf.Tan(Mathf.Max(elevation, LowestSunDeg * Mathf.Deg2Rad));
            Vector4 at = new Vector4(eye.x, eye.y, eye.z, 0f);
            _nearMaterial.SetVector(EyeId, at);
            _farMaterial.SetVector(EyeId, at);
            _looseMaterial.SetVector(EyeId, at);
            Matrix4x4 wide = Matrix4x4.Perspective(Mathf.Min(170f, camera.fieldOfView + ViewMarginDeg), Mathf.Max(camera.aspect, ViewAspect),
                                                   camera.nearClipPlane, camera.farClipPlane);
            GeometryUtility.CalculateFrustumPlanes(wide * camera.worldToCameraMatrix, _view);
            Clear(_nearGather);
            Clear(_shadowGather);
            Clear(_plainGather);
            Clear(_farGather);
            Clear(_stickGather);
            Clear(_cobbleGather);
            int trees = 0;
            foreach (TileStand tile in _held.Values)
            {
                trees += tile.Trees;
                // A tile whose trees are out of the view and whose ground lies beyond the near band's reach has nothing to
                // draw: its far trees cannot be seen, none of its trees is near enough to shade what can, and nothing of
                // it lies near enough to show. The reach is to the ground, not the trees, or a beach under the founder's
                // feet would lose its cobbles to a forest far across the tile.
                if (tile.HasTrees && Reach(eye, tile.Ground) > SplitM + BlockReach && !GeometryUtility.TestPlanesAABB(_view, tile.Bounds)) continue;
                foreach (Block block in tile.Blocks)
                {
                    if (block == null) continue;
                    float dx = block.Centre.x - eye.x, dz = block.Centre.z - eye.z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (block.Near != null && (DrawNear || DrawFar))
                    {
                        bool inView = GeometryUtility.TestPlanesAABB(_view, block.Bounds);
                        if (DrawNear && d < SplitM + BlockReach)
                        {
                            bool casts = DrawShadows && d - BlockReach < castM;
                            if (inView) Gather(block.Near, casts ? _nearGather : _plainGather);
                            else if (casts) Gather(block.Near, _shadowGather);
                        }
                        if (DrawFar && inView && d + BlockReach >= SplitM)
                            Gather(d < FarHalfM ? block.Far : d < FarQuarterM ? block.FarHalf : block.FarQuarter, _farGather);
                    }
                    if (DrawLoose && d < LooseDrawM + BlockReach)
                    {
                        Gather(block.Sticks, _stickGather);
                        Gather(block.Cobbles, _cobbleGather);
                    }
                }
            }
            TreeCount = trees;
            Bounds near = new Bounds(eye, new Vector3(2f * (SplitM + BlockM), 2000f, 2f * (SplitM + BlockM)));
            for (int g = 0; g < _near.Length; g++)
            {
                Submit(_nearMaterial, _near[g], _nearGather[g], near, ShadowCastingMode.On);
                Submit(_nearMaterial, _near[g], _plainGather[g], near, ShadowCastingMode.Off);
                Submit(_nearMaterial, _near[g], _shadowGather[g], near, ShadowCastingMode.ShadowsOnly);
            }
            Bounds far = new Bounds(eye, new Vector3(2f * (float)_grid.ExtentM, 4000f, 2f * (float)_grid.ExtentM));
            for (int t = 0; t < _tall; t++) Submit(_farMaterial, _far[t], _farGather[t], far, ShadowCastingMode.Off);
            Bounds loose = new Bounds(eye, new Vector3(2f * (LooseDrawM + BlockM), 2000f, 2f * (LooseDrawM + BlockM)));
            for (int v = 0; v < StandPreparation.Variants; v++)
            {
                Submit(_looseMaterial, _sticks[v], _stickGather[v], loose, ShadowCastingMode.Off);
                Submit(_looseMaterial, _cobbles[v], _cobbleGather[v], loose, ShadowCastingMode.Off);
            }
            LastDrawMs = _clock.Elapsed.TotalMilliseconds;
        }

        public void Dispose()
        {
            _held.Clear();
            _wanted.Clear();
            UnityEngine.Object.Destroy(_nearMaterial);
            UnityEngine.Object.Destroy(_farMaterial);
            UnityEngine.Object.Destroy(_looseMaterial);
        }

        private bool IsBuiltFrom(TileId id, ReceivedTile stand, ReceivedTile loose, ReceivedTile ground, int takenVersion) =>
            _held.TryGetValue(id, out TileStand have) && have.StandCrc == stand.Crc32 && have.GroundCrc == ground.Crc32
            && have.LooseCrc == (loose != null ? loose.Crc32 : 0u) && have.TakenVersion == takenVersion;

        private void Start(TileId id)
        {
            var want = _wanted[id];
            _building[id] = Task.Run(() => Build(want.Stand, want.Loose, want.Ground, want.Taken, want.TakenVersion));
        }

        /// <summary>On a worker: the tile's things placed, less what was taken, and the matrices every one of them is drawn by, sorted into the tile's blocks.</summary>
        private TileStand Build(ReceivedTile stand, ReceivedTile loose, ReceivedTile ground, LooseTaken taken, int takenVersion)
        {
            PreparedStand prepared = StandPreparation.Prepare(stand, loose, ground, _grid, taken);
            _grid.Origin(stand.Id, out double originEast, out double originNorth);
            TileStand tile = new TileStand
            {
                StandCrc = prepared.StandCrc,
                LooseCrc = prepared.LooseCrc,
                GroundCrc = prepared.GroundCrc,
                TakenVersion = takenVersion,
                Blocks = new Block[_blocksPerSide * _blocksPerSide],
                Trees = prepared.Trees.Length,
                Ground = new Bounds(new Vector3((float)(originEast + 0.5 * _grid.TileSizeM), 0f, (float)(originNorth + 0.5 * _grid.TileSizeM)),
                                    new Vector3((float)_grid.TileSizeM, 1f, (float)_grid.TileSizeM)),
            };
            foreach (StandTree t in prepared.Trees)
            {
                int g = t.Tall * StandPreparation.Variants + t.Variant;
                float across = t.CrownM / _widths[g];
                Block block = BlockAt(tile, originEast, originNorth, t.East, t.North);
                if (block.Near == null)
                {
                    block.Near = new List<Matrix4x4>[_near.Length];
                    block.Far = new List<Matrix4x4>[_tall];
                    block.FarHalf = new List<Matrix4x4>[_tall];
                    block.FarQuarter = new List<Matrix4x4>[_tall];
                }
                int nth = block.Trees++;
                (block.Near[g] ?? (block.Near[g] = new List<Matrix4x4>())).Add(Trs(t.East, t.Up - SinkM, t.North, t.YawDeg, across, t.HeightM, across));
                (block.Far[t.Tall] ?? (block.Far[t.Tall] = new List<Matrix4x4>())).Add(Trs(t.East, t.Up - SinkM, t.North, t.YawDeg, t.CrownM, t.HeightM, t.CrownM));
                // One tree in two, and one in four, stand for the rest when a block is far off, their crowns spread by the
                // root of how many each stands for, so the cover of the canopy holds.
                if (nth % 2 == 0)
                    (block.FarHalf[t.Tall] ?? (block.FarHalf[t.Tall] = new List<Matrix4x4>())).Add(Trs(t.East, t.Up - SinkM, t.North, t.YawDeg, t.CrownM * 1.4142f, t.HeightM, t.CrownM * 1.4142f));
                if (nth % 4 == 0)
                    (block.FarQuarter[t.Tall] ?? (block.FarQuarter[t.Tall] = new List<Matrix4x4>())).Add(Trs(t.East, t.Up - SinkM, t.North, t.YawDeg, t.CrownM * 2f, t.HeightM, t.CrownM * 2f));
                // A crown's whole width either side of its trunk: a drawn crown is as wide as the world spaced it by,
                // but it need not be centred on the trunk.
                block.Grow(new Vector3(t.East - t.CrownM, t.Up - SinkM, t.North - t.CrownM), new Vector3(t.East + t.CrownM, t.Up + t.HeightM, t.North + t.CrownM));
            }
            foreach (Block block in tile.Blocks) block?.Settle();
            foreach (Block block in tile.Blocks)
            {
                if (block?.Near == null) continue;
                if (!tile.HasTrees) tile.Bounds = block.Bounds;
                else tile.Bounds.Encapsulate(block.Bounds);
                tile.HasTrees = true;
            }
            foreach (LooseInstance s in prepared.Sticks)
            {
                Block block = BlockAt(tile, originEast, originNorth, s.East, s.North);
                if (block.Sticks == null) block.Sticks = new List<Matrix4x4>[StandPreparation.Variants];
                (block.Sticks[s.Variant] ?? (block.Sticks[s.Variant] = new List<Matrix4x4>())).Add(Trs(s.East, s.Up, s.North, s.YawDeg, 1f, 1f, 1f));
            }
            foreach (LooseInstance c in prepared.Cobbles)
            {
                Block block = BlockAt(tile, originEast, originNorth, c.East, c.North);
                if (block.Cobbles == null) block.Cobbles = new List<Matrix4x4>[StandPreparation.Variants];
                (block.Cobbles[c.Variant] ?? (block.Cobbles[c.Variant] = new List<Matrix4x4>())).Add(Trs(c.East, c.Up, c.North, c.YawDeg, CobbleSizeM, CobbleSizeM, CobbleSizeM));
            }
            return tile;
        }

        private Block BlockAt(TileStand tile, double originEast, double originNorth, float east, float north)
        {
            int bx = Mathf.Clamp((int)Math.Floor((east - originEast) / BlockM), 0, _blocksPerSide - 1);
            int bz = Mathf.Clamp((int)Math.Floor((north - originNorth) / BlockM), 0, _blocksPerSide - 1);
            int i = bz * _blocksPerSide + bx;
            return tile.Blocks[i] ?? (tile.Blocks[i] = new Block
            {
                Centre = new Vector3((float)(originEast + (bx + 0.5) * BlockM), 0f, (float)(originNorth + (bz + 0.5) * BlockM)),
            });
        }

        /// <summary>
        /// A thing's matrix: turned by its yaw about the vertical (clockwise from north, as Unity turns), scaled, and put in
        /// place. Written out, not asked of <c>Matrix4x4.TRS</c>, because it is made on a worker.
        /// </summary>
        private static Matrix4x4 Trs(float east, float up, float north, float yawDeg, float sx, float sy, float sz)
        {
            double r = yawDeg * (Math.PI / 180.0);
            float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
            Matrix4x4 m = default;
            m.m00 = c * sx; m.m01 = 0f; m.m02 = s * sz; m.m03 = east;
            m.m10 = 0f; m.m11 = sy; m.m12 = 0f; m.m13 = up;
            m.m20 = -s * sx; m.m21 = 0f; m.m22 = c * sz; m.m23 = north;
            m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
            return m;
        }

        /// <summary>How far a box lies from the eye across the ground, m; nought when the eye stands over it.</summary>
        private static float Reach(Vector3 eye, Bounds box)
        {
            Vector3 min = box.min, max = box.max;
            float dx = Mathf.Max(0f, Mathf.Max(min.x - eye.x, eye.x - max.x));
            float dz = Mathf.Max(0f, Mathf.Max(min.z - eye.z, eye.z - max.z));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static void Gather(List<Matrix4x4>[] from, List<Matrix4x4>[] into)
        {
            if (from == null) return;
            for (int i = 0; i < from.Length; i++)
                if (from[i] != null) into[i].AddRange(from[i]);
        }

        /// <summary>
        /// Hands a list to the renderer in batches. No light probe or reflection probe is looked up for each instance:
        /// the shader lights from the sky's own harmonics, and a lookup for every one of tens of thousands of trees a
        /// frame is work that shows nothing.
        /// </summary>
        private static void Submit(Material material, Mesh mesh, List<Matrix4x4> matrices, Bounds bounds, ShadowCastingMode shadows)
        {
            if (matrices == null || matrices.Count == 0) return;
            RenderParams parameters = new RenderParams(material)
            {
                worldBounds = bounds,
                shadowCastingMode = shadows,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
            };
            for (int start = 0; start < matrices.Count; start += Chunk)
                Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices, Mathf.Min(Chunk, matrices.Count - start), start);
        }

        /// <summary>A band's material: a copy of the project's stand material, which is an asset so that a build keeps the shader's instanced variants.</summary>
        private static Material Band(Material template, string name, float band, float splitM)
        {
            Material material = new Material(template) { name = name, enableInstancing = true };
            material.SetFloat("_Band", band);
            material.SetFloat("_SplitM", splitM);
            return material;
        }

        private static List<Matrix4x4>[] Lists(int count)
        {
            List<Matrix4x4>[] lists = new List<Matrix4x4>[count];
            for (int i = 0; i < count; i++) lists[i] = new List<Matrix4x4>();
            return lists;
        }

        private static void Clear(List<Matrix4x4>[] lists)
        {
            foreach (List<Matrix4x4> list in lists) list.Clear();
        }
    }
}
