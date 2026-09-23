using System;
using System.Collections.Generic;
using System.Diagnostics;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// What grows underfoot, drawn (M1.6c): the tufts <see cref="Understorey"/> places from the cover tiles the client
    /// already holds, instanced in the stand's own material, one list for each shape and variant. Nothing is drawn from
    /// a tile the client does not hold, and nothing new travels for it.
    ///
    /// <para>The tufts are placed again only when the founder has walked <see cref="RelayM"/>, and they are placed out to
    /// <see cref="DrawM"/> and that much further, so a walk never reaches the edge of what was found. The placing is a
    /// few hundred cells and a few thousand tufts — well inside a frame's streaming budget — and the shader draws only
    /// what stands within <see cref="DrawM"/> of the eye, so the corners of the disc cost nothing.</para>
    /// </summary>
    public sealed class UnderstoreyViews
    {
        /// <summary>How far off the understorey is drawn, m.</summary>
        public const float DrawM = 45f;

        /// <summary>How far the founder walks before what grows round them is worked out again, m.</summary>
        public const float RelayM = 6f;

        /// <summary>How far a tuft's shade strays from its shape's colour, by a hash of where it stands.</summary>
        public const float VaryShade = 0.12f;

        private const int Chunk = 1023;
        private static readonly int EyeId = Shader.PropertyToID("_Eye");

        private readonly Material _material;
        private readonly Mesh[] _meshes;
        private readonly List<Matrix4x4>[] _drawn;
        private readonly List<UnderstoreyTuft> _found = new List<UnderstoreyTuft>();
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly Dictionary<int, Bounds> _bounds = new Dictionary<int, Bounds>();
        private double _atEast, _atNorth;
        private int _tiles = -1;
        private bool _placed;
        private int _changes;

        /// <summary>Whether the understorey is drawn at all; <c>-eg-hide understorey</c> turns it off, so what it costs can be measured.</summary>
        public bool Drawn { get; set; } = true;

        /// <summary>How many tufts were placed round the founder when they were last worked out.</summary>
        public int Tufts { get; private set; }

        /// <summary>What the last placing cost the main thread, milliseconds.</summary>
        public double LastPlaceMs { get; private set; }

        /// <summary>What the last draw cost the main thread, milliseconds.</summary>
        public double LastDrawMs { get; private set; }

        public UnderstoreyViews(Material template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            _material = new Material(template) { name = "Understorey", enableInstancing = true };
            _material.SetFloat("_Band", 0f);
            _material.SetFloat("_SplitM", DrawM);
            // Each tuft its own shade (M1.6e promise 4); the trees' materials leave this at zero.
            _material.SetFloat("_Vary", VaryShade);
            int groups = Understorey.Shapes * StandPreparation.Variants;
            _meshes = new Mesh[groups];
            _drawn = new List<Matrix4x4>[groups];
            for (int s = 0; s < Understorey.Shapes; s++)
                for (int v = 0; v < StandPreparation.Variants; v++)
                {
                    int g = s * StandPreparation.Variants + v;
                    _meshes[g] = StandMeshes.Tuft((TuftShape)s, v);
                    _drawn[g] = new List<Matrix4x4>();
                }
        }

        /// <summary>
        /// Works out what grows round the founder again when they have walked far enough from where it was last worked
        /// out, or when a cover tile has arrived or gone since. Without the second, a founder who stood still while the
        /// first tiles landed stood on bare ground until they had walked six metres: the four vantages' first frames
        /// were bare that way (2026-09-12).
        /// </summary>
        public void Follow(double east, double north, TileReceiver tiles, TileGrid grid) => Follow(east, north, tiles, grid, null);

        /// <summary>The one ground the tufts grow on (BF.4); null grows them on the tiles' raster alone.</summary>
        public ClientGround Ground;

        /// <summary>The same, less what the world's changes say is taken or cleared (BF.3); a change asks for the placing again (<see cref="MarkChanged"/>).</summary>
        public void Follow(double east, double north, TileReceiver tiles, TileGrid grid, WorldChanges changes)
        {
            if (!Drawn || tiles == null || grid == null) return;
            int held = tiles.CountOf(TileLayer.GroundCover);
            double de = east - _atEast, dn = north - _atNorth;
            if (_placed && held == _tiles && _changes == 0 && de * de + dn * dn < RelayM * RelayM) return;
            _tiles = held;
            _changes = 0;
            _clock.Restart();
            _found.Clear();
            Understorey.Find(east, north, DrawM + RelayM, tiles, grid, _found, Ground);
            if (changes != null && changes.Count > 0)
                _found.RemoveAll(t => changes.IsTuftTaken(t.Row, t.Col, t.Index) || (changes.GroundOf(t.Row, t.Col).Flags & GroundChange.Cleared) != 0);
            foreach (List<Matrix4x4> list in _drawn) list.Clear();
            foreach (UnderstoreyTuft tuft in _found)
            {
                int g = (int)tuft.Shape * StandPreparation.Variants + tuft.Variant;
                if (g < 0 || g >= _drawn.Length) continue;
                _drawn[g].Add(Matrix4x4.TRS(new Vector3(tuft.East, tuft.Up, tuft.North), Quaternion.Euler(0f, tuft.YawDeg, 0f),
                                            new Vector3(tuft.AcrossM, tuft.HeightM, tuft.AcrossM)));
            }
            Tufts = _found.Count;
            _atEast = east;
            _atNorth = north;
            _placed = true;
            LastPlaceMs = _clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>Draws what grows, from the eye this view hands the shader, as the stand's own bands are drawn.</summary>
        public void Draw(Camera camera)
        {
            if (!Drawn || camera == null || !_placed) return;
            _clock.Restart();
            Vector3 eye = camera.transform.position;
            _material.SetVector(EyeId, new Vector4(eye.x, eye.y, eye.z, 0f));
            Bounds bounds = new Bounds(eye, new Vector3(2f * (DrawM + RelayM), 500f, 2f * (DrawM + RelayM)));
            RenderParams parameters = new RenderParams(_material)
            {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
            };
            for (int g = 0; g < _drawn.Length; g++)
            {
                List<Matrix4x4> list = _drawn[g];
                if (list.Count == 0 || _meshes[g] == null) continue;
                for (int start = 0; start < list.Count; start += Chunk)
                    Graphics.RenderMeshInstanced(parameters, _meshes[g], 0, list, Mathf.Min(Chunk, list.Count - start), start);
            }
            LastDrawMs = _clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>A tuft or a cell changed (BF.3): what grows round the founder is placed again on the next follow.</summary>
        public void MarkChanged() => _changes++;

        /// <summary>
        /// The nearest drawn tuft the ray meets before a distance, by its mesh's bounds where it is drawn, as the litter is picked
        /// (BF.3); the distance becomes where it was met. Only the tufts within a reach of the ray's origin are tried.
        /// </summary>
        public bool Pick(Ray ray, float reachM, ref float nearestM, out UnderstoreyTuft best)
        {
            best = default;
            bool found = false;
            if (!_placed) return false;
            float reach2 = (reachM + 2f) * (reachM + 2f);
            foreach (UnderstoreyTuft tuft in _found)
            {
                if (tuft.Shape == TuftShape.Herb) continue;
                float dx = tuft.East - ray.origin.x, dz = tuft.North - ray.origin.z;
                if (dx * dx + dz * dz > reach2) continue;
                int g = (int)tuft.Shape * StandPreparation.Variants + tuft.Variant;
                if (g < 0 || g >= _meshes.Length || _meshes[g] == null) continue;
                if (!_bounds.TryGetValue(g, out Bounds bounds)) _bounds[g] = bounds = _meshes[g].bounds;
                Matrix4x4 toLocal = Matrix4x4.TRS(new Vector3(tuft.East, tuft.Up, tuft.North), Quaternion.Euler(0f, tuft.YawDeg, 0f),
                                                  new Vector3(tuft.AcrossM, tuft.HeightM, tuft.AcrossM)).inverse;
                if (!EntityViews.Meets(ray, bounds, toLocal, out float metres) || metres > nearestM) continue;
                nearestM = metres;
                best = tuft;
                found = true;
            }
            return found;
        }

        /// <summary>How many of each shape stand round the founder as they were last placed, for the census a run records (M1.6c promise 5).</summary>
        public void Census(int[] into)
        {
            if (into == null) return;
            for (int i = 0; i < into.Length; i++) into[i] = 0;
            foreach (UnderstoreyTuft tuft in _found)
                if ((int)tuft.Shape < into.Length) into[(int)tuft.Shape]++;
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(_material);
        }
    }
}
