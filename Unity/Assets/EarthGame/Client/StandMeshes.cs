using System.Collections.Generic;
using EarthGame.ClientCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// The trees, sticks and cobbles, grown as geometry (M1.6a). Ported from v1's <c>EucalyptBuilder</c> and
    /// <c>MeshData</c>, with Bherwerre's tall plants in place of the Southern Highlands' and their shapes read from
    /// <see cref="StandForms"/>, the one table of what each looks like.
    ///
    /// <para>What makes a tree recognisable from the next ridge is its silhouette, not its surface: a crooked trunk that
    /// never runs straight, bare limbs forking off it, and a crown of separate clumps with sky between them. So a tree
    /// is a five-sided faceted trunk, one to three limbs, and deformed icosahedra hung along their outer ends; every face
    /// is lit by its own plane, so each catches the sun on its own, and colour is baked into the mesh, so a whole forest
    /// is a handful of meshes and one material.</para>
    ///
    /// <para>A tree's faces share their corners (<see cref="FacetMeshData"/>): each face leads with a corner that carries
    /// its normal and its colour, and the shader takes both from the leading corner alone, so a face is still one plane
    /// of one colour while a tree has about as many corners as faces rather than three times as many (2026-09-11, when a
    /// near tree's thousand-odd corners, each drawn in the depth, colour and two shadow passes, were what the near band
    /// cost).</para>
    ///
    /// <para>Everything is built at unit height — the foot of the trunk at y = 0, the top of the crown at y = 1 — and
    /// deterministic from the form and the variant alone, never from Unity's random numbers, so a tree is the same tree
    /// in every session. A near tree's crown is measured once built; the client scales it across to the crown the
    /// world spaced the trunk by (<c>PlantSpecies.CrownShare</c>).</para>
    /// </summary>
    public static class StandMeshes
    {
        private const int TrunkSides = 5;
        private const int FarTrunkSides = 3;
        private const int TrunkSegments = 5;
        private const float BendMinDeg = 3f;
        private const float BendMaxDeg = 9f;
        private const int LimbSegments = 3;
        private const float LimbRadiusShare = 0.7f;
        private const float LimbTipRadiusShare = 0.55f;
        private const float ClumpPushMin = 0.65f;
        private const float ClumpPushSpan = 0.7f;
        private const float FaceJitter = 0.05f;
        private const float ClumpJitter = 0.08f;
        private const float StoneJitter = 0.06f;
        private const float GreenNudge = 0.03f;
        private const float StockingJitter = 0.10f;
        private const float StockingBand = 0.06f;

        private static readonly Dictionary<int, Mesh> Trees = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, float> Widths = new Dictionary<int, float>();
        private static readonly Dictionary<int, Mesh> FarTrees = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> Sticks = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> Cobbles = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> Tufts = new Dictionary<int, Mesh>();

        /// <summary>
        /// A near tree of a tall plant (<c>StandCodes.Tall</c> index) in one of its variants, built on first use and kept,
        /// with how wide its crown came out as a share of its height.
        /// </summary>
        public static Mesh Tree(int tall, int variant, out float crownWidth)
        {
            int key = tall * 64 + Wrap(variant);
            if (!Trees.TryGetValue(key, out Mesh mesh))
            {
                mesh = BuildTree(tall * 7919 + Wrap(variant) * 104729 + 17, StandForms.ForTall(tall), out float width);
                Trees[key] = mesh;
                Widths[key] = width;
            }
            crownWidth = Widths[key];
            return mesh;
        }

        /// <summary>
        /// A far tree of a tall plant: a three-sided trunk under one lumpy clump, unit height and a crown of unit width, so
        /// the client scales it across by the crown and up by the height. A ridge a few hundred metres off needs the right
        /// lumpy edge against the sky, not its limbs.
        /// </summary>
        public static Mesh FarTree(int tall)
        {
            if (FarTrees.TryGetValue(tall, out Mesh mesh)) return mesh;
            TreeForm form = StandForms.ForTall(tall);
            FacetMeshData data = new FacetMeshData();
            Vector3[] centres = { Vector3.zero, new Vector3(0f, 0.62f, 0f) };
            Vector3[] dirs = { Vector3.up, Vector3.up };
            float[] radii = { 0.035f, 0.02f };
            FacetTube(data, FarTrunkSides, centres, dirs, radii, 0f, tall, ToColor(form.BarkLow), ToColor(form.BarkHigh), 0.5f * form.StockingShare);
            FacetClump(data, new Vector3(0f, 0.72f, 0f), 0.5f, new Vector3(1f, 0.56f * form.CrownSquash, 1f), ToColor(form.Foliage), FaceJitter, tall * 31 + 5);
            mesh = data.ToMesh("Far " + form.Name);
            FarTrees[tall] = mesh;
            return mesh;
        }

        /// <summary>A fallen stick, lying along x on the ground: a slender five-sided rod with a kink or two, about a metre long.</summary>
        public static Mesh Stick(int variant)
        {
            int v = Wrap(variant);
            if (Sticks.TryGetValue(v, out Mesh mesh)) return mesh;
            System.Random rand = new System.Random(v * 613 + 3);
            MeshData data = new MeshData();
            float length = Range(rand, 0.7f, 1.2f);
            float radius = Range(rand, 0.014f, 0.024f);
            int segments = 3;
            Vector3[] centres = new Vector3[segments + 1];
            Vector3[] dirs = new Vector3[segments + 1];
            float[] radii = new float[segments + 1];
            Vector3 direction = Vector3.right;
            centres[0] = new Vector3(-0.5f * length, radius, 0f);
            for (int i = 0; i < segments; i++)
            {
                dirs[i] = direction;
                centres[i + 1] = centres[i] + direction * (length / segments);
                direction = Quaternion.AngleAxis(Range(rand, -14f, 14f), Vector3.up) * direction;
            }
            dirs[segments] = dirs[segments - 1];
            for (int i = 0; i <= segments; i++) radii[i] = radius * Mathf.Lerp(1f, 0.6f, i / (float)segments);
            Color colour = ToColor(StandForms.Stick);
            AddTube(data, centres, dirs, radii, Range(rand, 0f, Mathf.PI * 2f), v, colour, colour, -1f);
            mesh = data.ToMesh("Stick " + v);
            Sticks[v] = mesh;
            return mesh;
        }

        /// <summary>
        /// A cobble: an icosahedron with every vertex pushed in or out by a quarter and its bottom third cut flat, so it
        /// sits on the ground instead of on one point. Unit diameter; the client scales it to the stone's size.
        /// </summary>
        public static Mesh Cobble(int variant)
        {
            int v = Wrap(variant);
            if (Cobbles.TryGetValue(v, out Mesh mesh)) return mesh;
            MeshData data = new MeshData();
            Vector3[] verts = new Vector3[IcoVerts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = IcoVerts[i] * (0.5f * (0.75f + 0.5f * Hash01(v * 7 + 1, i)));
                p.y = Mathf.Max(p.y, -0.15f) + 0.15f;
                verts[i] = p;
            }
            Color grey = ToColor(StandForms.Cobble);
            for (int f = 0; f < IcoFaces.Length; f += 3)
            {
                Color c = Scale(grey, Jitter(v, f / 3, StoneJitter));
                data.AddFace(verts[IcoFaces[f]], verts[IcoFaces[f + 1]], verts[IcoFaces[f + 2]], new Vector3(0f, 0.15f, 0f), c, c, c);
            }
            mesh = data.ToMesh("Cobble " + v);
            Cobbles[v] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ the understorey (M1.6c)

        /// <summary>
        /// One tuft of the understorey, of unit height and unit width, which the client scales to what the cover grows
        /// there: blades for a tussock, arching straps for a sedge clump, fronds for bracken, and a low bush of woody
        /// stems under two or three small crowns for heath. Every blade and leaf is drawn on both sides, because a tuft is
        /// walked round and there is no thickness in a blade of grass.
        /// </summary>
        public static Mesh Tuft(TuftShape shape, int variant)
        {
            int v = Wrap(variant);
            int key = (int)shape * StandPreparation.Variants + v;
            if (Tufts.TryGetValue(key, out Mesh mesh)) return mesh;
            System.Random rand = new System.Random(key * 977 + 11);
            MeshData data = new MeshData();
            Color low = ToColor(StandForms.TuftLow(shape)), high = ToColor(StandForms.TuftHigh(shape));
            switch (shape)
            {
                case TuftShape.Tussock:
                    Blades(data, rand, 7, 0.055f, 0.42f, 0.62f, low, high);
                    break;
                case TuftShape.Clump:
                    Blades(data, rand, 9, 0.075f, 0.62f, 0.80f, low, high);
                    break;
                case TuftShape.Frond:
                    Fronds(data, rand, 4, low, high);
                    break;
                case TuftShape.Herb:
                    Rosette(data, rand, low, high);
                    break;
                default:
                    Bush(data, rand, low, high);
                    break;
            }
            mesh = data.ToMesh(shape + " " + v);
            Tufts[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// A fan of blades from one root: each leans out by <paramref name="lean"/> of the width and reaches
        /// <paramref name="reach"/> of the height, the taller ones standing straighter, so a tuft has a shape rather than
        /// being a star.
        /// </summary>
        private static void Blades(MeshData data, System.Random rand, int count, float halfWidth, float lean, float reach, Color low, Color high)
        {
            for (int i = 0; i < count; i++)
            {
                float yaw = (i + Range(rand, -0.35f, 0.35f)) * Mathf.PI * 2f / count;
                float tall = reach * Range(rand, 0.6f, 1.4f);
                float out1 = lean * Range(rand, 0.35f, 1.0f);
                Vector3 along = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                Vector3 across = new Vector3(along.z, 0f, -along.x) * halfWidth * Range(rand, 0.7f, 1.3f);
                Vector3 root = along * Range(rand, 0f, 0.06f);
                Vector3 middle = root + along * (out1 * 0.45f) + Vector3.up * (tall * 0.55f);
                Vector3 tip = root + along * out1 + Vector3.up * tall;
                Color mid = Color.Lerp(low, high, 0.55f);
                Blade(data, root - across, root + across, middle + across * 0.55f, middle - across * 0.55f, low, mid);
                Blade(data, middle - across * 0.55f, middle + across * 0.55f, tip, tip, mid, high);
            }
        }

        /// <summary>Bracken: a few fronds, each a bare stalk with a blade off its top, leaning further out than grass does.</summary>
        private static void Fronds(MeshData data, System.Random rand, int count, Color low, Color high)
        {
            for (int i = 0; i < count; i++)
            {
                float yaw = (i + Range(rand, -0.3f, 0.3f)) * Mathf.PI * 2f / count;
                float tall = Range(rand, 0.55f, 1.0f);
                float out1 = Range(rand, 0.25f, 0.55f);
                Vector3 along = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                Vector3 across = new Vector3(along.z, 0f, -along.x);
                Vector3 root = along * Range(rand, 0f, 0.05f);
                Vector3 fork = root + along * (out1 * 0.4f) + Vector3.up * (tall * 0.45f);
                Vector3 tip = root + along * out1 + Vector3.up * tall;
                Blade(data, root - across * 0.02f, root + across * 0.02f, fork + across * 0.02f, fork - across * 0.02f, low, low);
                // The blade: a leaf a third of the tuft wide, hanging a little under the frond's line.
                Vector3 wide = across * Range(rand, 0.16f, 0.26f);
                Blade(data, fork - wide, fork + wide, tip + wide * 0.35f, tip - wide * 0.35f, Color.Lerp(low, high, 0.4f), high);
            }
        }

        /// <summary>A herb between the tufts (M1.6e): a rosette of five to seven flat leaves laid out from the root, ankle-high at most, the leaves lifting a little at their tips.</summary>
        private static void Rosette(MeshData data, System.Random rand, Color low, Color high)
        {
            int leaves = rand.Next(5, 8);
            for (int i = 0; i < leaves; i++)
            {
                float yaw = (i + Range(rand, -0.3f, 0.3f)) * Mathf.PI * 2f / leaves;
                float reach = Range(rand, 0.5f, 1.0f);
                Vector3 along = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                Vector3 across = new Vector3(along.z, 0f, -along.x) * Range(rand, 0.12f, 0.2f);
                Vector3 root = Vector3.up * 0.02f;
                Vector3 middle = root + along * (reach * 0.5f) + Vector3.up * 0.06f;
                Vector3 tip = root + along * reach + Vector3.up * Range(rand, 0.12f, 0.3f);
                Color mid = Color.Lerp(low, high, 0.5f);
                Blade(data, root - across * 0.3f, root + across * 0.3f, middle + across, middle - across, low, mid);
                Blade(data, middle - across, middle + across, tip + across * 0.2f, tip - across * 0.2f, mid, high);
            }
        }

        /// <summary>Heath: two or three low crowns on short woody stems, the whole of it under a founder's waist.</summary>
        private static void Bush(MeshData data, System.Random rand, Color low, Color high)
        {
            int crowns = rand.Next(2, 4);
            for (int i = 0; i < crowns; i++)
            {
                float yaw = (i + Range(rand, -0.3f, 0.3f)) * Mathf.PI * 2f / crowns;
                Vector3 along = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                float lean = Range(rand, 0.12f, 0.3f);
                float tall = Range(rand, 0.45f, 0.8f);
                Vector3 top = along * lean + Vector3.up * tall;
                Vector3 across = new Vector3(along.z, 0f, -along.x) * 0.03f;
                Blade(data, -across, across, top + across, top - across, low, low);
                Crown(data, top, Range(rand, 0.2f, 0.34f), Color.Lerp(low, high, Range(rand, 0.3f, 1f)), i * 31 + 5);
            }
        }

        /// <summary>A quad drawn on both sides, since a leaf has no thickness: a and b at the foot, c and d at the head.</summary>
        private static void Blade(MeshData data, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color foot, Color head)
        {
            Vector3 middle = (a + b + c + d) * 0.25f;
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal = normal.normalized * 0.5f;
            // Twice, wound from either side, so the blade is lit and seen whichever side the founder walks round.
            data.AddFace(a, b, c, middle - normal, foot, foot, head);
            data.AddFace(a, c, d, middle - normal, foot, head, head);
            data.AddFace(a, b, c, middle + normal, foot, foot, head);
            data.AddFace(a, c, d, middle + normal, foot, head, head);
        }

        /// <summary>A little crown of leaves: the cobble's solid, squashed and coloured as foliage.</summary>
        private static void Crown(MeshData data, Vector3 centre, float radius, Color colour, int seed)
        {
            Vector3[] verts = new Vector3[IcoVerts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = IcoVerts[i] * (radius * (0.75f + 0.5f * Hash01(seed, i)));
                p.y *= 0.72f;
                verts[i] = centre + p;
            }
            for (int f = 0; f < IcoFaces.Length; f += 3)
            {
                Color c = Scale(colour, Jitter(seed, f / 3, StoneJitter));
                data.AddFace(verts[IcoFaces[f]], verts[IcoFaces[f + 1]], verts[IcoFaces[f + 2]], centre, c, c, c);
            }
        }

        private static int Wrap(int variant) => ((variant % StandPreparation.Variants) + StandPreparation.Variants) % StandPreparation.Variants;

        // ------------------------------------------------------------------ tree

        /// <summary>
        /// Grows one tree, in the order a tree grows: a leaning trunk that wanders, limbs off it near the top, then
        /// clumps of leaves along the outer part of whatever tips there are, two at least on each. Built roughly to
        /// height and then scaled exactly to it.
        /// </summary>
        private static Mesh BuildTree(int seed, TreeForm form, out float crownWidth)
        {
            System.Random rand = new System.Random(seed);
            FacetMeshData data = new FacetMeshData();

            float trunkLength = form.TrunkLength * Range(rand, 0.94f, 1.06f);
            float leanAzimuth = Range(rand, 0f, 360f);
            float lean = form.LeanDeg * Range(rand, 0.6f, 1.4f);

            Vector3[] centres = new Vector3[TrunkSegments + 1];
            Vector3[] dirs = new Vector3[TrunkSegments];
            float[] radii = new float[TrunkSegments + 1];
            Vector3 direction = TiltToward(Vector3.up, leanAzimuth, lean);
            float segmentLength = trunkLength / TrunkSegments;
            centres[0] = Vector3.zero;
            for (int i = 0; i < TrunkSegments; i++)
            {
                dirs[i] = direction;
                centres[i + 1] = centres[i] + direction * segmentLength;
                // Each segment leaves at a slight angle to the one below, so the trunk wanders rather than wobbling
                // about a straight line.
                direction = TiltToward(direction, Range(rand, 0f, 360f), Range(rand, BendMinDeg, BendMaxDeg));
            }
            for (int i = 0; i <= TrunkSegments; i++)
                // The taper is StandForms' since M1.6b, so the trunk a founder is stopped by is the trunk that is drawn.
                radii[i] = form.TrunkRadius * Mathf.Lerp(1f, (float)StandForms.TrunkTipRadiusShare, i / (float)TrunkSegments);

            Vector3[] ringDirs = new Vector3[TrunkSegments + 1];
            ringDirs[0] = Vector3.up;
            for (int i = 1; i < TrunkSegments; i++) ringDirs[i] = (dirs[i - 1] + dirs[i]).normalized;
            ringDirs[TrunkSegments] = dirs[TrunkSegments - 1];

            // Rough bark to the stocking line and the species' smooth bark above it; the line moves per tree so a stand
            // does not show one ring across all of it.
            float stocking = form.StockingShare <= 0f
                ? -1f
                : Mathf.Clamp(trunkLength * form.StockingShare + Range(rand, -StockingJitter, StockingJitter) * 0.5f, 0.05f, trunkLength * 1.2f);
            FacetTube(data, TrunkSides, centres, ringDirs, radii, Range(rand, 0f, Mathf.PI * 2f), seed, ToColor(form.BarkLow), ToColor(form.BarkHigh), stocking);

            List<Vector3> tips = new List<Vector3> { centres[TrunkSegments] };
            List<Vector3> inner = new List<Vector3> { centres[TrunkSegments - 1] };
            int limbCount = 1 + (int)Range(rand, 0f, form.MaxLimbs - 0.001f);
            float limbAzimuth = Range(rand, 0f, 360f);
            float treeHeight = trunkLength + form.ClumpRadius;
            for (int limb = 0; limb < limbCount; limb++)
            {
                float spread = form.MaxLimbs > 1 ? limb / (float)(form.MaxLimbs - 1) : 0f;
                float forkY = treeHeight * Mathf.Clamp(form.ForkShare + spread * 0.22f + Range(rand, -0.05f, 0.05f), 0.18f, 0.88f);
                SampleTrunk(centres, dirs, radii, forkY, out Vector3 forkPoint, out Vector3 forkDir, out float forkRadius);
                // Two limbs go to roughly opposite sides, or the tree hangs off one shoulder.
                float azimuth = limbAzimuth + limb * (360f / Mathf.Max(1, limbCount)) + Range(rand, -35f, 35f);
                float splay = form.LimbSplayDeg * Range(rand, 0.8f, 1.2f);
                float limbLength = form.LimbLength * Range(rand, 0.85f, 1.15f);

                Vector3[] limbCentres = new Vector3[LimbSegments + 1];
                Vector3[] limbDirs = new Vector3[LimbSegments];
                float[] limbRadii = new float[LimbSegments + 1];
                Vector3 d = TiltToward(forkDir, azimuth, splay);
                limbCentres[0] = forkPoint;
                for (int i = 0; i < LimbSegments; i++)
                {
                    limbDirs[i] = d;
                    limbCentres[i + 1] = limbCentres[i] + d * (limbLength / LimbSegments);
                    // A limb leaves sideways and sweeps back up toward the light, a fraction of its splay so it never
                    // swings past vertical.
                    d = TiltToward(d, azimuth + 180f, splay * Range(rand, 0.08f, 0.22f));
                    d = TiltToward(d, Range(rand, 0f, 360f), Range(rand, 3f, 8f));
                }
                float limbBase = forkRadius * LimbRadiusShare;
                for (int i = 0; i <= LimbSegments; i++) limbRadii[i] = limbBase * Mathf.Lerp(1f, LimbTipRadiusShare, i / (float)LimbSegments);
                Vector3[] limbRingDirs = new Vector3[LimbSegments + 1];
                limbRingDirs[0] = limbDirs[0];
                for (int i = 1; i < LimbSegments; i++) limbRingDirs[i] = (limbDirs[i - 1] + limbDirs[i]).normalized;
                limbRingDirs[LimbSegments] = limbDirs[LimbSegments - 1];
                FacetTube(data, TrunkSides, limbCentres, limbRingDirs, limbRadii, Range(rand, 0f, Mathf.PI * 2f), seed, ToColor(form.BarkHigh), ToColor(form.BarkHigh), -1f);
                tips.Add(limbCentres[LimbSegments]);
                inner.Add(limbCentres[1]);
            }

            Color canopy = ToColor(form.Foliage);
            canopy.g = Mathf.Clamp01(canopy.g + Range(rand, -GreenNudge, GreenNudge));
            // Two clumps at least on every tip, each somewhere along the outer part of its limb rather than on its very
            // end: the first frames of the stand (2026-09-11) showed lone clumps held up against the sky on hair-thin limbs.
            int drawn = (int)(form.ClumpMin + Range(rand, 0f, form.ClumpMax - form.ClumpMin + 0.999f));
            int clumpCount = Mathf.Max(2 * tips.Count, Mathf.Min(form.ClumpMax, drawn));
            for (int i = 0; i < clumpCount; i++)
            {
                int k = i % tips.Count;
                Vector3 tip = Vector3.Lerp(inner[k], tips[k], Range(rand, 0.35f, 1f));
                float radius = form.ClumpRadius * Range(rand, 0.82f, 1.18f);
                // Never further off its tip than it is wide, so a clump always touches the limb holding it; fanned round
                // the compass so two clumps are never one blob.
                float offset = radius * Mathf.Lerp(0.55f, 0.95f, form.ClumpOffset / 0.22f * Range(rand, 0.7f, 1f));
                float azimuth = i * (360f / clumpCount) + Range(rand, -25f, 25f);
                Vector3 centre = tip + HorizontalAxis(azimuth) * offset + Vector3.up * Range(rand, -0.02f, 0.06f);
                FacetClump(data, centre, radius, new Vector3(1f, form.CrownSquash, 1f), Scale(canopy, 1f + Range(rand, -ClumpJitter, ClumpJitter)), FaceJitter, seed * 97 + i);
            }

            data.ScaleToUnitHeight();
            crownWidth = data.Width();
            return data.ToMesh(form.Name + " " + seed);
        }

        /// <summary>Walks up the trunk to a height and says where it is, which way it heads and how thick it is there.</summary>
        private static void SampleTrunk(Vector3[] centres, Vector3[] dirs, float[] radii, float targetY, out Vector3 point, out Vector3 direction, out float radius)
        {
            int last = centres.Length - 1;
            float y = Mathf.Clamp(targetY, Mathf.Lerp(centres[0].y, centres[last].y, 0.45f), Mathf.Lerp(centres[0].y, centres[last].y, 0.92f));
            for (int i = 0; i < last; i++)
            {
                float y0 = centres[i].y, y1 = centres[i + 1].y;
                if (y > y1 && i < last - 1) continue;
                float t = y1 > y0 ? Mathf.Clamp01((y - y0) / (y1 - y0)) : 0f;
                point = Vector3.Lerp(centres[i], centres[i + 1], t);
                direction = dirs[i];
                radius = Mathf.Lerp(radii[i], radii[i + 1], t);
                return;
            }
            point = centres[last];
            direction = dirs[last - 1];
            radius = radii[last];
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>
        /// The rings a tube is skinned over: five-sided (or as many as asked) rings about the centres, their phase carried
        /// up by parallel transport so the facets run unbroken from butt to tip.
        /// </summary>
        private static Vector3[][] Rings(Vector3[] centres, Vector3[] ringDirs, float[] radii, float phase, int sides)
        {
            int rings = centres.Length;
            Vector3[][] ring = new Vector3[rings][];
            Vector3 carried = Perpendicular(ringDirs[0]);
            for (int i = 0; i < rings; i++)
            {
                Vector3 axis = ringDirs[i].normalized;
                Vector3 right = carried - axis * Vector3.Dot(carried, axis);
                if (right.sqrMagnitude < 1e-8f) right = Perpendicular(axis);
                right = right.normalized;
                carried = right;
                Vector3 forward = Vector3.Cross(axis, right).normalized;
                ring[i] = new Vector3[sides];
                for (int k = 0; k < sides; k++)
                {
                    float a = phase + k * (Mathf.PI * 2f / sides);
                    ring[i][k] = centres[i] + (right * Mathf.Cos(a) + forward * Mathf.Sin(a)) * radii[i];
                }
            }
            return ring;
        }

        /// <summary>
        /// Skins ring centres with a tube of separate faces, rough bark below <paramref name="boundaryY"/> and smooth
        /// above; a negative boundary is smooth all the way. The sticks' tube.
        /// </summary>
        private static void AddTube(MeshData data, Vector3[] centres, Vector3[] ringDirs, float[] radii, float phase, int seed, Color lower, Color upper, float boundaryY)
        {
            Vector3[][] ring = Rings(centres, ringDirs, radii, phase, TrunkSides);
            for (int i = 0; i + 1 < centres.Length; i++)
            {
                Vector3 inside = (centres[i] + centres[i + 1]) * 0.5f;
                for (int k = 0; k < TrunkSides; k++)
                {
                    int k2 = (k + 1) % TrunkSides;
                    Vector3 p0 = ring[i][k], p1 = ring[i][k2], q0 = ring[i + 1][k], q1 = ring[i + 1][k2];
                    float j0 = Jitter(seed, data.FaceCount, FaceJitter);
                    data.AddFace(p0, q0, q1, inside, Bark(p0.y, lower, upper, boundaryY, j0), Bark(q0.y, lower, upper, boundaryY, j0), Bark(q1.y, lower, upper, boundaryY, j0));
                    float j1 = Jitter(seed, data.FaceCount, FaceJitter);
                    data.AddFace(p0, q1, p1, inside, Bark(p0.y, lower, upper, boundaryY, j1), Bark(q1.y, lower, upper, boundaryY, j1), Bark(p1.y, lower, upper, boundaryY, j1));
                }
            }
        }

        /// <summary>
        /// Skins ring centres with a tube whose faces share their corners, each face one colour: rough bark below
        /// <paramref name="boundaryY"/> and smooth above, by the height of the face's middle; a negative boundary is
        /// smooth all the way.
        /// </summary>
        private static void FacetTube(FacetMeshData data, int sides, Vector3[] centres, Vector3[] ringDirs, float[] radii, float phase, int seed, Color lower, Color upper, float boundaryY)
        {
            Vector3[][] ring = Rings(centres, ringDirs, radii, phase, sides);
            int[][] points = new int[ring.Length][];
            for (int i = 0; i < ring.Length; i++)
            {
                points[i] = new int[sides];
                for (int k = 0; k < sides; k++) points[i][k] = data.Point(ring[i][k]);
            }
            for (int i = 0; i + 1 < ring.Length; i++)
            {
                Vector3 inside = (centres[i] + centres[i + 1]) * 0.5f;
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    float y0 = (ring[i][k].y + ring[i + 1][k].y + ring[i + 1][k2].y) / 3f;
                    data.Face(points[i][k], points[i + 1][k], points[i + 1][k2], inside, Bark(y0, lower, upper, boundaryY, Jitter(seed, data.FaceCount, FaceJitter)));
                    float y1 = (ring[i][k].y + ring[i + 1][k2].y + ring[i][k2].y) / 3f;
                    data.Face(points[i][k], points[i + 1][k2], points[i][k2], inside, Bark(y1, lower, upper, boundaryY, Jitter(seed, data.FaceCount, FaceJitter)));
                }
            }
        }

        /// <summary>A clump of leaves: an icosahedron with each corner pushed out by 0.65 to 1.35 of the radius, then squashed; its faces share their corners.</summary>
        private static void FacetClump(FacetMeshData data, Vector3 centre, float radius, Vector3 squash, Color colour, float faceJitter, int seed)
        {
            int[] points = new int[IcoVerts.Length];
            for (int i = 0; i < IcoVerts.Length; i++)
            {
                Vector3 v = IcoVerts[i] * (radius * (ClumpPushMin + ClumpPushSpan * Hash01(seed, i)));
                points[i] = data.Point(centre + new Vector3(v.x * squash.x, v.y * squash.y, v.z * squash.z));
            }
            for (int f = 0; f < IcoFaces.Length; f += 3)
                data.Face(points[IcoFaces[f]], points[IcoFaces[f + 1]], points[IcoFaces[f + 2]], centre, Scale(colour, Jitter(seed, f / 3, faceJitter)));
        }

        private static Color Bark(float y, Color lower, Color upper, float boundaryY, float jitter)
        {
            float t = boundaryY < 0f ? 1f : Mathf.Clamp01((y - boundaryY) / StockingBand + 0.5f);
            return Scale(Color.Lerp(lower, upper, t), jitter);
        }

        private static Vector3 HorizontalAxis(float azimuthDeg)
        {
            float r = azimuthDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r));
        }

        private static Vector3 TiltToward(Vector3 v, float azimuthDeg, float angleDeg) => Quaternion.AngleAxis(angleDeg, HorizontalAxis(azimuthDeg - 90f)) * v;

        private static Vector3 Perpendicular(Vector3 axis)
        {
            Vector3 reference = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
            return Vector3.Cross(axis, reference).normalized;
        }

        /// <summary>A value in [0, 1) from two integers, for per-face numbers that must not depend on the order of drawing.</summary>
        private static float Hash01(int a, int b)
        {
            uint h = (uint)a * 2654435761u ^ (uint)b * 2246822519u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f;
        }

        private static float Jitter(int seed, int face, float amount) => 1f + (Hash01(seed, face) - 0.5f) * 2f * amount;

        private static Color Scale(Color c, float f) => new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);

        private static float Range(System.Random rand, float min, float max) => min + (float)rand.NextDouble() * (max - min);

        private static Color ToColor(Rgb rgb) => new Color(rgb.R, rgb.G, rgb.B, 1f);

        private static readonly Vector3[] IcoVerts = BuildIcoVerts();

        private static readonly int[] IcoFaces =
        {
            0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
            3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1,
        };

        private static Vector3[] BuildIcoVerts()
        {
            float p = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] v =
            {
                new Vector3(-1f, p, 0f), new Vector3(1f, p, 0f), new Vector3(-1f, -p, 0f), new Vector3(1f, -p, 0f),
                new Vector3(0f, -1f, p), new Vector3(0f, 1f, p), new Vector3(0f, -1f, -p), new Vector3(0f, 1f, -p),
                new Vector3(p, 0f, -1f), new Vector3(p, 0f, 1f), new Vector3(-p, 0f, -1f), new Vector3(-p, 0f, 1f),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        /// <summary>
        /// A triangle soup with the winding worked out: each face wound away from a point inside it (the tube's axis, the
        /// stone's centre), with its own normal and its vertices' colours. Ported from v1's <c>MeshData</c>; the sticks and
        /// cobbles, too few to be worth sharing corners.
        /// </summary>
        private sealed class MeshData
        {
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color> _colours = new List<Color>();
            private readonly List<int> _tris = new List<int>();

            public int FaceCount => _tris.Count / 3;

            public void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Color ca, Color cb, Color cc)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-14f) return;
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    Vector3 sv = b; b = c; c = sv;
                    Color sc = cb; cb = cc; cc = sc;
                    n = -n;
                }
                n = n.normalized;
                int i0 = _verts.Count;
                _verts.Add(a); _verts.Add(b); _verts.Add(c);
                _normals.Add(n); _normals.Add(n); _normals.Add(n);
                _colours.Add(ca); _colours.Add(cb); _colours.Add(cc);
                _tris.Add(i0); _tris.Add(i0 + 1); _tris.Add(i0 + 2);
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(_verts);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colours);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// Faces over shared corners, each lit as one plane of one colour. A face leads with a corner that carries its
        /// normal and its colour — the first corner of its triangle, which is the corner the GPU hands a flat
        /// (<c>nointerpolation</c>) value from — and borrows whatever corners stand at its other two points, whose normal
        /// and colour it never reads. A corner leads one face at most; one that stands at a point without leading yet is
        /// given to the next face that can lead from there, so a mesh has about as many corners as faces.
        /// </summary>
        private sealed class FacetMeshData
        {
            private readonly List<Vector3> _points = new List<Vector3>();
            private readonly List<int> _cornerAt = new List<int>();
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color> _colours = new List<Color>();
            private readonly List<bool> _leads = new List<bool>();
            private readonly List<int> _tris = new List<int>();

            public int FaceCount => _tris.Count / 3;

            /// <summary>A place faces may share a corner at; the number names it.</summary>
            public int Point(Vector3 position)
            {
                _points.Add(position);
                _cornerAt.Add(-1);
                return _points.Count - 1;
            }

            /// <summary>A face over three points, wound away from a point inside the shape, of one plane and one colour.</summary>
            public void Face(int a, int b, int c, Vector3 inside, Color colour)
            {
                Vector3 pa = _points[a], pb = _points[b], pc = _points[c];
                Vector3 n = Vector3.Cross(pb - pa, pc - pa);
                if (n.sqrMagnitude < 1e-14f) return;
                if (Vector3.Dot(n, (pa + pb + pc) / 3f - inside) < 0f)
                {
                    int swap = b;
                    b = c;
                    c = swap;
                    n = -n;
                }
                n = n.normalized;
                // Turn the three round, keeping the winding, so the face leads from a point whose corner leads nothing yet.
                if (Taken(a) && !Taken(b))
                {
                    int t = a;
                    a = b;
                    b = c;
                    c = t;
                }
                else if (Taken(a) && !Taken(c))
                {
                    int t = a;
                    a = c;
                    c = b;
                    b = t;
                }
                _tris.Add(Lead(a, n, colour));
                _tris.Add(Borrow(b, n, colour));
                _tris.Add(Borrow(c, n, colour));
            }

            private bool Taken(int point) => _cornerAt[point] >= 0 && _leads[_cornerAt[point]];

            private int Lead(int point, Vector3 normal, Color colour)
            {
                int v = _cornerAt[point];
                if (v >= 0 && !_leads[v])
                {
                    _normals[v] = normal;
                    _colours[v] = colour;
                    _leads[v] = true;
                    return v;
                }
                v = Corner(point, normal, colour, true);
                if (_cornerAt[point] < 0) _cornerAt[point] = v;
                return v;
            }

            private int Borrow(int point, Vector3 normal, Color colour)
            {
                int v = _cornerAt[point];
                if (v >= 0) return v;
                v = Corner(point, normal, colour, false);
                _cornerAt[point] = v;
                return v;
            }

            private int Corner(int point, Vector3 normal, Color colour, bool leads)
            {
                _verts.Add(_points[point]);
                _normals.Add(normal);
                _colours.Add(colour);
                _leads.Add(leads);
                return _verts.Count - 1;
            }

            /// <summary>Scales the whole thing so its top sits at y = 1, uniformly, so every share of height survives.</summary>
            public void ScaleToUnitHeight()
            {
                float top = 0f;
                for (int i = 0; i < _verts.Count; i++) if (_verts[i].y > top) top = _verts[i].y;
                if (top <= 1e-5f) return;
                for (int i = 0; i < _verts.Count; i++) _verts[i] = _verts[i] / top;
            }

            /// <summary>The widest the thing spans across, east-west or north-south.</summary>
            public float Width()
            {
                if (_verts.Count == 0) return 0f;
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (Vector3 v in _verts)
                {
                    if (v.x < minX) minX = v.x;
                    if (v.x > maxX) maxX = v.x;
                    if (v.z < minZ) minZ = v.z;
                    if (v.z > maxZ) maxZ = v.z;
                }
                return Mathf.Max(maxX - minX, maxZ - minZ);
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh { name = name };
                if (_verts.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(_verts);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colours);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
