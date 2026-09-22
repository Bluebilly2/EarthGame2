using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using EarthGame.Transport;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// The understorey's variety and where it stops (M1.6e promises 2 and 3): nothing under standing water and only sedge
    /// in the shallows, a cover's companion shape and herbs between, density in patches with the stated mean.
    /// </summary>
    public sealed class UnderstoreyVarietyTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        private const double East = 300.0, North = -300.0;
        private const int Quarter = 2;

        /// <summary>Grass everywhere; a lake a metre deep over a block of cells north-east of the founder and a film a hand deep over a block south-east.</summary>
        private static bool Deep(int row, int col) => row >= 104 && row <= 106 && col >= 114 && col <= 116;
        private static bool Shallow(int row, int col) => row >= 114 && row <= 116 && col >= 114 && col <= 116;

        private static WorldState World()
        {
            RegionRaster cover = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_cover", "cover",
                (row, col) => GroundCovers.Pack(GroundCover.Grass, Quarter), null);
            RegionRaster surface = TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_surface",
                (row, col) => TestRasters.MadeCoastHeight(row, col) + (Deep(row, col) ? 1.0f : Shallow(row, col) ? 0.10f : 0f));
            RegionRaster classes = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_water", "water",
                (row, col) => Deep(row, col) || Shallow(row, col) ? (uint)WaterClass.Lake : (uint)WaterClass.Dry, null);
            return new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()), 0, null, new WorldWater(surface, classes), cover);
        }

        private static GameClient Joined(WorldState world)
        {
            InMemoryTransport.CreatePair(out IServerTransport st, out IClientTransport _);
            GameServer server = new GameServer(new ServerConfig { InterestRadiusM = 100.0, InterestMarginM = 50.0 }, st, world);
            server.Listen(1);
            SavedPlayer player = default;
            player.Name = "William";
            player.Body = MoverState.AtRest(East, world.Terrain.HeightAt(East, North), North);
            player.Body.Grounded = true;
            server.RememberPlayers(new[] { player });
            GameClient client = new GameClient(InMemoryTransport.CreateClient(st));
            client.Connect("memory", 1, "William", "");
            long ms = 0;
            for (int i = 0; i < 40; i++)
            {
                client.Update(ms);
                server.Update(0.05);
                client.Update(ms);
                ms += 50;
            }
            Assert.That(client.State, Is.EqualTo(ClientState.Connected));
            TileId id = client.Grid.ForPosition(East, North);
            Assert.That(client.Tiles.Holding(TileLayer.GroundCover, id), Is.Not.Null, "the cover tile arrived");
            Assert.That(client.Tiles.Holding(TileLayer.WaterDepth, id), Is.Not.Null, "and the water's depth");
            return client;
        }

        private static List<UnderstoreyTuft> At(GameClient client, double east, double north, double radiusM)
        {
            List<UnderstoreyTuft> tufts = new List<UnderstoreyTuft>();
            Understorey.Find(east, north, radiusM, client.Tiles, client.Grid, tufts);
            return tufts;
        }

        private static void Centre(int row, int col, out double east, out double north)
            => StandLayout.CellCentre(row, col, TestRasters.MadeCellM, TestRasters.MadeExtentM, out east, out north);

        [Test]
        public void NothingGrowsUnderStandingWaterAndOnlySedgeInTheShallows()
        {
            GameClient client = Joined(World());
            Centre(105, 115, out double deepEast, out double deepNorth);
            Centre(115, 115, out double shallowEast, out double shallowNorth);
            Centre(110, 110, out double dryEast, out double dryNorth);
            Assert.That(At(client, deepEast, deepNorth, 4.0), Is.Empty, "a metre of water over the grass: nothing stands in it");
            List<UnderstoreyTuft> shallows = At(client, shallowEast, shallowNorth, 4.0);
            Assert.That(shallows, Is.Not.Empty, "a hand of water: the margin's sedge stands");
            foreach (UnderstoreyTuft tuft in shallows) Assert.That(tuft.Shape, Is.EqualTo(TuftShape.Clump), "and nothing else");
            List<UnderstoreyTuft> dry = At(client, dryEast, dryNorth, 4.0);
            Assert.That(dry, Is.Not.Empty);
            Assert.That(dry.Exists(t => t.Shape == TuftShape.Tussock), "dry grass grows its tussocks");
        }

        [Test]
        public void WhatStandsInWaterIsAShapesOwnRule()
        {
            Assert.That(Understorey.Stands(TuftShape.Tussock, 0.0), Is.True);
            Assert.That(Understorey.Stands(TuftShape.Tussock, Understorey.WetFootM * 0.5), Is.True, "a damp foot is dry enough");
            Assert.That(Understorey.Stands(TuftShape.Tussock, 0.1), Is.False, "a tussock does not stand in water");
            Assert.That(Understorey.Stands(TuftShape.Herb, 0.1), Is.False, "nor a herb");
            Assert.That(Understorey.Stands(TuftShape.Clump, 0.1), Is.True, "sedge stands in the shallows");
            Assert.That(Understorey.Stands(TuftShape.Clump, Understorey.DeepestTuftM + 0.01), Is.False, "but not in deep water");
        }

        [Test]
        public void ACoverGrowsItsCompanionAndHerbsBetween()
        {
            Assert.That(Understorey.Shapes, Is.EqualTo(5), "the four shapes and the herb");
            Assert.That(Understorey.Companion(GroundCover.Grass, out TuftShape shape, out double share), Is.True);
            Assert.That(shape, Is.EqualTo(TuftShape.Clump));
            Assert.That(share, Is.InRange(0.05, 0.4));
            Assert.That(Understorey.Companion(GroundCover.Heath, out shape, out _), Is.True);
            Assert.That(shape, Is.EqualTo(TuftShape.Tussock), "tussocks between the heath's shrubs");
            Assert.That(Understorey.Companion(GroundCover.Sand, out _, out _), Is.False);

            GameClient client = Joined(World());
            Centre(110, 106, out double east, out double north);
            const double radius = 40.0;
            List<UnderstoreyTuft> tufts = At(client, east, north, radius);
            int tussocks = 0, clumps = 0, herbs = 0, other = 0;
            foreach (UnderstoreyTuft t in tufts)
            {
                if (t.Shape == TuftShape.Tussock) tussocks++;
                else if (t.Shape == TuftShape.Clump) clumps++;
                else if (t.Shape == TuftShape.Herb) herbs++;
                else other++;
            }
            Assert.That(other, Is.Zero, "grass grows tussocks, its companion clumps, and herbs, nothing else");
            Assert.That(tussocks, Is.GreaterThan(clumps), "the cover's own shape is the greater part");
            Assert.That(clumps / (double)(tussocks + clumps), Is.EqualTo(share).Within(0.06), "the companion at its share");
            double area = Math.PI * radius * radius;
            Assert.That(herbs / area, Is.EqualTo(Understorey.HerbPerSquareM).Within(0.3 * Understorey.HerbPerSquareM), "herbs at their density");
            Assert.That(tufts.Exists(t => t.Shape == TuftShape.Herb && t.HeightM < 0.25f), "a herb is low");
        }

        [Test]
        public void TheDensityVariesInPatchesWithTheStatedMean()
        {
            double least = double.MaxValue, most = 0.0, sum = 0.0;
            int cells = 0;
            for (int row = 0; row < 60; row++)
                for (int col = 0; col < 60; col++)
                {
                    double patch = Understorey.PatchAt(row, col);
                    least = Math.Min(least, patch);
                    most = Math.Max(most, patch);
                    sum += patch;
                    cells++;
                }
            Assert.That(least, Is.LessThan(0.75), "somewhere thin");
            Assert.That(most, Is.GreaterThan(1.25), "somewhere thick");
            Assert.That(most, Is.GreaterThanOrEqualTo(1.5 * least), "the thickest cell half again the thinnest at least");
            Assert.That(sum / cells, Is.EqualTo(1.0).Within(0.1), "the stated density is the mean");
            Assert.That(Understorey.PatchAt(7, 9), Is.EqualTo(Understorey.PatchAt(7, 9)), "and a cell answers the same every time");
            Assert.That(Math.Abs(Understorey.PatchAt(7, 9) - Understorey.PatchAt(7, 10)), Is.LessThan(0.35), "neighbours are alike: patches, not speckle");
        }
    }
}
