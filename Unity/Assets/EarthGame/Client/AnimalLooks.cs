using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// What an animal looks like, grown as geometry (M1.7b, CANON ruling 29: the looks are made here, nothing is bought or
    /// downloaded). <see cref="AnimalShapes"/> says what a kangaroo and an oystercatcher are made of — a short list of
    /// rigid pieces, each a box, a capsule or an ellipsoid of a stated size and colour — and this turns each piece into a
    /// mesh at its own size, with its colour in its corners, built once and kept, as <see cref="StandMeshes"/> keeps the
    /// trees. A pose moves the pieces and never resizes them, so one mesh a piece serves every pose and every frame.
    ///
    /// <para>The mesh carries the piece's real size rather than being scaled to it by the object that draws it, because
    /// the stand's shader assumes a uniform scale and takes no inverse matrix (ARCHITECTURE §8, 2026-09-11); a piece drawn
    /// at scale one keeps that promise and its faces are lit by their own planes.</para>
    ///
    /// <para>Every face is its own triangle with its own normal and colour, faceted like everything else this game grows,
    /// and jittered a little from face to face so a flank is not one flat sheet of colour.</para>
    /// </summary>
    public static class AnimalLooks
    {
        /// <summary>
        /// How far from the eye the animals' material draws, m. The stand's shader puts an instance in the near band when
        /// it lies within this of the eye the client hands it and folds it away otherwise (<c>EarthGame/StandLit</c>); the
        /// animals have one band and no far form, so the split is set past any distance a world holds and the eye the
        /// material carries is never read. The trees' two bands are the reason that machinery exists, not this.
        /// </summary>
        public const float ReachM = 1.0e6f;

        /// <summary>How far a face's colour is moved either way from its piece's, as a share: the facets of a flank.</summary>
        private const float FaceJitter = 0.045f;

        /// <summary>How many sides a lathed piece has, and how many rings it is stacked from between its poles.</summary>
        private const int Sides = 10, Rings = 6, CapRings = 2;

        /// <summary>How far apart in its own cycle two animals start, s: enough that a mob does not hop as one machine.</summary>
        private const double SpreadSeconds = 4.0;

        private static readonly Dictionary<int, Mesh> Pieces = new Dictionary<int, Mesh>();

        /// <summary>The kind a definition names, or null for anything that is not an animal this build has shapes for.</summary>
        public static AnimalSpecies SpeciesOf(Definition definition)
        {
            if (definition == null || definition.Kind != DefinitionKind.Animal) return null;
            return definition.Row is AnimalSpecies species && AnimalShapes.Draws(species) ? species : null;
        }

        /// <summary>
        /// Where in its own hop or wingbeat an animal of an id is: a start of its own, drawn from the id, so the members of
        /// a mob bound out of step with each other as real ones do.
        /// </summary>
        public static double StartOf(ulong id)
        {
            ulong h = id * 0x9E3779B97F4A7C15UL;
            h ^= h >> 29;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 32;
            return (h >> 11) * (1.0 / 9007199254740992.0) * SpreadSeconds;
        }

        /// <summary>
        /// The mesh one piece of a kind is drawn by, built on first use and kept. The piece's shape, size and colour never
        /// change with its pose (<see cref="AnimalShapes"/>), so its index in the kind's list names its mesh.
        /// </summary>
        public static Mesh Piece(AnimalSpecies species, int index, in AnimalPart part)
        {
            int key = Key(species, index);
            if (Pieces.TryGetValue(key, out Mesh mesh)) return mesh;
            Color colour = new Color(part.Colour.R, part.Colour.G, part.Colour.B, 1f);
            Bits bits = new Bits();
            switch (part.Shape)
            {
                case AnimalPartShape.Box:
                    Box(bits, 0.5f * part.SizeX, 0.5f * part.SizeY, 0.5f * part.SizeZ, colour, key);
                    break;
                case AnimalPartShape.Capsule:
                    Capsule(bits, 0.5f * part.SizeX, 0.5f * part.SizeZ, 0.5f * part.SizeY, colour, key);
                    break;
                default:
                    Ellipsoid(bits, 0.5f * part.SizeX, 0.5f * part.SizeY, 0.5f * part.SizeZ, colour, key);
                    break;
            }
            mesh = bits.ToMesh(species.Name + " " + part.Name);
            Pieces[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// The material the animals are drawn in: a copy of the stand's, which is an asset so that a build keeps its
        /// shader's instanced variants (ProjectSetup, ARCHITECTURE §8). The caller owns it and destroys it.
        /// </summary>
        public static Material MaterialFrom(Material template)
        {
            Material material = new Material(template) { name = "Animals", enableInstancing = true };
            material.SetFloat("_Band", 0f);
            material.SetFloat("_SplitM", ReachM);
            return material;
        }

        private static int Key(AnimalSpecies species, int index) => (species == AnimalSpecies.PiedOystercatcher ? 1 << 8 : 0) | (index & 0xFF);

        // ------------------------------------------------------------------ the solids

        /// <summary>A rectangular block of half-sides <paramref name="hx"/>, <paramref name="hy"/> and <paramref name="hz"/> about its middle.</summary>
        private static void Box(Bits bits, float hx, float hy, float hz, Color colour, int seed)
        {
            Vector3[] c =
            {
                new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz),
                new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz),
            };
            int[] quads =
            {
                0, 1, 2, 3,   4, 5, 6, 7,   0, 1, 5, 4,
                1, 2, 6, 5,   2, 3, 7, 6,   3, 0, 4, 7,
            };
            for (int q = 0; q < quads.Length; q += 4)
            {
                bits.Face(c[quads[q]], c[quads[q + 1]], c[quads[q + 2]], Vector3.zero, Shade(colour, seed, bits.FaceCount));
                bits.Face(c[quads[q]], c[quads[q + 2]], c[quads[q + 3]], Vector3.zero, Shade(colour, seed, bits.FaceCount));
            }
        }

        /// <summary>An ellipsoid of radii <paramref name="rx"/>, <paramref name="ry"/> and <paramref name="rz"/>, lathed in rings of latitude.</summary>
        private static void Ellipsoid(Bits bits, float rx, float ry, float rz, Color colour, int seed)
        {
            float[] ys = new float[Rings], widths = new float[Rings];
            for (int i = 0; i < Rings; i++)
            {
                // From just above the bottom pole to just below the top one; the poles are closed by fans.
                double a = (i + 1.0) / (Rings + 1.0) * System.Math.PI - System.Math.PI * 0.5;
                ys[i] = (float)(ry * System.Math.Sin(a));
                widths[i] = (float)System.Math.Cos(a);
            }
            Lathe(bits, ys, widths, rx, rz, -ry, ry, colour, seed);
        }

        /// <summary>
        /// A capsule along y: a tube of radii <paramref name="rx"/> and <paramref name="rz"/> whose straight part runs from
        /// −<paramref name="half"/> + r to <paramref name="half"/> − r, with a rounded cap on each end, so the whole piece
        /// is 2 × <paramref name="half"/> long. A bone shorter than it is thick is all cap.
        /// </summary>
        private static void Capsule(Bits bits, float rx, float rz, float half, Color colour, int seed)
        {
            float radius = Mathf.Max(rx, rz);
            float straight = Mathf.Max(0f, half - radius);
            float capUp = half - straight;
            List<float> ys = new List<float>();
            List<float> widths = new List<float>();
            for (int i = 1; i <= CapRings; i++)
            {
                double a = i / (CapRings + 1.0) * System.Math.PI * 0.5;
                ys.Add(-straight - (float)(capUp * System.Math.Cos(a)));
                widths.Add((float)System.Math.Sin(a));
            }
            ys.Add(-straight);
            widths.Add(1f);
            ys.Add(straight);
            widths.Add(1f);
            for (int i = CapRings; i >= 1; i--)
            {
                double a = i / (CapRings + 1.0) * System.Math.PI * 0.5;
                ys.Add(straight + (float)(capUp * System.Math.Cos(a)));
                widths.Add((float)System.Math.Sin(a));
            }
            Lathe(bits, ys.ToArray(), widths.ToArray(), rx, rz, -half, half, colour, seed);
        }

        /// <summary>
        /// Stacks rings of <see cref="Sides"/> corners about the y axis and skins them, closing each end with a fan to its
        /// pole: the one routine every rounded piece of an animal is made by.
        /// </summary>
        private static void Lathe(Bits bits, float[] ys, float[] widths, float rx, float rz, float bottomY, float topY, Color colour, int seed)
        {
            Vector3[][] rings = new Vector3[ys.Length][];
            for (int i = 0; i < ys.Length; i++)
            {
                rings[i] = new Vector3[Sides];
                for (int k = 0; k < Sides; k++)
                {
                    double a = k * (2.0 * System.Math.PI / Sides);
                    rings[i][k] = new Vector3((float)(rx * widths[i] * System.Math.Cos(a)), ys[i], (float)(rz * widths[i] * System.Math.Sin(a)));
                }
            }
            for (int i = 0; i + 1 < rings.Length; i++)
            {
                Vector3 inside = new Vector3(0f, 0.5f * (ys[i] + ys[i + 1]), 0f);
                for (int k = 0; k < Sides; k++)
                {
                    int k2 = (k + 1) % Sides;
                    bits.Face(rings[i][k], rings[i + 1][k], rings[i + 1][k2], inside, Shade(colour, seed, bits.FaceCount));
                    bits.Face(rings[i][k], rings[i + 1][k2], rings[i][k2], inside, Shade(colour, seed, bits.FaceCount));
                }
            }
            Vector3 bottom = new Vector3(0f, bottomY, 0f), top = new Vector3(0f, topY, 0f);
            Vector3[] first = rings[0], last = rings[rings.Length - 1];
            for (int k = 0; k < Sides; k++)
            {
                int k2 = (k + 1) % Sides;
                bits.Face(bottom, first[k], first[k2], new Vector3(0f, ys[0], 0f), Shade(colour, seed, bits.FaceCount));
                bits.Face(top, last[k], last[k2], new Vector3(0f, ys[ys.Length - 1], 0f), Shade(colour, seed, bits.FaceCount));
            }
        }

        private static Color Shade(Color colour, int seed, int face)
        {
            float f = 1f + (Hash01(seed, face) - 0.5f) * 2f * FaceJitter;
            return new Color(Mathf.Clamp01(colour.r * f), Mathf.Clamp01(colour.g * f), Mathf.Clamp01(colour.b * f), 1f);
        }

        /// <summary>A value in [0, 1) from two whole numbers, so a face's shade never depends on the order it was built in.</summary>
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

        /// <summary>
        /// A triangle soup wound away from a point inside the solid, each face carrying its own plane and colour on all
        /// three of its corners: the shader takes both from the leading one and interpolates neither, so a face is one
        /// plane of one colour (ARCHITECTURE §8).
        /// </summary>
        private sealed class Bits
        {
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color> _colours = new List<Color>();
            private readonly List<int> _tris = new List<int>();

            public int FaceCount => _tris.Count / 3;

            public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Color colour)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-16f) return;
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                    n = -n;
                }
                n = n.normalized;
                int i0 = _verts.Count;
                _verts.Add(a);
                _verts.Add(b);
                _verts.Add(c);
                _normals.Add(n);
                _normals.Add(n);
                _normals.Add(n);
                _colours.Add(colour);
                _colours.Add(colour);
                _colours.Add(colour);
                _tris.Add(i0);
                _tris.Add(i0 + 1);
                _tris.Add(i0 + 2);
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
    }
}
