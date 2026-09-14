using System.Collections.Generic;
using EarthGame.Client;
using EarthGame.ClientCore;
using NUnit.Framework;
using UnityEngine;

namespace EarthGame.Tests.Editor
{
    /// <summary>
    /// What a tile leaves when it is let go (M1.4f): nothing. A Terrain's data and a mesh are objects Unity keeps until
    /// they are destroyed themselves, and destroying the GameObjects alone left both behind for every tile a walk let
    /// go of (the bug hunt of 2026-09-13); and the hole cut in the coarse ground under a tile is filled once the tile
    /// has gone. In edit mode a freed object goes at once, so what is gone can be asked in the same test; a destroyed
    /// Unity object compares equal to null and is nothing else, which is why the tests ask that and not Is.Null.
    /// </summary>
    public sealed class TileReleaseTests
    {
        [Test]
        public void AFreedTileTakesItsDataWithIt()
        {
            Terrain tile = TerrainTileBuilder.Build(TerrainTileBuilderTests.Tiny(), -20.0, -20.0, 40f, 33, null, null, "released tile", true, 0f);
            TerrainData data = tile.terrainData;
            Assert.That(data == null, Is.False, "a built tile has data");
            TerrainTileBuilder.Free(tile);
            Assert.That(tile == null, Is.True, "the tile is gone");
            Assert.That(data == null, Is.True, "and its data with it");
        }

        [Test]
        public void AFreedWaterTileTakesItsMeshWithIt()
        {
            List<WaterQuad> quads = new List<WaterQuad>
            {
                new WaterQuad { EastFrom = 0.0, EastTo = 10.0, NorthFrom = 0.0, NorthTo = 10.0, SurfaceUp = 1f },
            };
            GameObject water = WaterTileBuilder.Build(quads, null, "released water");
            Mesh mesh = water.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh == null, Is.False, "built water has a mesh");
            WaterTileBuilder.Free(water);
            Assert.That(water == null, Is.True, "the water is gone");
            Assert.That(mesh == null, Is.True, "and its mesh with it");
        }

        [Test]
        public void TheHoleUnderATileIsFilledOnceTheTileHasGone()
        {
            // A 40 m coarse tile at 33 posts; a 10 m tile stood on its middle, so the cells 8 to 16 of 32 are under it.
            Terrain coarse = TerrainTileBuilder.Build(TerrainTileBuilderTests.Tiny(), -20.0, -20.0, 40f, 33, null, null, "coarse under a tile", true, 0f);
            try
            {
                TerrainData data = coarse.terrainData;
                Assert.That(data.IsHole(12, 12), Is.False, "whole to begin with");
                TerrainTileBuilder.CutHole(coarse, -20.0, -20.0, -10.0, -10.0, 10.0);
                Assert.That(data.IsHole(12, 12), Is.True, "cut where the tile stands");
                Assert.That(data.IsHole(4, 4), Is.False, "and nowhere else");
                TerrainTileBuilder.FillHole(coarse, -20.0, -20.0, -10.0, -10.0, 10.0);
                Assert.That(data.IsHole(12, 12), Is.False, "filled once the tile has gone");
                Assert.That(data.IsHole(4, 4), Is.False);
            }
            finally
            {
                TerrainTileBuilder.Free(coarse);
            }
        }
    }
}
