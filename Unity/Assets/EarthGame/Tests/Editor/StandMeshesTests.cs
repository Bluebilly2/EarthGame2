using EarthGame.Client;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;
using UnityEngine;

namespace EarthGame.Tests.Editor
{
    /// <summary>
    /// The trees share their corners and still light every face as one plane (M1.6a, 2026-09-11). The shader takes a
    /// face's normal and colour from the first corner of its triangle alone, so that corner must carry the face's own
    /// normal, the one its winding faces; and the corners must really be shared, or a tree costs what it did when each
    /// face had three of its own. A builder that broke either would draw blotchy trees or slow ones, and only a frame
    /// would show it. And every colour those meshes and the animals' skins carry is their tables' in linear light (M1.6h).
    /// </summary>
    public sealed class StandMeshesTests
    {
        [Test]
        public void EveryFaceLeadsWithItsOwnNormalAndTheCornersAreShared()
        {
            for (int tall = 0; tall < StandCodes.Tall.Count; tall++)
            {
                for (int variant = 0; variant < StandPreparation.Variants; variant++)
                    AssertFacets(StandMeshes.Tree(tall, variant, out _), StandCodes.Tall[tall].Name + " variant " + variant);
                AssertFacets(StandMeshes.FarTree(tall), StandCodes.Tall[tall].Name + " far");
            }
        }

        /// <summary>
        /// The rocks (BF.4 stage three) are drawn from the superellipsoid the server judges a founder standing on one by: every
        /// corner of a drawn rock lies within the stated roughness of it, and every corner of a rock's body on it, so what is drawn,
        /// what stops a founder and what the server holds are one shape to a few centimetres. A body keeps under the 255 faces the
        /// physics allows a convex one.
        /// </summary>
        [Test]
        public void EveryRockIsDrawnAndGivenABodyFromItsSuperellipsoid()
        {
            for (int stone = 0; stone < StoneType.All.Count; stone++)
                foreach (RockForm form in new[] { RockForm.Boulder, RockForm.Ledge })
                    for (int v = 0; v < StandingRocks.Variants; v++)
                    {
                        StandingRocks.ShapeOf(stone, form, v, out double up, out double around);
                        string name = StoneType.All[stone].Name + " " + form + " " + v;
                        Mesh drawn = StandMeshes.Rock(stone, form, v), body = StandMeshes.RockBody(stone, form, v);
                        Assert.That(drawn.triangles.Length / 3, Is.GreaterThan(100), name);
                        foreach (Vector3 p in drawn.vertices)
                            Assert.That(Measure(p, up, around), Is.InRange(1.0 - StandMeshes.RockRoughness - 1e-4, 1.0 + StandMeshes.RockRoughness + 1e-4), name + " drawn");
                        foreach (Vector3 p in body.vertices)
                            Assert.That(Measure(p, up, around), Is.EqualTo(1.0).Within(1e-4), name + " body");
                        Assert.That(body.triangles.Length / 3, Is.LessThan(255), name + ": a convex body holds at most 255 faces");
                    }
        }

        /// <summary>
        /// Every colour the stand shader draws is its table's in linear light (M1.6h, 2026-09-25). The shader takes a vertex
        /// colour as linear light and the tables are written in sRGB, so a mesh that carried a table colour as it stands drew it
        /// too pale, as every crown, trunk, tuft, stick, cobble and far tree did until then. Each corner's colour is held to the
        /// way its builder makes it: a table colour scaled by a face's jitter, or a blend of two so scaled (bark, a tuft's foot
        /// and tip), a crown's green nudged in the table's own terms, a rock's foot darkened and its faces stained or lichened;
        /// with the table colours made linear by Unity's own decoding, not the meshes' one. A corner carried as sRGB is from one
        /// and a fifth to five times too bright for any of those, and off their mix besides.
        /// </summary>
        [Test]
        public void EveryColourDrawnIsItsTablesInLinearLight()
        {
            _unmade.Clear();
            for (int tall = 0; tall < StandCodes.Tall.Count; tall++)
            {
                TreeForm form = StandForms.ForTall(tall);
                string name = StandCodes.Tall[tall].Name;
                for (int variant = 0; variant < StandPreparation.Variants; variant++)
                {
                    AssertColours(StandMeshes.Tree(tall, variant, out _), name + " variant " + variant, c => Bark(c, form) || Foliage(c, form));
                    AssertColours(StandMeshes.StrippedTree(tall, variant), name + " stripped, variant " + variant,
                        c => Scaled(c, Linear(StandForms.Sapwood), FaceLow, FaceHigh) || Bark(c, form) || Foliage(c, form));
                }
                AssertColours(StandMeshes.FarTree(tall), name + " far", c => Bark(c, form) || Foliage(c, form));
            }
            Vector3 stick = Linear(StandForms.Stick), cobble = Linear(StandForms.Cobble);
            for (int variant = 0; variant < StandPreparation.Variants; variant++)
            {
                AssertColours(StandMeshes.Stick(variant), "stick " + variant, c => Scaled(c, stick, FaceLow, FaceHigh));
                AssertColours(StandMeshes.Strip(variant), "strip " + variant, c => Scaled(c, stick, FaceLow, FaceHigh));
                AssertColours(StandMeshes.Cord(variant), "cord " + variant, c => Scaled(c, stick, FaceLow, FaceHigh));
                AssertColours(StandMeshes.Log(variant), "log " + variant, c => Scaled(c, stick, FaceLow, FaceHigh));
                AssertColours(StandMeshes.Cobble(variant), "cobble " + variant, c => Scaled(c, cobble, FaceLow, FaceHigh));
                foreach (TuftShape shape in (TuftShape[])System.Enum.GetValues(typeof(TuftShape)))
                {
                    Vector3 low = Linear(StandForms.TuftLow(shape)), high = Linear(StandForms.TuftHigh(shape));
                    AssertColours(StandMeshes.Tuft(shape, variant), shape + " " + variant, c => Between(c, low, high, FaceLow, FaceHigh));
                }
            }
            Vector3 stain = Linear(StandForms.RockStain), lichen = Linear(StandForms.Lichen);
            for (int stone = 0; stone < StoneType.All.Count; stone++)
            {
                Vector3 rock = Linear(StandForms.RockOf(StoneType.All[stone]));
                foreach (RockForm form in new[] { RockForm.Boulder, RockForm.Ledge })
                    for (int v = 0; v < StandingRocks.Variants; v++)
                        AssertColours(StandMeshes.Rock(stone, form, v), StoneType.All[stone].Name + " " + form + " " + v, c => Rock(c, rock, stain, lichen));
            }
            Assert.That(_unmade, Is.Empty, _unmade.Count + " mesh(es) carry a colour none of their tables make in linear light:\n" + string.Join("\n", _unmade));
        }

        /// <summary>
        /// An animal's skin is its tables' colours in linear light (M1.6h): each corner of every kind drawn the colour the skin
        /// was grown with (<see cref="AnimalBody.VertexColour"/>, in sRGB as the tables are), decoded by Unity's own decoding.
        /// </summary>
        [Test]
        public void EveryAnimalsSkinIsItsTablesInLinearLight()
        {
            int drawn = 0;
            foreach (AnimalSpecies species in AnimalSpecies.All)
            {
                if (!AnimalShapes.Draws(species)) continue;
                drawn++;
                AnimalBody body = AnimalShapes.BodyOf(species);
                Color[] colours = AnimalLooks.Body(species).colors;
                Assert.That(colours.Length, Is.EqualTo(body.VertexCount), species.Name);
                for (int v = 0; v < colours.Length; v++)
                {
                    Vector3 expected = Linear(body.VertexColour[v]);
                    string what = species.Name + " corner " + v + ": " + colours[v] + " where its table's colour in linear light is " + expected;
                    Assert.That(colours[v].r, Is.EqualTo(expected.x).Within(1e-5f), what);
                    Assert.That(colours[v].g, Is.EqualTo(expected.y).Within(1e-5f), what);
                    Assert.That(colours[v].b, Is.EqualTo(expected.z).Within(1e-5f), what);
                }
            }
            Assert.That(drawn, Is.GreaterThan(0), "no animal is drawn, so no skin was held to its tables");
        }

        /// <summary>How far a face's colour strays either way from its own, as the builders make it: a face's jitter and a tuft's or a stone's.</summary>
        private const float FaceLow = 0.9f, FaceHigh = 1.1f;

        /// <summary>A crown's clump and face together, and a rock's foot three quarters as bright as its middle.</summary>
        private const float CrownLow = 0.85f, CrownHigh = 1.15f, RockLow = 0.65f, RockHigh = 1.1f;

        /// <summary>How far a crown's green is nudged, in the table's own terms.</summary>
        private const float Nudge = 0.03f;

        /// <summary>A table colour in linear light by Unity's own decoding: the reference the meshes' one decoding is held to.</summary>
        private static Vector3 Linear(Rgb c) => new Vector3(Mathf.GammaToLinearSpace(c.R), Mathf.GammaToLinearSpace(c.G), Mathf.GammaToLinearSpace(c.B));

        /// <summary>Every mesh whose colours are not all its tables', with its first such corner: all of them listed, not the first alone.</summary>
        private readonly System.Collections.Generic.List<string> _unmade = new System.Collections.Generic.List<string>();

        private void AssertColours(Mesh mesh, string name, System.Func<Color, bool> made)
        {
            Color[] colours = mesh.colors;
            Assert.That(colours.Length, Is.EqualTo(mesh.vertexCount), name + ": a colour for every corner");
            for (int v = 0; v < colours.Length; v++)
                if (!made(colours[v]))
                {
                    _unmade.Add(name + ": corner " + v + " is " + colours[v]);
                    return;
                }
        }

        /// <summary>A trunk's or a limb's: its bark's two colours blended up the stocking line and scaled by a face's jitter.</summary>
        private static bool Bark(Color c, TreeForm form) => Between(c, Linear(form.BarkLow), Linear(form.BarkHigh), FaceLow, FaceHigh);

        /// <summary>A crown's: the foliage with its green nudged in the table's terms, scaled by a clump's and a face's jitter.</summary>
        private static bool Foliage(Color c, TreeForm form)
        {
            Vector3 f = Linear(form.Foliage);
            float red = c.r / f.x, blue = c.b / f.z;
            if (Mathf.Abs(red - blue) > 1e-4f * red || red < CrownLow || red > CrownHigh) return false;
            float green = Mathf.LinearToGammaSpace(c.g / red);
            return Mathf.Abs(green - form.Foliage.G) <= Nudge + 1e-3f;
        }

        /// <summary>A colour scaled by one number, the same in every channel, within the band.</summary>
        private static bool Scaled(Color c, Vector3 p, float low, float high)
        {
            float r = c.r / p.x, g = c.g / p.y, b = c.b / p.z;
            float spread = Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b));
            return spread <= 1e-4f * r && r >= low - 1e-3f && r <= high + 1e-3f;
        }

        /// <summary>
        /// A blend of two colours, anywhere from the first to the second, scaled by one number within the band: the least-squares
        /// fit of the colour to the plane the two span, which must leave nothing over, blend within them and scale within the band.
        /// </summary>
        private static bool Between(Color c, Vector3 a, Vector3 b, float low, float high)
        {
            Vector3 d = b - a, v = new Vector3(c.r, c.g, c.b);
            double aa = Vector3.Dot(a, a), ad = Vector3.Dot(a, d), dd = Vector3.Dot(d, d), av = Vector3.Dot(a, v), dv = Vector3.Dot(d, v);
            double det = aa * dd - ad * ad;
            if (dd < 1e-12 || det < 1e-9 * aa * dd) return Scaled(c, a, low, high) || Scaled(c, b, low, high);
            double scale = (av * dd - ad * dv) / det, along = (aa * dv - ad * av) / det;
            Vector3 fit = (float)scale * a + (float)along * d;
            if ((v - fit).magnitude > 1e-4f * v.magnitude + 1e-6f) return false;
            double t = along / scale;
            return scale >= low - 5e-3 && scale <= high + 5e-3 && t >= -0.01 && t <= 1.01;
        }

        /// <summary>
        /// A rock's face: its stone's colour scaled by its jitter and its damp foot, then perhaps half-way to the rust's stain and
        /// perhaps half-way to the lichen, as the builder paints them in turn.
        /// </summary>
        private static bool Rock(Color c, Vector3 stone, Vector3 stain, Vector3 lichen)
        {
            Vector3 v = new Vector3(c.r, c.g, c.b);
            return RockScaled(v, stone, 1f, Vector3.zero)
                   || RockScaled(v, stone, 0.55f, 0.45f * stain)
                   || RockScaled(v, stone, 0.5f, 0.5f * lichen)
                   || RockScaled(v, stone, 0.275f, 0.225f * stain + 0.5f * lichen);
        }

        private static bool RockScaled(Vector3 v, Vector3 stone, float share, Vector3 added)
        {
            Vector3 k = new Vector3((v.x - added.x) / (share * stone.x), (v.y - added.y) / (share * stone.y), (v.z - added.z) / (share * stone.z));
            float spread = Mathf.Max(k.x, Mathf.Max(k.y, k.z)) - Mathf.Min(k.x, Mathf.Min(k.y, k.z));
            return spread <= 1e-3f * Mathf.Abs(k.x) && k.x >= RockLow && k.x <= RockHigh;
        }

        /// <summary>The superellipsoid's own measure of a point at unit half-axes, of the first degree: 1 on its surface.</summary>
        private static double Measure(Vector3 p, double up, double around)
        {
            double horizontal = System.Math.Pow(System.Math.Pow(System.Math.Abs(p.x), 2.0 / around) + System.Math.Pow(System.Math.Abs(p.z), 2.0 / around), around / up);
            return System.Math.Pow(horizontal + System.Math.Pow(System.Math.Abs(p.y), 2.0 / up), up / 2.0);
        }

        private static void AssertFacets(Mesh mesh, string name)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] tris = mesh.triangles;
            int faces = tris.Length / 3;
            Assert.That(faces, Is.GreaterThan(0), name);
            // Three corners a face is a soup; about one a face is what sharing gives.
            Assert.That(verts.Length, Is.LessThan(1.5 * faces), name + ": " + verts.Length + " corners for " + faces + " faces");
            for (int f = 0; f < faces; f++)
            {
                Vector3 a = verts[tris[3 * f]], b = verts[tris[3 * f + 1]], c = verts[tris[3 * f + 2]];
                Vector3 facing = Vector3.Cross(b - a, c - a);
                Assert.That(facing.sqrMagnitude, Is.GreaterThan(1e-14f), name + ": face " + f + " has no area");
                float agree = Vector3.Dot(facing.normalized, normals[tris[3 * f]].normalized);
                Assert.That(agree, Is.GreaterThan(0.999f), name + ": face " + f + " leads with a normal " + agree + " off its own");
            }
        }
    }
}
