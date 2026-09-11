using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// What stands and lies on the ground, drawn (M1.6a). A held tile's trees, sticks and cobbles are placed on a worker
    /// (<see cref="StandPreparation"/>, then the matrices they are drawn by, made from whole arithmetic rather than
    /// Unity's native calls) and swapped in whole, so the main thread's share of a tile is one dictionary write; every
    /// frame they are drawn instanced from lists that do not change while the founder walks.
    ///
    /// <para>Two bands, one shader (<c>EarthGame/StandLit</c>). Every tree of every held tile is drawn far — a trunk
    /// under one clump — and the trees of the blocks near the camera are drawn near, as the grown tree; the shader
    /// draws each instance in exactly one of the two, by its distance against <see cref="SplitM"/>, so no tree moves
    /// from one list to the other as the founder walks. The distance is from the eye this view hands the shader each
    /// frame, not from whatever camera a pass renders for, so the shadow caster's pass puts every instance in the band
    /// the lit pass does. Sticks and cobbles are drawn near only. No <c>MaterialPropertyBlock</c>: colour is in the
    /// meshes (ARCHITECTURE §8's rule).</para>
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

        /// <summary>A cobble's size, m: a stone to fit the hand.</summary>
        public const float CobbleSizeM = 0.12f;

        /// <summary>How far a trunk's foot is set into the ground, m, so a slope drawn by the terrain's own triangles shows no gap under it.</summary>
        public const float SinkM = 0.15f;

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
        private readonly Dictionary<TileId, (ReceivedTile Stand, ReceivedTile Loose, ReceivedTile Ground)> _wanted =
            new Dictionary<TileId, (ReceivedTile, ReceivedTile, ReceivedTile)>();
        private readonly List<Matrix4x4>[] _nearGather;
        private readonly List<Matrix4x4>[] _stickGather;
        private readonly List<Matrix4x4>[] _cobbleGather;

        /// <summary>Trees in the tiles held, for the HUD's line.</summary>
        public int TreeCount { get; private set; }

        /// <summary>Whether the trees are drawn; <c>-eg-hide trees</c> turns them off, so what they cost can be measured.</summary>
        public bool DrawTrees { get; set; } = true;

        /// <summary>Whether the sticks and cobbles are drawn; <c>-eg-hide loose</c> turns them off.</summary>
        public bool DrawLoose { get; set; } = true;

        private sealed class Block
        {
            public Vector3 Centre;
            public List<Matrix4x4>[] Near;
            public List<Matrix4x4>[] Sticks;
            public List<Matrix4x4>[] Cobbles;
        }

        private sealed class TileStand
        {
            public uint StandCrc;
            public uint LooseCrc;
            public uint GroundCrc;
            public Bounds Bounds;
            public Block[] Blocks;
            public List<Matrix4x4>[] Far;
            public int Trees;
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
            _stickGather = Lists(StandPreparation.Variants);
            _cobbleGather = Lists(StandPreparation.Variants);
        }

        /// <summary>
        /// Asks for a tile's things to be placed once its stand and its ground are held. The loose layer may come later,
        /// and a tile is placed again when anything it was placed from changes; a request while one is running is kept
        /// and answered when that one is taken.
        /// </summary>
        public void Want(ReceivedTile stand, ReceivedTile loose, ReceivedTile ground)
        {
            if (stand == null || stand.Codes == null || ground == null || ground.Heights == null) return;
            TileId id = stand.Id;
            _wanted[id] = (stand, loose, ground);
            if (!_building.ContainsKey(id) && !IsBuiltFrom(id, stand, loose, ground)) Start(id);
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
            if (!IsBuiltFrom(done, want.Stand, want.Loose, want.Ground)) Start(done);
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

        /// <summary>Draws every held tile's trees far, the near blocks' trees grown, and the near blocks' sticks and cobbles.</summary>
        public void Draw(Vector3 eye)
        {
            Vector4 at = new Vector4(eye.x, eye.y, eye.z, 0f);
            _nearMaterial.SetVector(EyeId, at);
            _farMaterial.SetVector(EyeId, at);
            _looseMaterial.SetVector(EyeId, at);
            Clear(_nearGather);
            Clear(_stickGather);
            Clear(_cobbleGather);
            int trees = 0;
            foreach (TileStand tile in _held.Values)
            {
                trees += tile.Trees;
                if (DrawTrees)
                    for (int t = 0; t < _tall; t++) Submit(_farMaterial, _far[t], tile.Far[t], tile.Bounds, ShadowCastingMode.Off);
                foreach (Block block in tile.Blocks)
                {
                    if (block == null) continue;
                    float dx = block.Centre.x - eye.x, dz = block.Centre.z - eye.z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (DrawTrees && d < SplitM + BlockReach) Gather(block.Near, _nearGather);
                    if (DrawLoose && d < LooseDrawM + BlockReach)
                    {
                        Gather(block.Sticks, _stickGather);
                        Gather(block.Cobbles, _cobbleGather);
                    }
                }
            }
            TreeCount = trees;
            Bounds near = new Bounds(eye, new Vector3(2f * (SplitM + BlockM), 2000f, 2f * (SplitM + BlockM)));
            for (int g = 0; g < _near.Length; g++) Submit(_nearMaterial, _near[g], _nearGather[g], near, ShadowCastingMode.On);
            Bounds loose = new Bounds(eye, new Vector3(2f * (LooseDrawM + BlockM), 2000f, 2f * (LooseDrawM + BlockM)));
            for (int v = 0; v < StandPreparation.Variants; v++)
            {
                Submit(_looseMaterial, _sticks[v], _stickGather[v], loose, ShadowCastingMode.Off);
                Submit(_looseMaterial, _cobbles[v], _cobbleGather[v], loose, ShadowCastingMode.Off);
            }
        }

        public void Dispose()
        {
            _held.Clear();
            _wanted.Clear();
            UnityEngine.Object.Destroy(_nearMaterial);
            UnityEngine.Object.Destroy(_farMaterial);
            UnityEngine.Object.Destroy(_looseMaterial);
        }

        private bool IsBuiltFrom(TileId id, ReceivedTile stand, ReceivedTile loose, ReceivedTile ground) =>
            _held.TryGetValue(id, out TileStand have) && have.StandCrc == stand.Crc32 && have.GroundCrc == ground.Crc32
            && have.LooseCrc == (loose != null ? loose.Crc32 : 0u);

        private void Start(TileId id)
        {
            var want = _wanted[id];
            _building[id] = Task.Run(() => Build(want.Stand, want.Loose, want.Ground));
        }

        /// <summary>On a worker: the tile's things placed, and the matrices every one of them is drawn by, sorted into the tile's blocks.</summary>
        private TileStand Build(ReceivedTile stand, ReceivedTile loose, ReceivedTile ground)
        {
            PreparedStand prepared = StandPreparation.Prepare(stand, loose, ground, _grid);
            _grid.Origin(stand.Id, out double originEast, out double originNorth);
            float size = (float)_grid.TileSizeM;
            TileStand tile = new TileStand
            {
                StandCrc = prepared.StandCrc,
                LooseCrc = prepared.LooseCrc,
                GroundCrc = prepared.GroundCrc,
                Bounds = new Bounds(new Vector3((float)originEast + 0.5f * size, 200f, (float)originNorth + 0.5f * size), new Vector3(size + 100f, 1000f, size + 100f)),
                Blocks = new Block[_blocksPerSide * _blocksPerSide],
                Far = Lists(_tall),
                Trees = prepared.Trees.Length,
            };
            foreach (StandTree t in prepared.Trees)
            {
                int g = t.Tall * StandPreparation.Variants + t.Variant;
                float across = t.CrownM / _widths[g];
                Block block = BlockAt(tile, originEast, originNorth, t.East, t.North);
                if (block.Near == null) block.Near = new List<Matrix4x4>[_near.Length];
                (block.Near[g] ?? (block.Near[g] = new List<Matrix4x4>())).Add(Trs(t.East, t.Up - SinkM, t.North, t.YawDeg, across, t.HeightM, across));
                tile.Far[t.Tall].Add(Trs(t.East, t.Up - SinkM, t.North, t.YawDeg, t.CrownM, t.HeightM, t.CrownM));
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

        private static void Gather(List<Matrix4x4>[] from, List<Matrix4x4>[] into)
        {
            if (from == null) return;
            for (int i = 0; i < from.Length; i++)
                if (from[i] != null) into[i].AddRange(from[i]);
        }

        private static void Submit(Material material, Mesh mesh, List<Matrix4x4> matrices, Bounds bounds, ShadowCastingMode shadows)
        {
            if (matrices == null || matrices.Count == 0) return;
            RenderParams parameters = new RenderParams(material) { worldBounds = bounds, shadowCastingMode = shadows, receiveShadows = true };
            for (int start = 0; start < matrices.Count; start += Chunk)
                Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices, Mathf.Min(Chunk, matrices.Count - start), start);
        }

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
