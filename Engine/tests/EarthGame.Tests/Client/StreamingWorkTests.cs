using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Client
{
    /// <summary>
    /// The work a streamed tile needs before a renderer is asked for anything, and the allowance the main thread
    /// spends on what is left (M1.4e promises 2, 3 and 7).
    /// </summary>
    public sealed class StreamingWorkTests
    {
        private const int Posts = 251;
        private const double CellM = 4.0;

        private static ReceivedTile Ground(TileId id, double originEast, double originNorth) => new ReceivedTile
        {
            Id = id,
            Layer = TileLayer.Ground,
            Posts = Posts,
            CellM = CellM,
            OriginEast = originEast,
            OriginNorth = originNorth,
            Crc32 = 0xABCDEF01,
            Heights = Law(),
        };

        private static float[,] Law()
        {
            float[,] h = new float[Posts, Posts];
            for (int z = 0; z < Posts; z++)
                for (int x = 0; x < Posts; x++) h[z, x] = 10f + z * 0.1f + x * 0.05f;
            return h;
        }

        private static ReceivedTile Cover(TileId id, byte code) => new ReceivedTile
        {
            Id = id,
            Layer = TileLayer.GroundCover,
            Posts = Posts,
            CellM = CellM,
            Crc32 = 0x1234,
            Codes = Codes(code),
        };

        private static byte[,] Codes(byte code)
        {
            byte[,] c = new byte[Posts, Posts];
            for (int z = 0; z < Posts; z++)
                for (int x = 0; x < Posts; x++) c[z, x] = code;
            return c;
        }

        /// <summary>
        /// The prepared posts are the ground the tile carries, at the pitch asked for, normalised over the tile's
        /// own range. Two posts for every post the wire sent is what M1.4e builds at; the law here is linear, so
        /// the corners say whether the square landed where it should.
        /// </summary>
        [Test]
        public void APreparedTileCarriesItsOwnGroundAtThePitchAskedFor()
        {
            ReceivedTile tile = Ground(new TileId(0, 0), -4000.0, -4000.0);
            PreparedTile prepared = TilePreparation.Prepare(tile, 513, null, GroundColourMap.Texels);
            Assert.That(prepared.Posts, Is.EqualTo(513));
            Assert.That(prepared.SizeM, Is.EqualTo(1000f).Within(0.001f), "250 cells of 4 m");
            Assert.That(prepared.Id, Is.EqualTo(tile.Id));
            Assert.That(prepared.GroundCrc, Is.EqualTo(tile.Crc32));
            Assert.That(prepared.ColourMap, Is.Null, "no cover was held");
            // The Terrain's base lies the room for a hole below its lowest post (BF.4), and its range reaches the highest.
            float lowest = prepared.BaseM + TilePreparation.DigRoomM, range = prepared.RangeM;
            Assert.That(lowest, Is.EqualTo(10f).Within(0.01f));
            Assert.That(prepared.BaseM + range, Is.EqualTo(10f + 250 * 0.1f + 250 * 0.05f).Within(0.05f));
            Assert.That(prepared.BaseM + prepared.Normalised[0, 0] * range, Is.EqualTo(10f).Within(0.01f));
            Assert.That(prepared.Normalised[512, 512], Is.EqualTo(1f).Within(1e-3f));
            // The middle of a linear ground is the middle of its heights, whatever the pitch.
            Assert.That(prepared.BaseM + prepared.Normalised[256, 256] * range, Is.EqualTo(10f + (250 * 0.1f + 250 * 0.05f) / 2f).Within(0.05f));
        }

        /// <summary>
        /// A flat tile has a range of something rather than of nothing: a normalised height is a division by it. Until BF.4 the
        /// range was held to a metre at least; since, it is never less than the room below the lowest post for a hole.
        /// </summary>
        [Test]
        public void AFlatTileIsNotADivisionByZero()
        {
            ReceivedTile tile = Ground(new TileId(0, 0), -4000.0, -4000.0);
            for (int z = 0; z < Posts; z++)
                for (int x = 0; x < Posts; x++) tile.Heights[z, x] = 7f;
            PreparedTile prepared = TilePreparation.Prepare(tile, 65, null, 32);
            Assert.That(prepared.RangeM, Is.EqualTo(TilePreparation.DigRoomM).Within(1e-5f));
            Assert.That(prepared.BaseM, Is.EqualTo(7f - TilePreparation.DigRoomM).Within(1e-5f));
            Assert.That(prepared.Normalised[10, 10], Is.EqualTo(1f).Within(1e-6f), "the flat ground at the top of its range, the hole's room below it");
        }

        /// <summary>The colour is prepared beside the ground, and only from the cover of the same tile.</summary>
        [Test]
        public void TheColourIsPreparedWithItAndOnlyItsOwn()
        {
            ReceivedTile tile = Ground(new TileId(2, 3), -4000.0, -4000.0);
            byte code = GroundCovers.Pack(GroundCover.Heath, 1);
            PreparedTile prepared = TilePreparation.Prepare(tile, 65, Cover(new TileId(2, 3), code), 16);
            Assert.That(prepared.ColourMap, Is.Not.Null);
            Assert.That(prepared.ColourMap.Length, Is.EqualTo(16 * 16 * GroundColourMap.BytesPerTexel));
            Assert.That(prepared.CoverCrc, Is.EqualTo(0x1234u));
            GroundColour want = GroundPalette.Of(code);
            Assert.That(prepared.ColourMap[0], Is.EqualTo((byte)(int)(want.R * 255f + 0.5f)));

            PreparedTile elsewhere = TilePreparation.Prepare(tile, 65, Cover(new TileId(4, 4), code), 16);
            Assert.That(elsewhere.ColourMap, Is.Null, "another tile's cover means nothing here");
            Assert.That(elsewhere.CoverCrc, Is.Zero);
        }

        [Test]
        public void OnlyAGroundTileIsPrepared()
        {
            ReceivedTile tile = Ground(new TileId(0, 0), -4000.0, -4000.0);
            ReceivedTile cover = Cover(new TileId(0, 0), 0);
            Assert.That(() => TilePreparation.Prepare(cover, 65, null, 16), Throws.ArgumentException);
            Assert.That(() => TilePreparation.Prepare(null, 65, null, 16), Throws.ArgumentNullException);
        }

        /// <summary>What the worker did is reported in milliseconds off a clock it is told, not one it reads.</summary>
        [Test]
        public void ThePreparationSaysWhatItCost()
        {
            ReceivedTile tile = Ground(new TileId(0, 0), -4000.0, -4000.0);
            double clock = 100.0;
            PreparedTile prepared = TilePreparation.Prepare(tile, 65, null, 16, () => clock += 3.0);
            Assert.That(prepared.WorkerMs, Is.EqualTo(3.0).Within(1e-9), "the difference between the two readings");
        }

        // ---- the budget ----

        [Test]
        public void WorkStopsWhenTheBudgetIsSpentAndTheRestWaitsForTheNextFrame()
        {
            double now = 0.0;
            FrameBudget budget = new FrameBudget(1.5, () => now);
            int done = 0;
            budget.BeginFrame();
            while (budget.HasTimeLeft && done < 10)
            {
                done++;
                now += 0.6;                       // three of these fit in 1.5 ms; the third overruns and is allowed to finish
            }
            Assert.That(done, Is.EqualTo(3), "started while there was time, and the third finished what it started");
            Assert.That(budget.EndFrame(), Is.EqualTo(1.8).Within(1e-9));
            Assert.That(budget.WorstFrameMs, Is.EqualTo(1.8).Within(1e-9));

            budget.BeginFrame();
            int more = 0;
            while (budget.HasTimeLeft && done + more < 10)
            {
                more++;
                now += 0.6;
            }
            Assert.That(more, Is.EqualTo(3), "the next frame carries on with what was left");
            budget.EndFrame();
            Assert.That(budget.FramesSpent, Is.EqualTo(2));
        }

        /// <summary>
        /// Asking is the decision, and the answer carries what had been spent when it was made — not what has
        /// been spent by the time the work starts running, which includes compiling the work on the way in.
        /// </summary>
        [Test]
        public void TheBudgetSaysWhatItDecidedOn()
        {
            double now = 0.0;
            FrameBudget budget = new FrameBudget(1.5, () => now);
            budget.BeginFrame();
            Assert.That(budget.TryStart(out double first), Is.True);
            Assert.That(first, Is.EqualTo(0.0).Within(1e-9));
            now += 1.0;
            Assert.That(budget.TryStart(out double second), Is.True);
            Assert.That(second, Is.EqualTo(1.0).Within(1e-9), "a second piece may start: a millisecond is inside 1.5");
            now += 1.0;
            Assert.That(budget.TryStart(out double third), Is.False);
            Assert.That(third, Is.EqualTo(2.0).Within(1e-9), "and it says what was spent when it said no");
        }

        [Test]
        public void OneItemLongerThanTheWholeBudgetIsStillReported()
        {
            double now = 0.0;
            FrameBudget budget = new FrameBudget(1.5, () => now);
            budget.BeginFrame();
            Assert.That(budget.HasTimeLeft, Is.True, "a frame always gets to start one piece of work");
            now += 40.0;
            Assert.That(budget.HasTimeLeft, Is.False);
            Assert.That(budget.EndFrame(), Is.EqualTo(40.0).Within(1e-9), "the frame says what it really spent");
            Assert.That(budget.WorstFrameMs, Is.EqualTo(40.0).Within(1e-9));
        }

        [Test]
        public void ABudgetIsSomeMillisecondsAndNeedsAClock()
        {
            Assert.That(() => new FrameBudget(0.0, () => 0.0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new FrameBudget(1.5, null), Throws.ArgumentNullException);
        }

        /// <summary>
        /// The ground a tile is built from is the ground it was built from before M1.4e, post for post, except
        /// at the far edge where the old path asked a neighbour the client does not hold and got NaN. Nothing
        /// moved: the frames of a slice that only changed when and where the work happens cannot show that,
        /// because the world's clock advances between recordings and the sun with it.
        /// </summary>
        [Test]
        public void ThePreparedGroundIsTheGroundTheFieldWouldHaveGiven()
        {
            ReceivedTile tile = Ground(new TileId(0, 0), -4000.0, -4000.0);
            TileHeightfield field = new TileHeightfield(new TileGrid(8000.0));
            field.Add(tile);
            const int posts = 513;
            PreparedTile prepared = TilePreparation.Prepare(tile, posts, null, GroundColourMap.Texels);
            double spacing = (tile.Posts - 1) * tile.CellM / (double)(posts - 1);
            int compared = 0, edge = 0;
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++)
                {
                    double asked = field.HeightAt(tile.OriginEast + x * spacing, tile.OriginNorth + z * spacing);
                    if (double.IsNaN(asked))
                    {
                        edge++;
                        continue;
                    }
                    float mine = prepared.Normalised[z, x] * prepared.RangeM + prepared.BaseM;
                    Assert.That(mine, Is.EqualTo((float)asked).Within(0.01f), "post " + z + "," + x);
                    compared++;
                }
            Assert.That(compared, Is.EqualTo(posts * posts - edge));
            Assert.That(edge, Is.EqualTo(posts * 2 - 1), "the far row and the far column, which the field could not answer");
        }

        /// <summary>
        /// The tile is prepared from its own posts, so its far edge is its own last post rather than the first
        /// post of a neighbour the client may not hold. Through the field, that edge read NaN.
        /// </summary>
        [Test]
        public void TheFarEdgeIsTheTilesOwnLastPostAndNeverNothing()
        {
            ReceivedTile tile = Ground(new TileId(0, 0), -4000.0, -4000.0);
            TileHeightfield field = new TileHeightfield(new TileGrid(8000.0));
            field.Add(tile);
            Assert.That(field.HeightAt(-3000.0, -3000.0), Is.NaN, "the field hands the far corner to a tile it does not hold");
            PreparedTile prepared = TilePreparation.Prepare(tile, 513, null, GroundColourMap.Texels);
            for (int i = 0; i < prepared.Posts; i++)
            {
                Assert.That(float.IsNaN(prepared.Normalised[prepared.Posts - 1, i]), Is.False, "the northern edge at " + i);
                Assert.That(float.IsNaN(prepared.Normalised[i, prepared.Posts - 1]), Is.False, "the eastern edge at " + i);
            }
            Assert.That(prepared.BaseM + prepared.RangeM, Is.EqualTo(47.5f).Within(0.01f), "the tile's own highest post");
        }
    }
}
