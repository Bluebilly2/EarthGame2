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
    /// would show it.
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
