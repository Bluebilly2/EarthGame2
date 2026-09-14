using System.IO;
using EarthGame.Client;
using EarthGame.Engine;
using NUnit.Framework;
using UnityEngine;

namespace EarthGame.Tests.Editor
{
    /// <summary>
    /// The terrain tile's posts are the raster's surface, oriented the right way round: Unity's +X is east and
    /// +Z is north, the heightmap is [north, east], and the tile's south-west corner is where the builder was
    /// told. The fixture is the same tiny raster the engine's own loader tests read.
    /// </summary>
    public sealed class TerrainTileBuilderTests
    {
        internal static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>The tiny fixture raster, for this file's tests and <see cref="TileReleaseTests"/>.</summary>
        internal static Heightfield Tiny()
        {
            string sidecar = Path.Combine(RepoRoot, "Data", "fixtures", "raster", "tiny.json");
            Assert.That(File.Exists(sidecar), Is.True, "fixture missing: " + sidecar);
            return new Heightfield(RegionRaster.Load(sidecar));
        }

        private static double Law(int row, int col) => 100.0 + row + 10.0 * col + (row == 2 && col == 2 ? 5.0 : 0.0);

        [Test]
        public void PostsAreTheRastersSurfaceIndexedNorthThenEast()
        {
            float[,] posts = TerrainTileBuilder.SamplePosts(Tiny(), -20.0, -20.0, 5, 10.0, out float minM, out float maxM);
            for (int z = 0; z < 5; z++)
                for (int x = 0; x < 5; x++)
                    Assert.That(posts[z, x], Is.EqualTo((float)Law(4 - z, x)).Within(1e-4f), "post north " + z + ", east " + x + " (row " + (4 - z) + ", col " + x + ")");
            Assert.That(minM, Is.EqualTo(100f));
            Assert.That(maxM, Is.EqualTo(144f));
        }

        [Test]
        public void TheBuiltTileStandsWhereItWasToldWithTheRightHeights()
        {
            Terrain tile = TerrainTileBuilder.Build(Tiny(), -20.0, -20.0, null, null, "test tile");
            try
            {
                Assert.That(tile.transform.position.x, Is.EqualTo(-20f));
                Assert.That(tile.transform.position.z, Is.EqualTo(-20f));
                Assert.That(tile.transform.position.y, Is.EqualTo(100f), "the tile's base is its lowest post");
                TerrainData data = tile.terrainData;
                Assert.That(data.heightmapResolution, Is.EqualTo(TerrainTileBuilder.TilePosts));
                Assert.That(data.size.x, Is.EqualTo(TerrainTileBuilder.TileSizeM));
                // The south-west post is row 4, column 0 of the raster; the sampled world height is base + relative.
                float southWest = data.GetHeight(0, 0) + tile.transform.position.y;
                Assert.That(southWest, Is.EqualTo((float)Law(4, 0)).Within(0.01f));
                // Ten metres east of the south-west corner, still on the first raster row from the south.
                float posts10m = 10f / TerrainTileBuilder.PostSpacingM;
                float east10 = data.GetInterpolatedHeight(posts10m / (TerrainTileBuilder.TilePosts - 1), 0f) + tile.transform.position.y;
                Assert.That(east10, Is.EqualTo((float)Law(4, 1)).Within(0.05f));
                Assert.That(tile.gameObject.layer, Is.EqualTo(EarthGame.Shared.Layers.Terrain));
                Assert.That(tile.GetComponent<TerrainCollider>(), Is.Not.Null, "the tile is the client's ground");
            }
            finally
            {
                Object.DestroyImmediate(tile.gameObject);
            }
        }
    }
}
