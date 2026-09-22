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
    /// What grows underfoot, placed from the cover a client was streamed (M1.6c promises 1, 3 and 7): the cover's own
    /// shape on each cell, the same tufts every time, more and taller where the land is wetter, none on sand or under a
    /// canopy's litter, each standing on the tile's ground and out of a trunk's bark.
    /// </summary>
    public sealed class UnderstoreyTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        /// <summary>The founder stands at the cell whose centre is (300, -300) on the made coast; the bands run north and south beside them.</summary>
        private const double East = 300.0, North = -300.0;
        private const int HeathQuarter = 1, GrassQuarter = 3;
        /// <summary>The cell of the tree: two cells west of the founder, in the heath.</summary>
        private const int TreeRow = 110, TreeCol = 108;
        private const double TreeHeightM = 12.5;

        private static WorldState World()
        {
            // Heath west of the founder, grass in a band east of them, sand beyond it.
            RegionRaster cover = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_cover", "cover",
                (row, col) => col <= 110 ? GroundCovers.Pack(GroundCover.Heath, HeathQuarter)
                    : col <= 115 ? GroundCovers.Pack(GroundCover.Grass, GrassQuarter)
                    : GroundCovers.Pack(GroundCover.Sand, 0), null);
            RegionRaster stand = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_stand", "stand",
                (row, col) => row == TreeRow && col == TreeCol ? StandCodes.Pack(PlantSpecies.OldManBanksia, TreeHeightM) : 0u, null);
            return new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()), 0, null, null, cover, stand);
        }

        /// <summary>A client joined to a server of that world, standing where the bands meet, with the tiles round it held.</summary>
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
            Assert.That(client.Tiles.Holding(TileLayer.GroundCover, client.Grid.ForPosition(East, North)), Is.Not.Null, "the cover tile arrived");
            return client;
        }

        private static List<UnderstoreyTuft> At(GameClient client, double east, double north, double radiusM)
        {
            List<UnderstoreyTuft> tufts = new List<UnderstoreyTuft>();
            Understorey.Find(east, north, radiusM, client.Tiles, client.Grid, tufts);
            return tufts;
        }

        [Test]
        public void TheGroundGrowsWhatTheCoverSays()
        {
            GameClient client = Joined(World());
            List<UnderstoreyTuft> heath = At(client, East - 10.0, North, 6.0);
            List<UnderstoreyTuft> grass = At(client, East + 30.0, North, 6.0);
            List<UnderstoreyTuft> sand = At(client, East + 80.0, North, 6.0);

            // Since M1.6e a cover grows its companion between its own shape and herbs under both, so the cover's own shape is
            // the greater part of what is not a herb, and a tuft's size runs wider.
            Assert.That(heath, Is.Not.Empty, "the heath grows shrubs");
            int shrubs = 0, heathOthers = 0;
            foreach (UnderstoreyTuft tuft in heath)
            {
                if (tuft.Shape == TuftShape.Herb) continue;
                if (tuft.Shape == TuftShape.Shrub) shrubs++; else { heathOthers++; Assert.That(tuft.Shape, Is.EqualTo(TuftShape.Tussock), "the heath's companion"); continue; }
                double stands = Understorey.HeightIn(0.90, HeathQuarter);
                Assert.That(tuft.HeightM, Is.InRange(stands * Understorey.SmallestSize - 1e-4, stands * Understorey.LargestSize + 1e-4), "its height, with its own size");
                Assert.That(tuft.AcrossM / tuft.HeightM, Is.EqualTo(Understorey.AcrossShare(TuftShape.Shrub)).Within(1e-5), "a bush spreads");
                Assert.That(tuft.Variant, Is.InRange(0, StandPreparation.Variants - 1));
                Assert.That(tuft.YawDeg, Is.InRange(0f, 359f));
            }
            Assert.That(shrubs, Is.GreaterThan(heathOthers), "shrubs are the greater part of the heath");
            Assert.That(grass, Is.Not.Empty, "the grass grows tussocks");
            int tussocks = 0, grassOthers = 0;
            foreach (UnderstoreyTuft tuft in grass)
            {
                if (tuft.Shape == TuftShape.Herb) continue;
                if (tuft.Shape == TuftShape.Tussock) tussocks++; else { grassOthers++; Assert.That(tuft.Shape, Is.EqualTo(TuftShape.Clump), "the grass's companion"); }
            }
            Assert.That(tussocks, Is.GreaterThan(grassOthers), "tussocks are the greater part of the grass");
            Assert.That(tussocks + grassOthers, Is.GreaterThan(2 * (shrubs + heathOthers)), "and thicker on the ground than the heath, as the covers say");
            Assert.That(sand, Is.Empty, "nothing grows on sand");
        }

        [Test]
        public void TheSameGroundGrowsTheSameTuftsEveryTime()
        {
            GameClient client = Joined(World());
            List<UnderstoreyTuft> once = At(client, East - 10.0, North, 6.0);
            List<UnderstoreyTuft> again = At(client, East - 10.0, North, 6.0);
            Assert.That(again.Count, Is.EqualTo(once.Count));
            for (int i = 0; i < once.Count; i++)
            {
                Assert.That(again[i].East, Is.EqualTo(once[i].East));
                Assert.That(again[i].North, Is.EqualTo(once[i].North));
                Assert.That(again[i].HeightM, Is.EqualTo(once[i].HeightM));
                Assert.That(again[i].Variant, Is.EqualTo(once[i].Variant));
            }
        }

        [Test]
        public void ATuftStandsOnTheGroundTheTileCarriesAndOutOfTheBark()
        {
            GameClient client = Joined(World());
            TileId id = client.Grid.ForPosition(East, North);
            ReceivedTile ground = client.Tiles.Holding(TileLayer.Ground, id);
            // The tree of its cell, where the layout stands it, and the bark it is drawn with at the ground.
            StandLayout.CellCentre(TreeRow, TreeCol, TestRasters.MadeCellM, TestRasters.MadeExtentM, out double cellEast, out double cellNorth);
            StandLayout.Place(TreeRow, TreeCol, StandLayout.Kind.Trunk, 0, (int)Math.Round(TestRasters.MadeCellM * 100.0),
                              out int eastCm, out int northCm, out _);
            double trunkEast = cellEast + eastCm / 100.0, trunkNorth = cellNorth + northCm / 100.0;
            double bark = StandForms.TrunkRadiusAt(StandForms.OldManBanksia, StandCodes.HeightOf(StandCodes.Pack(PlantSpecies.OldManBanksia, TreeHeightM)), 0.0);
            Assert.That(bark, Is.GreaterThan(0.3), "a stout little tree to keep clear of");

            List<UnderstoreyTuft> tufts = At(client, cellEast, cellNorth, 12.0);
            Assert.That(tufts, Is.Not.Empty);
            foreach (UnderstoreyTuft tuft in tufts)
            {
                double up = TileGround.HeightAt(ground, tuft.East, tuft.North) - Understorey.SinkM;
                Assert.That(tuft.Up, Is.EqualTo((float)up).Within(1e-3), "on the ground the tile carries, set a little into it");
                double dx = tuft.East - trunkEast, dz = tuft.North - trunkNorth;
                Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.GreaterThanOrEqualTo(bark), "nothing grows through the trunk");
            }
        }

        [Test]
        public void WetterGroundGrowsMoreAndTallerAndSomeCoversGrowNothing()
        {
            Assert.That(Understorey.PerSquareMetreIn(1.0, 3), Is.GreaterThan(Understorey.PerSquareMetreIn(1.0, 0)), "the wettest quarter is the thickest");
            Assert.That(Understorey.HeightIn(1.0, 3), Is.GreaterThan(Understorey.HeightIn(1.0, 0)), "and the tallest");
            Assert.That(Understorey.PerSquareMetreIn(1.0, 9), Is.EqualTo(Understorey.PerSquareMetreIn(1.0, GroundCovers.Quarters - 1)), "a quarter beyond the four is the last");
            Assert.That(Understorey.CountOn(7, 9, 4.0), Is.EqualTo(4), "a whole density is that many");
            Assert.That(Understorey.CountOn(7, 9, 4.0), Is.EqualTo(Understorey.CountOn(7, 9, 4.0)), "and the same cell answers the same every time");
            Assert.That(Understorey.CountOn(7, 9, 0.0), Is.Zero);
            Assert.That(Understorey.CountOn(7, 9, 1000.0), Is.EqualTo(Understorey.MostPerCell), "however thick a cover is, a cell holds only so many");

            foreach (GroundCover cover in new[] { GroundCover.Sea, GroundCover.FreshWater, GroundCover.Sand, GroundCover.DuneSand,
                                                  GroundCover.Rock, GroundCover.BareEarth, GroundCover.ForestFloor, GroundCover.Unknown })
                Assert.That(Understorey.Grows(cover, out _, out _, out _), Is.False, cover + " grows nothing");
            foreach (GroundCover cover in new[] { GroundCover.Heath, GroundCover.Bracken, GroundCover.Sedge, GroundCover.Grass, GroundCover.SwampFloor })
            {
                Assert.That(Understorey.Grows(cover, out TuftShape shape, out double perSquareM, out double heightM), Is.True, cover + " grows something");
                Assert.That(perSquareM, Is.GreaterThan(0.0));
                Assert.That(heightM, Is.InRange(0.3, 1.5), cover + " stands at a founder's knee or thereabouts");
                Assert.That(Understorey.AcrossShare(shape), Is.InRange(0.5, 1.5));
            }
        }
    }
}
