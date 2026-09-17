using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>A scripted founder leaves the route for water when thirsty, drinks until the word is gone, and finds only fresh water that stands (the corpus, 2026-09-16).</summary>
    public sealed class DrinkingTests
    {
        [Test]
        public void AFounderToldThirstyWantsWaterAndOneToldFineDoesNot()
        {
            Assert.That(Drinking.Wants(1.0), Is.False, "a full body");
            Assert.That(Drinking.Wants(1.0 - Hydration.ThirstyAt + 1e-9), Is.False, "a hair short of the word");
            Assert.That(Drinking.Wants(1.0 - Hydration.ThirstyAt), Is.True, "thirsty");
            Assert.That(Drinking.Wants(1.0 - Hydration.CollapsingAt), Is.True, "collapsing");
        }

        [Test]
        public void OneVisitFillsABodyAtTheLethalEdgeAndStopsWhenTheWordIsGone()
        {
            Hydration body = new Hydration();
            body.Restore(1.0 - Hydration.LethalWaterLoss + 1e-6);
            int presses = 0;
            VerbOutcome last = VerbOutcome.Done;
            do
            {
                body.Drink(Hydration.MaxDrinkPerVisitL);
                presses++;
            } while (Drinking.PressAgain(last, body.Water01, presses));
            Assert.That(body.Thirst, Is.EqualTo(ThirstLevel.Fine), "the word is gone");
            Assert.That(presses, Is.LessThanOrEqualTo(Drinking.MostPresses));
            // The most presses is the fewest whole drinks that fill a body from the lethal edge, and one over.
            int fewest = (int)Math.Ceiling(Hydration.LethalWaterLoss * Hydration.TotalBodyWaterL / Hydration.MaxDrinkPerVisitL);
            Assert.That(Drinking.MostPresses, Is.EqualTo(fewest + 1));
            Assert.That(presses, Is.EqualTo(fewest - 1).Or.EqualTo(fewest), "the word goes before the body is quite full");
        }

        [Test]
        public void ARefusalOrAFullBodyEndsTheVisit()
        {
            Assert.That(Drinking.PressAgain(VerbOutcome.Done, 0.95, 1), Is.True, "drank, still thirsty, presses left");
            Assert.That(Drinking.PressAgain(VerbOutcome.Salt, 0.95, 1), Is.False, "the sea refused");
            Assert.That(Drinking.PressAgain(VerbOutcome.NoWater, 0.95, 1), Is.False, "nothing there");
            Assert.That(Drinking.PressAgain(VerbOutcome.OutOfReach, 0.95, 1), Is.False, "out of reach");
            Assert.That(Drinking.PressAgain(VerbOutcome.Done, 1.0, 1), Is.False, "full");
            Assert.That(Drinking.PressAgain(VerbOutcome.Done, 0.95, Drinking.MostPresses), Is.False, "the visit's presses spent");
        }

        private const int Posts = 21;
        private const double CellM = 4.0;
        private const double Origin = -80.0;

        /// <summary>
        /// One 80 m tile of each layer, east −80 to 0 and north −80 to 0: water half a metre deep from post 8 (east −48)
        /// eastward, fresh water on posts 8 and 9 and the sea from post 10 (east −40) to the tile's edge, dry sand to the west.
        /// </summary>
        private static Dictionary<TileLayer, ReceivedTile> Tiles()
        {
            TileId id = new TileId(0, 0);
            float[,] depth = new float[Posts, Posts];
            byte[,] cover = new byte[Posts, Posts];
            for (int z = 0; z < Posts; z++)
                for (int x = 0; x < Posts; x++)
                {
                    depth[z, x] = x >= 8 ? 0.5f : 0f;
                    cover[z, x] = (byte)(x >= 10 ? GroundCover.Sea : x >= 8 ? GroundCover.FreshWater : GroundCover.Sand);
                }
            return new Dictionary<TileLayer, ReceivedTile>
            {
                [TileLayer.WaterDepth] = new ReceivedTile { Id = id, Layer = TileLayer.WaterDepth, Posts = Posts, CellM = CellM, OriginEast = Origin, OriginNorth = Origin, Heights = depth },
                [TileLayer.GroundCover] = new ReceivedTile { Id = id, Layer = TileLayer.GroundCover, Posts = Posts, CellM = CellM, OriginEast = Origin, OriginNorth = Origin, Codes = cover },
            };
        }

        private static Func<TileLayer, TileId, ReceivedTile> Holding(Dictionary<TileLayer, ReceivedTile> tiles)
            => (layer, id) => id.Ix == 0 && id.Iz == 0 && tiles.TryGetValue(layer, out ReceivedTile tile) ? tile : null;

        [Test]
        public void TheNearestStandingFreshWaterIsFoundAndTheSeaAndDryGroundAreNot()
        {
            TileGrid grid = new TileGrid(160.0, 80.0);
            Dictionary<TileLayer, ReceivedTile> tiles = Tiles();
            Func<TileLayer, TileId, ReceivedTile> holding = Holding(tiles);

            Assert.That(Drinking.StandsFresh(holding, grid, -8.0, -8.0), Is.False, "the sea is deep and not fresh");
            Assert.That(Drinking.FreshWaterNear(holding, grid, -8.0, -8.0, Drinking.SearchM, out _, out _), Is.False, "standing in the sea, the nearest fresh post is 36 m off: the sea under the founder is never offered");
            Assert.That(Drinking.FreshWaterNear(holding, grid, -76.0, -76.0, Drinking.SearchM, out _, out _), Is.False, "dry sand all round, the fresh posts 28 m off");

            Assert.That(Drinking.FreshWaterNear(holding, grid, -60.0, -20.0, Drinking.SearchM, out double east, out double north), Is.True, "from the sand, the fresh water's edge lies about 9 m east");
            Assert.That(east, Is.GreaterThan(-52.0).And.LessThan(-44.0), "on the fresh posts, where the depth between posts has reached 2 cm and the nearest post is fresh");
            Assert.That(Math.Abs(north + 20.0), Is.LessThan(2.0), "straight east of the founder");
            Assert.That(Drinking.StandsFresh(holding, grid, east, north), Is.True);

            tiles.Remove(TileLayer.GroundCover);
            Assert.That(Drinking.FreshWaterNear(holding, grid, -60.0, -20.0, Drinking.SearchM, out _, out _), Is.False, "without the cover held, water of unknown kind is not drunk");
        }

        [Test]
        public void TheWakeLoopPassesAKangarooMobAndAnOystercatcherPair()
        {
            // The corpus's soak startles animals only if the loop passes within a kind's flight distance of a group's place
            // (DEBTS, "The corpus's loop startles no animal"); lay_loop.py --pass lays the waypoints and labels them.
            List<string> segments = new List<string>();
            foreach (Waypoint w in Routes.WakeLoop()) segments.Add(w.Segment);
            Assert.That(segments, Does.Contain("mob"), "a pass by a kangaroo mob's place");
            Assert.That(segments, Does.Contain("pair"), "a pass by an oystercatcher pair's place");
        }
    }
}
