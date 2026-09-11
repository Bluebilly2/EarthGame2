using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// What stands and lies on the ground (M1.6a promise 7). The trees are asked of a made forest at the real 4 m cell,
    /// a plane that drains west with no sea, where the canopy alone decides where a trunk may stand; the sticks and the
    /// cobbles of the made coast, whose beach, dune, cliff, lake and creek are where the fixture put them; and the codes
    /// and the layout that carry them, of values made here.
    /// </summary>
    public sealed class StandTests
    {
        private const int ForestSide = 201;
        private const double ForestCellM = 4.0;

        private static WorldLayers _forest;
        private static WorldLayers _coast;

        /// <summary>A plane rising two in a hundred to the east, 800 m a side at 4 m: wet in the west, dry in the east, no sea.</summary>
        private static WorldLayers Forest() => _forest ?? (_forest = WorldLayers.Compute(
            TestRasters.FromLaw(ForestSide, ForestCellM, (ForestSide - 1) * ForestCellM, "made_forest", (row, col) => (float)(20.0 + 0.08 * col)), 1347UL));

        private static WorldLayers Coast() => _coast ?? (_coast = WorldLayers.Compute(TestRasters.MadeCoast(), 1347UL));

        private static List<(int Row, int Col, PlantSpecies Species, double HeightM, double RadiusM)> Trunks(WorldLayers w)
        {
            var trunks = new List<(int, int, PlantSpecies, double, double)>();
            for (int r = 0; r < w.Height; r++)
                for (int c = 0; c < w.Width; c++)
                {
                    byte code = w.Stand[r * w.Width + c];
                    if (code == 0) continue;
                    PlantSpecies species = StandCodes.SpeciesOf(code);
                    double height = StandCodes.HeightOf(code);
                    trunks.Add((r, c, species, height, 0.5 * species.CrownShare * height));
                }
            return trunks;
        }

        [Test]
        public void TrunksStandOnlyUnderACanopyOfTheirOwnSpecies()
        {
            foreach (WorldLayers w in new[] { Forest(), Coast() })
            {
                int trunks = 0;
                for (int i = 0; i < w.Width * w.Height; i++)
                {
                    if (w.Stand[i] == 0) continue;
                    trunks++;
                    Assert.That(w.Overstory[i], Is.Not.EqualTo((byte)0), "a trunk at cell " + i + " where no canopy stands");
                    Assert.That(StandCodes.SpeciesOf(w.Stand[i]), Is.SameAs(PlantSpecies.All[w.Overstory[i] - 1]), "the trunk at cell " + i + " is its canopy's species");
                }
                Assert.That(trunks, Is.GreaterThan(100), "trees stand at all: " + trunks);
            }
        }

        [Test]
        public void NoTwoTrunksStandNearerThanTheirCrownsAllow()
        {
            WorldLayers w = Forest();
            var byCell = new Dictionary<int, double>();
            foreach (var t in Trunks(w)) byCell[t.Row * w.Width + t.Col] = t.RadiusM;
            double largest = 0.0;
            foreach (PlantSpecies tall in StandCodes.Tall) largest = Math.Max(largest, 0.5 * tall.CrownShare * tall.MaxHeightM);
            int reach = (int)Math.Ceiling((1.0 - WorldLayers.CrownOverlap) * 2.0 * largest / w.CellM);
            foreach (var t in Trunks(w))
                for (int dr = -reach; dr <= reach; dr++)
                    for (int dc = -reach; dc <= reach; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        if (!byCell.TryGetValue((t.Row + dr) * w.Width + t.Col + dc, out double other)) continue;
                        double d = w.CellM * Math.Sqrt(dr * dr + dc * dc);
                        Assert.That(d, Is.GreaterThanOrEqualTo((1.0 - WorldLayers.CrownOverlap) * (t.RadiusM + other) - 1e-9),
                            "two trunks " + d.ToString("0.0") + " m apart at row " + t.Row + " col " + t.Col);
                    }
        }

        /// <summary>
        /// The crowns cover about the ground the canopy layer says is covered: most of the canopied cells lie under a
        /// crown, and the stems a hectare are a stand's, neither a lawn of saplings nor a park of a few giants.
        /// </summary>
        [Test]
        public void TheCrownsCoverAboutWhatTheCanopyCovers()
        {
            WorldLayers w = Forest();
            var trunks = Trunks(w);
            bool[] under = new bool[w.Width * w.Height];
            foreach (var t in trunks)
            {
                int reach = (int)Math.Ceiling(t.RadiusM / w.CellM);
                for (int dr = -reach; dr <= reach; dr++)
                    for (int dc = -reach; dc <= reach; dc++)
                    {
                        int r = t.Row + dr, c = t.Col + dc;
                        if (r < 0 || c < 0 || r >= w.Height || c >= w.Width) continue;
                        if (w.CellM * Math.Sqrt(dr * dr + dc * dc) <= t.RadiusM) under[r * w.Width + c] = true;
                    }
            }
            int canopied = 0, covered = 0;
            for (int i = 0; i < under.Length; i++)
            {
                if (w.Overstory[i] == 0) continue;
                canopied++;
                if (under[i]) covered++;
            }
            double share = covered / (double)canopied;
            double stemsPerHa = trunks.Count / (canopied * w.CellM * w.CellM / 10000.0);
            Assert.That(canopied, Is.GreaterThan(1000), "the made forest carries a canopy");
            Assert.That(share, Is.InRange(0.5, 0.95), "the share of the canopy's cells under a crown: " + share.ToString("0.00"));
            Assert.That(stemsPerHa, Is.InRange(50.0, 1500.0), "stems a hectare of canopy: " + stemsPerHa.ToString("0"));
        }

        [Test]
        public void EachTreeIsAsTallAsItsSpeciesGrows()
        {
            foreach (var t in Trunks(Forest()))
            {
                Assert.That(t.HeightM, Is.GreaterThanOrEqualTo(t.Species.MinHeightM - StandCodes.HeightStepM), t.Species.DisplayName);
                Assert.That(t.HeightM, Is.LessThanOrEqualTo(t.Species.MaxHeightM + StandCodes.HeightStepM), t.Species.DisplayName);
            }
        }

        /// <summary>A stick lies under a crown; a cobble where stone lies loose, and never on a dune or under water.</summary>
        [Test]
        public void SticksLieUnderTreesAndCobblesWhereStoneLiesLoose()
        {
            WorldLayers w = Coast();
            var trunks = Trunks(w);
            int sticks = 0, cobbles = 0, onCliffOrCreek = 0;
            for (int r = 0; r < w.Height; r++)
                for (int c = 0; c < w.Width; c++)
                {
                    int i = r * w.Width + c;
                    byte code = w.Loose[i];
                    var water = (WaterClass)w.Water[i];
                    if (LooseCodes.SticksOf(code) > 0)
                    {
                        sticks += LooseCodes.SticksOf(code);
                        bool nearTree = false;
                        foreach (var t in trunks)
                        {
                            double d = w.CellM * Math.Sqrt((t.Row - r) * (t.Row - r) + (t.Col - c) * (t.Col - c));
                            if (d <= t.RadiusM + w.CellM) { nearTree = true; break; }
                        }
                        Assert.That(nearTree, Is.True, "sticks at row " + r + " col " + c + " lie under no crown");
                    }
                    int here = LooseCodes.CobblesOf(code);
                    if (here == 0) continue;
                    cobbles += here;
                    Assert.That(water, Is.Not.EqualTo(WaterClass.Sea).And.Not.EqualTo(WaterClass.Lake).And.Not.EqualTo(WaterClass.Swamp), "a cobble under water at row " + r + " col " + c);
                    Assert.That(w.Has(r, c, Topology.Dune), Is.False, "a cobble on the dune at row " + r + " col " + c);
                    bool loose = w.Has(r, c, Topology.ShorePlatform) || w.Has(r, c, Topology.Cliff) || w.Has(r, c, Topology.Beach)
                                 || water == WaterClass.Creek || water == WaterClass.Stream || w.Soil.DepthM[i] < WorldLayers.ThinSoilM;
                    Assert.That(loose, Is.True, "a cobble on deep soil at row " + r + " col " + c);
                    if (w.Has(r, c, Topology.Cliff) || water == WaterClass.Creek || water == WaterClass.Stream) onCliffOrCreek++;
                }
            Assert.That(sticks, Is.GreaterThan(100), "sticks lie under the made coast's trees: " + sticks);
            Assert.That(cobbles, Is.GreaterThan(0), "and cobbles somewhere");
            Assert.That(onCliffOrCreek, Is.GreaterThan(0), "on the hill's cliff or in the swale's creek");
        }

        [Test]
        public void TheLayoutIsWholeNumbersInsideTheCellAndTheSameEveryTime()
        {
            var seen = new HashSet<(int, int)>();
            for (int k = 0; k < 16; k++)
            {
                StandLayout.Place(1234, 567, StandLayout.Kind.Stick, k, 400, out int east, out int north, out int yaw);
                StandLayout.Place(1234, 567, StandLayout.Kind.Stick, k, 400, out int east2, out int north2, out int yaw2);
                Assert.That((east2, north2, yaw2), Is.EqualTo((east, north, yaw)), "the same thing in the same place on every call");
                Assert.That(east, Is.InRange(-200, 199));
                Assert.That(north, Is.InRange(-200, 199));
                Assert.That(yaw, Is.InRange(0, 359));
                seen.Add((east, north));
            }
            Assert.That(seen.Count, Is.GreaterThan(12), "the sticks of one cell lie in different places");
            StandLayout.Place(1234, 567, StandLayout.Kind.Trunk, 0, 400, out int te, out int tn, out _);
            StandLayout.Place(1234, 567, StandLayout.Kind.Cobble, 0, 400, out int ce, out int cn, out _);
            Assert.That((te, tn), Is.Not.EqualTo((ce, cn)), "the kind is part of where a thing lies");
        }

        [Test]
        public void TheCodesGoInAndComeOut()
        {
            foreach (PlantSpecies tall in StandCodes.Tall)
                for (double h = StandCodes.HeightStepM; h <= StandCodes.HeightMask * StandCodes.HeightStepM; h += StandCodes.HeightStepM)
                {
                    byte code = StandCodes.Pack(tall, h);
                    Assert.That(StandCodes.SpeciesOf(code), Is.SameAs(tall));
                    Assert.That(StandCodes.HeightOf(code), Is.EqualTo(h).Within(1e-9));
                }
            Assert.That(StandCodes.Tall.Count, Is.EqualTo(5), "the five tall plants of Bherwerre");
            Assert.That(StandCodes.SpeciesOf(0), Is.Null);
            Assert.That(StandCodes.HeightOf(0), Is.EqualTo(0.0));
            Assert.That(StandCodes.HeightOf(StandCodes.Pack(PlantSpecies.Blackbutt, 100.0)), Is.EqualTo(StandCodes.HeightMask * StandCodes.HeightStepM), "a height past the code's reach is carried at its top");
            Assert.Throws<ArgumentException>(() => StandCodes.Pack(PlantSpecies.Lomandra, 1.0), "a herb does not stand as a tree");
            foreach (PlantSpecies tall in StandCodes.Tall) Assert.That(StandCodes.Legend(), Does.Contain(tall.Name));
            byte loose = LooseCodes.Pack(7, 3);
            Assert.That(LooseCodes.SticksOf(loose), Is.EqualTo(7));
            Assert.That(LooseCodes.CobblesOf(loose), Is.EqualTo(3));
            Assert.That(LooseCodes.SticksOf(LooseCodes.Pack(40, 0)), Is.EqualTo(LooseCodes.MaxEach), "more than a code counts is carried at its top");
            Assert.That(LooseCodes.CobblesOf(LooseCodes.Pack(0, -2)), Is.Zero);
        }

        /// <summary>A tile's posts land on their raster cells by the one rule the tile's writer uses.</summary>
        [Test]
        public void ThePostsOfATileLandOnTheirCells()
        {
            TileGrid grid = new TileGrid(8000.0);
            grid.Origin(new TileId(3, 5), out double east, out double north);
            for (int z = 0; z <= 250; z += 50)
                for (int x = 0; x <= 250; x += 50)
                {
                    TileCodec.CellOf(8000.0, 4.0, east + x * 4.0, north + z * 4.0, out int row, out int col);
                    Assert.That(col, Is.EqualTo(3 * 250 + x));
                    Assert.That(row, Is.EqualTo(2000 - (5 * 250 + z)));
                }
        }
    }
}
