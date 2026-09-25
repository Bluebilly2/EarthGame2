using System;
using System.Collections.Generic;
using System.Linq;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// The loading screen's map (2026-09-25): it is handed the making's layers before the first step, and each array it paints
    /// is whole by the report it waits for, held here against a real making of the made coast.
    /// </summary>
    public sealed class MakingMapTests
    {
        /// <summary>A watcher that hands the map what it is handed, and keeps the layers for the test's own reading.</summary>
        private sealed class Tee : IMakingWatcher
        {
            public readonly MakingMap Map = new MakingMap(64);
            public WorldLayers Layers;
            public void Began(WorldLayers layers)
            {
                Layers = layers;
                Map.Began(layers);
            }
            public void WakeChosen(double east, double north) => Map.WakeChosen(east, north);
        }

        private sealed class Making
        {
            public readonly Tee Tee = new Tee();
            public readonly List<int> LevelAtReport = new List<int>();
            public byte[] WaterAtItsReport, CanopyAtItsReport, CoverAtItsReport;
            public WorldLayers Done;
        }

        private static Making _made;

        /// <summary>The made coast made once, the map told every report, each array copied as it stood when its report came.</summary>
        private static Making Made()
        {
            if (_made != null) return _made;
            var made = new Making();
            made.Done = WorldLayers.Compute(TestRasters.MadeCoast(), 1347UL, null, stage =>
            {
                made.Tee.Map.Reported(stage);
                made.LevelAtReport.Add(made.Tee.Map.Level);
                if (stage == MakingMap.WaterEndsAt) made.WaterAtItsReport = made.Tee.Layers.Water.ToArray();
                if (stage == MakingMap.CanopyEndsAt) made.CanopyAtItsReport = made.Tee.Layers.Overstory.ToArray();
                if (stage == MakingMap.CoverEndsAt) made.CoverAtItsReport = made.Tee.Layers.Cover.ToArray();
            }, null, made.Tee);
            return _made = made;
        }

        [Test]
        public void TheMapHasTheLandBeforeTheFirstStepAndEachLayerAfterItsOwn()
        {
            Making made = Made();
            Assert.That(made.Tee.Layers, Is.SameAs(made.Done), "the watcher is handed the making's own layers");
            Assert.That(made.LevelAtReport[0], Is.EqualTo(MakingMap.Land), "the land is there at the first report");
            Assert.That(made.LevelAtReport, Is.Ordered, "what the map can show only rises");
            Assert.That(made.Tee.Map.Level, Is.EqualTo(MakingMap.Cover));
            Assert.That(made.WaterAtItsReport, Is.EqualTo(made.Done.Water), "the water's classes were whole when its report came");
            Assert.That(made.CanopyAtItsReport, Is.EqualTo(made.Done.Overstory), "the canopy was whole when its report came");
            Assert.That(made.CoverAtItsReport, Is.EqualTo(made.Done.Cover), "the cover was whole when its report came");
        }

        [Test]
        public void TheSeaIsPaintedSeaAndTheLandIsNot()
        {
            Making made = Made();
            MakingMap map = made.Tee.Map;
            byte[] land = map.Paint(MakingMap.Land), cover = map.Paint(MakingMap.Cover);
            Assert.That(land.Length, Is.EqualTo(map.Size * map.Size * 4));
            Assert.That(map.Paint(MakingMap.Nothing), Is.Null);
            WorldLayers w = made.Done;
            double half = (w.Width - 1) * w.CellM / 2.0, step = 2.0 * half / map.Size;
            int seaPixels = 0, landPixels = 0;
            for (int y = 0; y < map.Size; y++)
            {
                for (int x = 0; x < map.Size; x++)
                {
                    // Read by hand: the post at the pixel's centre, row 0 of the picture at the south.
                    double east = -half + (x + 0.5) * step, north = -half + (y + 0.5) * step;
                    int col = (int)Math.Round((east + half) / w.CellM), row = (int)Math.Round((half - north) / w.CellM);
                    var wet = (WaterClass)w.Water[row * w.Width + col];
                    int o = (y * map.Size + x) * 4;
                    bool blue = cover[o + 2] > cover[o] && cover[o + 2] > cover[o + 1];
                    if (wet == WaterClass.Sea) { Assert.That(blue, "a sea pixel is blue"); seaPixels++; }
                    if (wet == WaterClass.Dry && w.Heights[row, col] > 5f)
                    {
                        bool anyWet = false;
                        int reach = (int)Math.Floor(step / w.CellM / 2.0);
                        for (int r = Math.Max(0, row - reach); r <= Math.Min(w.Height - 1, row + reach); r++)
                            for (int c = Math.Max(0, col - reach); c <= Math.Min(w.Width - 1, col + reach); c++)
                                anyWet |= w.Water[r * w.Width + c] >= (byte)WaterClass.Creek && w.Water[r * w.Width + c] != (byte)WaterClass.Swamp;
                        // Any watercourse at all counts as wet here, drawn or not: the row asks only that dry land is not blue.
                        if (!anyWet) { Assert.That(blue, Is.False, "dry land well above the sea is not blue"); landPixels++; }
                    }
                }
            }
            Assert.That(seaPixels, Is.GreaterThan(0), "the made coast has sea to paint");
            Assert.That(landPixels, Is.GreaterThan(0), "and land");
        }

        [Test]
        public void AWatercourseIsDrawnOnlyWhereItDrainsEnoughOfTheLandForTheScale()
        {
            // Read by hand from the made coast: every pixel painted as a river holds a creek or a stream cell whose catchment is
            // at least the rule's area for the picture's pixel, and there is at least one.
            WorldLayers w = Made().Done;
            var map = new MakingMap(128);
            map.Began(w);
            map.Reported(MakingMap.WaterEndsAt);
            byte[] rgba = map.Paint(MakingMap.Water);
            double half = (w.Width - 1) * w.CellM / 2.0, step = 2.0 * half / map.Size, riverM2 = MakingMap.RiverDrainsPixels * step * step;
            int reach = (int)Math.Floor(step / w.CellM / 2.0), drawn = 0;
            for (int y = 0; y < map.Size; y++)
            {
                for (int x = 0; x < map.Size; x++)
                {
                    int o = (y * map.Size + x) * 4;
                    if (!(rgba[o] == 84 && rgba[o + 1] == 148 && rgba[o + 2] == 199)) continue;
                    double east = -half + (x + 0.5) * step, north = -half + (y + 0.5) * step;
                    int col = (int)Math.Round((east + half) / w.CellM), row = (int)Math.Round((half - north) / w.CellM);
                    bool bigEnough = false;
                    for (int r = Math.Max(0, row - reach); r <= Math.Min(w.Height - 1, row + reach); r++)
                        for (int c = Math.Max(0, col - reach); c <= Math.Min(w.Width - 1, col + reach); c++)
                        {
                            var wet = (WaterClass)w.Water[r * w.Width + c];
                            bigEnough |= (wet == WaterClass.Creek || wet == WaterClass.Stream) && w.Drainage.CatchmentM2(c, r) >= riverM2;
                        }
                    Assert.That(bigEnough, "a river pixel drains at least " + riverM2 + " m2");
                    drawn++;
                }
            }
            Assert.That(drawn, Is.GreaterThan(0), "the made coast has a watercourse big enough to draw at this scale");
        }

        [Test]
        public void TheWakeIsPlacedOnThePicture()
        {
            var map = new MakingMap(32);
            Assert.That(map.TryWake(out _, out _), Is.False, "nothing before the making");
            WorldLayers w = Made().Done;
            map.Began(w);
            Assert.That(map.TryWake(out _, out _), Is.False, "nothing before the wake is chosen");
            double half = (w.Width - 1) * w.CellM / 2.0;
            map.WakeChosen(half / 2.0, -half / 2.0);
            Assert.That(map.TryWake(out float fromWest, out float fromSouth), Is.True);
            Assert.That(fromWest, Is.EqualTo(0.75f).Within(1e-6f), "a quarter of the width east of the centre");
            Assert.That(fromSouth, Is.EqualTo(0.25f).Within(1e-6f), "a quarter of it south");
        }
    }
}
