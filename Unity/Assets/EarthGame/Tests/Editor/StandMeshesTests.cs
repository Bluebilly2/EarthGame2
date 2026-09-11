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
