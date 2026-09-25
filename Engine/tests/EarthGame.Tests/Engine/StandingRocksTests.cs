using System;
using System.Collections.Generic;
using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Server;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// The ground world of BF.4 (<see cref="FineWorld"/>) with stone, loose stone and trees on it, for the rocks that stand
    /// (stage three): five stones in 100 m blocks, two to five cobbles on every rock cell and none to two on the rest, a
    /// banksia on some cells of the growing covers.
    /// </summary>
    internal static class RockWorld
    {
        private static readonly StoneType[] Stones = { StoneType.Sandstone, StoneType.Basalt, StoneType.Granite, StoneType.Shale, StoneType.Quartzite };

        private static GroundCover CoverOf(int row, int col) => GroundCovers.CoverOf((byte)FineWorld.CoverAt(row, col));

        public static uint StoneAt(int row, int col)
        {
            GroundCover cover = CoverOf(row, col);
            if (cover == GroundCover.Sea || cover == GroundCover.FreshWater || cover == GroundCover.SwampFloor) return 0u;
            StoneType s = Stones[(row / 25 + col / 25) % Stones.Length];
            for (int i = 0; i < StoneType.All.Count; i++)
                if (ReferenceEquals(StoneType.All[i], s)) return (uint)(i + 1);
            return 0u;
        }

        public static uint LooseAt(int row, int col)
        {
            int sticks = (row * 7 + col * 3) % 4 == 0 ? 2 : 0;
            switch (CoverOf(row, col))
            {
                case GroundCover.Sea:
                case GroundCover.FreshWater:
                case GroundCover.SwampFloor: return 0u;
                case GroundCover.Rock: return LooseCodes.Pack(sticks, 2 + (row + col) % 4);
                case GroundCover.Sand:
                case GroundCover.DuneSand: return LooseCodes.Pack(sticks, 0);
                default: return LooseCodes.Pack(sticks, (row * 5 + col) % 3);
            }
        }

        public static uint StandAt(int row, int col)
        {
            GroundCover cover = CoverOf(row, col);
            bool grows = cover == GroundCover.ForestFloor || cover == GroundCover.Heath || cover == GroundCover.Grass || cover == GroundCover.Bracken;
            return grows && row % 9 == 0 && col % 7 == 0 ? StandCodes.Pack(PlantSpecies.OldManBanksia, 11.0) : 0u;
        }

        public static WorldState Make()
        {
            int side = FineWorld.Side;
            double cell = FineWorld.CellM, extent = FineWorld.ExtentM;
            return FineWorld.Make(
                stand: TestRasters.FromCodes(side, cell, extent, "rock_stand", "stand", StandAt, null),
                loose: TestRasters.FromCodes(side, cell, extent, "rock_loose", "loose", LooseAt, null),
                stone: TestRasters.FromCodes(side, cell, extent, "rock_stone", "stone", StoneAt, null));
        }

        /// <summary>Every rock of the world by the server's rule.</summary>
        public static List<StandingRock> All(WorldState world)
        {
            List<StandingRock> all = new List<StandingRock>();
            for (int row = 0; row < FineWorld.Side; row++)
                for (int col = 0; col < FineWorld.Side; col++)
                    if (StandingRocks.TryOfCell(world, row, col, out StandingRock rock)) all.Add(rock);
            return all;
        }

        /// <summary>Every tile a client could hold of the world: its ground and cover, and its stand, loose layer and stone.</summary>
        public static Dictionary<(TileLayer, TileId), ReceivedTile> AllTiles(WorldState world)
        {
            Dictionary<(TileLayer, TileId), ReceivedTile> held = FineWorld.AllTiles(world);
            TileGrid grid = FineWorld.Grid();
            for (int iz = 0; iz < grid.TilesPerSide; iz++)
                for (int ix = 0; ix < grid.TilesPerSide; ix++)
                {
                    TileId id = new TileId(ix, iz);
                    held[(TileLayer.Stand, id)] = FineWorld.Tile(world, TileLayer.Stand, id);
                    held[(TileLayer.Loose, id)] = FineWorld.Tile(world, TileLayer.Loose, id);
                    held[(TileLayer.Stone, id)] = FineWorld.Tile(world, TileLayer.Stone, id);
                }
            return held;
        }

        /// <summary>The slope of the raster alone across a cell, degrees, from its posts 2 m off each way: a reading the rule does not make, the relief left out.</summary>
        public static double RasterSlopeDeg(WorldState world, int row, int col)
        {
            StandLayout.CellCentre(row, col, FineWorld.CellM, FineWorld.ExtentM, out double east, out double north);
            double ge = (world.RasterGroundAt(east + 2.0, north) - world.RasterGroundAt(east - 2.0, north)) / 4.0;
            double gn = (world.RasterGroundAt(east, north + 2.0) - world.RasterGroundAt(east, north - 2.0)) / 4.0;
            return Math.Atan(Math.Sqrt(ge * ge + gn * gn)) * 180.0 / Math.PI;
        }
    }

    /// <summary>
    /// Rock that stands (BF.4 stage three, promise 5 as amended 2026-09-25): where the rule puts boulders and ledges, the same
    /// on the server and on a client from its tiles, each inside its cell and seated on the ground; the surface a foot or a
    /// thing meets over a rock and beside it; things let go over a boulder resting on it; litter moved to a rock's foot alike on
    /// both sides; a founder on a boulder standing, not corrected; and a dig beside a boulder leaving it where it was.
    /// </summary>
    public sealed class StandingRocksTests
    {
        private static WorldState _shared;
        private static List<StandingRock> _rocks;

        /// <summary>A world no test changes, and its rocks, made once.</summary>
        private static WorldState Shared => _shared ?? (_shared = RockWorld.Make());
        private static List<StandingRock> Rocks => _rocks ?? (_rocks = RockWorld.All(Shared));

        private static bool Grows(GroundCover cover) =>
            cover == GroundCover.BareEarth || cover == GroundCover.Heath || cover == GroundCover.Bracken || cover == GroundCover.Sedge
            || cover == GroundCover.Grass || cover == GroundCover.ForestFloor;

        [Test]
        public void RocksStandWhereTheRuleSaysAndNowhereElse()
        {
            WorldState world = Shared;
            double flatExpected = 0.0, talusExpected = 0.0, cliffExpected = 0.0, thinExpected = 0.0;
            int flatSeen = 0, talusSeen = 0, cliffSeen = 0, thinSeen = 0, flatCells = 0, talusCells = 0, cliffCells = 0, thinCells = 0;
            int ledges = 0, boulders = 0;
            for (int row = 0; row < FineWorld.Side; row++)
                for (int col = 0; col < FineWorld.Side; col++)
                {
                    GroundCover cover = GroundCovers.CoverOf((byte)world.Cover.Code(row, col));
                    int cobbles = LooseCodes.CobblesOf((byte)world.Loose.Code(row, col));
                    bool trunk = world.Stand.Code(row, col) != 0, stone = world.Stone.Code(row, col) != 0;
                    bool has = StandingRocks.TryOfCell(world, row, col, out StandingRock rock);
                    bool edge = row < StandingRocks.EdgeCells || col < StandingRocks.EdgeCells || row >= FineWorld.Side - StandingRocks.EdgeCells
                                || col >= FineWorld.Side - StandingRocks.EdgeCells;
                    if (has)
                    {
                        Assert.That(trunk, Is.False, "no rock on a trunk's cell: " + rock.Row + ", " + rock.Col);
                        Assert.That(stone, Is.True, "no rock where no stone is");
                        Assert.That(edge, Is.False, "no rock at the region's edge");
                        Assert.That(cover == GroundCover.Rock || Grows(cover), Is.True, "no rock on " + cover);
                        if (cover != GroundCover.Rock)
                        {
                            Assert.That(rock.Form, Is.EqualTo(RockForm.Boulder), "thin soil holds boulders");
                            Assert.That(cobbles, Is.GreaterThan(0), "and only where its stone lies loose");
                        }
                        if (rock.Form == RockForm.Ledge) ledges++;
                        else boulders++;
                    }
                    if (trunk || !stone || edge) continue;
                    if (cover == GroundCover.Rock)
                    {
                        // Counted by the raster's own slope, a reading the rule does not make, away from where the relief could
                        // carry a cell across a line.
                        double slope = RockWorld.RasterSlopeDeg(world, row, col);
                        if (slope < 12.0)
                        {
                            flatCells++;
                            flatExpected += (StandingRocks.PlatformChance + StandingRocks.PlatformPerCobble * cobbles) / 1000.0;
                            if (has) flatSeen++;
                            if (has) Assert.That(rock.Form, Is.EqualTo(RockForm.Boulder), "flat rock holds boulders");
                        }
                        else if (slope > 18.0 && slope < 37.0)
                        {
                            talusCells++;
                            talusExpected += StandingRocks.TalusChance / 1000.0;
                            if (has) talusSeen++;
                            if (has) Assert.That(rock.Form, Is.EqualTo(RockForm.Boulder), "steep rock holds boulders");
                        }
                        else if (slope > 43.0)
                        {
                            cliffCells++;
                            cliffExpected += StandingRocks.LedgeChance / 1000.0;
                            if (has) cliffSeen++;
                            if (has) Assert.That(rock.Form, Is.EqualTo(RockForm.Ledge), "a cliff holds ledges");
                        }
                    }
                    else if (Grows(cover) && cobbles > 0)
                    {
                        thinCells++;
                        thinExpected += StandingRocks.ThinSoilPerCobble * cobbles / 1000.0;
                        if (has) thinSeen++;
                    }
                }
            TestContext.WriteLine("boulders " + boulders + ", ledges " + ledges);
            void Near(string what, int cells, int seen, double expected)
            {
                TestContext.WriteLine(what + ": " + cells + " cells, " + seen + " rocks where " + expected.ToString("0.0") + " were expected");
                Assert.That(cells, Is.GreaterThan(30), what + ": the fixture has such ground");
                Assert.That(Math.Abs(seen - expected), Is.LessThan(4.0 * Math.Sqrt(expected) + 3.0), what + ": as the table's chance says");
            }
            Near("flat rock", flatCells, flatSeen, flatExpected);
            Near("steep rock", talusCells, talusSeen, talusExpected);
            Near("cliff", cliffCells, cliffSeen, cliffExpected);
            Near("thin soil", thinCells, thinSeen, thinExpected);
        }

        [Test]
        public void EveryRockKeepsInsideItsCellClearOfItsEdge()
        {
            double room = FineWorld.CellM / 2.0 - StandingRocks.CellMarginM, most = 0.0;
            foreach (StandingRock rock in Rocks)
            {
                StandLayout.CellCentre(rock.Row, rock.Col, FineWorld.CellM, FineWorld.ExtentM, out double postEast, out double postNorth);
                for (int k = 0; k < 72; k++)
                {
                    double a = k * Math.PI / 36.0, ua = Math.Cos(a), uc = Math.Sin(a);
                    double reach = 1.0 / rock.OutlineAt(ua, uc);
                    rock.ToWorld(ua * reach, uc * reach, out double east, out double north);
                    double off = Math.Max(Math.Abs(east - postEast), Math.Abs(north - postNorth));
                    most = Math.Max(most, off);
                    Assert.That(off, Is.LessThanOrEqualTo(room + 1e-9), "the rim of the rock on " + rock.Row + ", " + rock.Col);
                }
            }
            TestContext.WriteLine(Rocks.Count + " rocks; the farthest rim " + most.ToString("0.000") + " m from its post, of " + room + " allowed");
        }

        [Test]
        public void EveryRockIsSeatedOnTheGround()
        {
            WorldState world = Shared;
            double worst = 0.0;
            foreach (StandingRock rock in Rocks)
            {
                double centre = FineGround.UndugAt(world, rock.East, rock.North);
                if (rock.Form == RockForm.Ledge)
                {
                    Assert.That(rock.MidUp, Is.EqualTo(centre).Within(1e-12), "a ledge straddles the face");
                    continue;
                }
                Assert.That(rock.MidUp - rock.HalfHeight, Is.LessThan(centre), "a boulder's foot is in the ground");
                // Its widest part stands a little above the ground all round, and no more: it does not float on a slope. Seated by
                // sixteen points round its rim, it may stand a few centimetres higher between them where the ground folds (the
                // fixture's scarp edge).
                for (int k = 0; k < 32; k++)
                {
                    double a = k * Math.PI / 16.0, ua = Math.Cos(a), uc = Math.Sin(a);
                    double reach = 1.0 / rock.OutlineAt(ua, uc);
                    rock.ToWorld(ua * reach, uc * reach, out double east, out double north);
                    double above = rock.MidUp - FineGround.UndugAt(world, east, north);
                    worst = Math.Max(worst, above - StandingRocks.SeatShare * rock.HalfHeight);
                    Assert.That(above, Is.LessThan(StandingRocks.SeatShare * rock.HalfHeight + 0.05), "the boulder on " + rock.Row + ", " + rock.Col + " stands off the ground");
                }
            }
            TestContext.WriteLine("the most a boulder's widest part stood above its seat, between the points it was seated by: " + worst.ToString("0.000") + " m");
        }

        [Test]
        public void TheServerAndAClientStandTheSameRocks()
        {
            WorldState world = Shared;
            Dictionary<(TileLayer, TileId), ReceivedTile> held = RockWorld.AllTiles(world);
            ReceivedTile Holding(TileLayer layer, TileId id) => held.TryGetValue((layer, id), out ReceivedTile t) ? t : null;
            ClientGround ground = new ClientGround(FineWorld.Grid(), Holding, world.Seed, world.Changes);
            TileGrid grid = FineWorld.Grid();
            Dictionary<(int, int), StandingRock> server = new Dictionary<(int, int), StandingRock>();
            foreach (StandingRock rock in Rocks) server[(rock.Row, rock.Col)] = rock;
            int drawn = 0;
            for (int iz = 0; iz < grid.TilesPerSide; iz++)
                for (int ix = 0; ix < grid.TilesPerSide; ix++)
                {
                    TileId id = new TileId(ix, iz);
                    PreparedStand placed = StandPreparation.Prepare(Holding(TileLayer.Stand, id), Holding(TileLayer.Loose, id), Holding(TileLayer.Ground, id), grid,
                        null, null, null, ground.SnapshotFor(id), Holding(TileLayer.Stone, id));
                    Assert.That(placed.StoneCrc, Is.EqualTo(Holding(TileLayer.Stone, id).Crc32), "it says which stone it was decided from");
                    foreach (StandingRock mine in placed.Rocks)
                    {
                        Assert.That(server.TryGetValue((mine.Row, mine.Col), out StandingRock theirs), Is.True, "the server has the rock on " + mine.Row + ", " + mine.Col);
                        Same(mine, theirs);
                        drawn++;
                    }
                }
            Assert.That(drawn, Is.EqualTo(server.Count), "every rock drawn once, by one tile");
            // And the rock a client asks of a cell on its main thread, for a body, the crosshair or a stick.
            foreach (StandingRock theirs in Rocks)
            {
                Assert.That(ClientRocks.TryOfCell(Holding, grid, ground.Undug, FineWorld.CellM, theirs.Row, theirs.Col, out StandingRock mine), Is.True);
                Same(mine, theirs);
            }
            Assert.That(server.Count, Is.GreaterThan(500));
            TestContext.WriteLine(server.Count + " rocks, each the same on both sides");
        }

        private static void Same(in StandingRock mine, in StandingRock theirs)
        {
            string where = " of the rock on " + theirs.Row + ", " + theirs.Col;
            Assert.That(mine.Form, Is.EqualTo(theirs.Form), "form" + where);
            Assert.That(mine.Stone, Is.EqualTo(theirs.Stone), "stone" + where);
            Assert.That(mine.Variant, Is.EqualTo(theirs.Variant), "variant" + where);
            Assert.That(mine.YawDeg, Is.EqualTo(theirs.YawDeg), "yaw" + where);
            Assert.That(mine.East, Is.EqualTo(theirs.East).Within(1e-9), "east" + where);
            Assert.That(mine.North, Is.EqualTo(theirs.North).Within(1e-9), "north" + where);
            Assert.That(mine.MidUp, Is.EqualTo(theirs.MidUp).Within(1e-6), "height" + where);
            Assert.That(mine.HalfLength, Is.EqualTo(theirs.HalfLength), "length" + where);
            Assert.That(mine.HalfWidth, Is.EqualTo(theirs.HalfWidth), "width" + where);
            Assert.That(mine.HalfHeight, Is.EqualTo(theirs.HalfHeight), "height's half" + where);
            Assert.That(mine.SquareUp, Is.EqualTo(theirs.SquareUp), "squareness" + where);
            Assert.That(mine.SquareAround, Is.EqualTo(theirs.SquareAround), "squareness round about" + where);
        }

        [Test]
        public void TheSurfaceIsARocksTopOverItAndTheGroundBesideIt()
        {
            WorldState world = Shared;
            int over = 0;
            foreach (StandingRock rock in Rocks)
            {
                double ground = world.GroundAt(rock.East, rock.North), top = rock.MidUp + rock.HalfHeight;
                Assert.That(rock.TryTopAt(rock.East, rock.North, out double atCentre), Is.True);
                Assert.That(atCentre, Is.EqualTo(top).Within(1e-9), "the top of a superellipsoid at its centre is its half-height over its middle");
                Assert.That(world.SurfaceAt(rock.East, rock.North), Is.EqualTo(Math.Max(ground, top)).Within(1e-9));
                if (top > ground) over++;
                // A hand's breadth outside the rim, the ground alone.
                double a = (rock.Row * 37 + rock.Col) % 360 * Math.PI / 180.0, ua = Math.Cos(a), uc = Math.Sin(a);
                double reach = 1.0 / rock.OutlineAt(ua, uc) + 0.05;
                rock.ToWorld(ua * reach, uc * reach, out double east, out double north);
                Assert.That(world.SurfaceAt(east, north), Is.EqualTo(world.GroundAt(east, north)), "beside the rock on " + rock.Row + ", " + rock.Col);
            }
            Assert.That(over, Is.GreaterThan(Rocks.Count * 9 / 10), "nearly every rock stands above the ground at its middle");
        }

        /// <summary>The rock, and the point on it, where it stands farthest above the ground.</summary>
        private static StandingRock Tallest(WorldState world, out double east, out double north, out double above)
        {
            StandingRock best = default;
            east = north = double.NaN;
            above = double.NegativeInfinity;
            foreach (StandingRock rock in Rocks)
                for (int i = -4; i <= 4; i++)
                    for (int j = -4; j <= 4; j++)
                    {
                        rock.ToWorld(rock.HalfLength * i / 5.0, rock.HalfWidth * j / 5.0, out double e, out double n);
                        if (!rock.TryTopAt(e, n, out double top)) continue;
                        double a = top - world.GroundAt(e, n);
                        if (a <= above) continue;
                        above = a;
                        best = rock;
                        east = e;
                        north = n;
                    }
            return best;
        }

        [Test]
        public void AFounderStandingOnABoulderIsStandingAndOneAboveItIsNot()
        {
            WorldState world = Shared;
            StandingRock rock = Tallest(world, out double east, out double north, out double above);
            MovementRules rules = new MovementRules();
            TestContext.WriteLine("the rock on " + rock.Row + ", " + rock.Col + " (" + rock.Form + ") stands " + above.ToString("0.00") + " m above the ground");
            Assert.That(above, Is.GreaterThan(rules.GroundTolerance + 0.05), "the fixture has a rock taller than the check's tolerance");
            MoverState report = MoverState.AtRest(east, world.SurfaceAt(east, north), north);
            report.Grounded = true;
            double half = FineWorld.ExtentM * 0.5;
            Assert.That(MovementValidator.Check(default, false, report, 0.05, new SurfaceSource(world), half, MoverConfig.Default, rules), Is.Null,
                "standing on the rock is standing");
            Assert.That(MovementValidator.Check(default, false, report, 0.05, new FineGroundSource(world), half, MoverConfig.Default, rules), Does.StartWith("grounded"),
                "judged by the ground alone, it would be corrected");
            MoverState floating = report;
            floating.Up += rules.GroundTolerance + 0.2;
            Assert.That(MovementValidator.Check(default, false, floating, 0.05, new SurfaceSource(world), half, MoverConfig.Default, rules), Does.StartWith("grounded"),
                "and standing on the air above it is not");
        }

        [Test]
        public void AThingLetGoOverARockRestsOnItsTop()
        {
            WorldState world = RockWorld.Make();
            StandingRock rock = Tallest(world, out double east, out double north, out _);
            Assert.That(rock.TryTopAt(east, north, out double top), Is.True);
            Entity cobble = world.SpawnItem(DefinitionCatalogue.Cobble, east, north, top + 2.0);
            ItemFall fall = new ItemFall();
            for (int i = 0; i < 200 && !cobble.Item.Resting; i++) fall.Step(world, 0.05);
            Assert.That(cobble.Item.Resting, Is.True);
            Assert.That(cobble.Position.Y, Is.EqualTo(top).Within(1e-9), "on the rock's top");
            Assert.That(cobble.Position.Y, Is.GreaterThan(world.GroundAt(east, north) + 0.5), "not down inside it on the ground");
            Entity dropped = world.SpawnItem(DefinitionCatalogue.Cobble, east, north);
            Assert.That(dropped.Position.Y, Is.EqualTo(top).Within(1e-9), "a thing put at a point with no height is put on the rock too");
        }

        [Test]
        public void LitterWhosePlaceFallsInsideARockLiesAtItsFootOnBothSides()
        {
            WorldState world = Shared;
            Dictionary<(TileLayer, TileId), ReceivedTile> held = RockWorld.AllTiles(world);
            ReceivedTile Holding(TileLayer layer, TileId id) => held.TryGetValue((layer, id), out ReceivedTile t) ? t : null;
            ClientGround ground = new ClientGround(FineWorld.Grid(), Holding, world.Seed, world.Changes);
            TileGrid grid = FineWorld.Grid();
            Dictionary<TileId, PreparedStand> prepared = new Dictionary<TileId, PreparedStand>();
            int cellCm = (int)Math.Round(FineWorld.CellM * 100.0), moved = 0;
            foreach (StandingRock rock in Rocks)
            {
                byte code = (byte)world.Loose.Code(rock.Row, rock.Col);
                StandLayout.CellCentre(rock.Row, rock.Col, FineWorld.CellM, FineWorld.ExtentM, out double postEast, out double postNorth);
                foreach (StandLayout.Kind kind in new[] { StandLayout.Kind.Stick, StandLayout.Kind.Cobble })
                    for (int k = 0; k < LooseCodes.CountOf(code, kind); k++)
                    {
                        StandLayout.Place(rock.Row, rock.Col, kind, k, cellCm, out int eastCm, out int northCm, out _);
                        if (!rock.Covers(postEast + eastCm / 100.0, postNorth + northCm / 100.0)) continue;
                        LyingThing thing = new LyingThing(rock.Row, rock.Col, kind, k);
                        Assert.That(LyingThings.TryFind(world, thing, out Double3 at), Is.True);
                        Assert.That(rock.Covers(at.X, at.Z), Is.False, thing + " lies outside the rock");
                        Assert.That(Math.Max(Math.Abs(at.X - postEast), Math.Abs(at.Z - postNorth)), Is.LessThan(FineWorld.CellM / 2.0), "and inside its own cell");
                        Assert.That(at.Y, Is.EqualTo(world.GroundAt(at.X, at.Z)).Within(1e-9), "on the ground");
                        // Where the client draws it: the thing of its tile's list nearest the server's place.
                        TileId id = grid.ForPosition(postEast, postNorth);
                        if (!prepared.TryGetValue(id, out PreparedStand stand))
                            prepared[id] = stand = StandPreparation.Prepare(Holding(TileLayer.Stand, id), Holding(TileLayer.Loose, id), Holding(TileLayer.Ground, id), grid,
                                null, null, null, ground.SnapshotFor(id), Holding(TileLayer.Stone, id));
                        double nearest = double.MaxValue;
                        foreach (LooseInstance drawn in kind == StandLayout.Kind.Stick ? stand.Sticks : stand.Cobbles)
                            nearest = Math.Min(nearest, Math.Abs(drawn.East - at.X) + Math.Abs(drawn.North - at.Z) + Math.Abs(drawn.Up - at.Y));
                        Assert.That(nearest, Is.LessThan(1e-3), thing + " drawn where the server holds it");
                        moved++;
                    }
            }
            TestContext.WriteLine(moved + " sticks and cobbles moved to a rock's foot");
            Assert.That(moved, Is.GreaterThan(10), "the fixture has things whose places fall inside rocks");
        }

        [Test]
        public void ADigBesideABoulderLeavesItWhereItWasAndNoneDigsUnderIt()
        {
            WorldState world = RockWorld.Make();
            StandingRock boulder = default;
            bool found = false;
            foreach (StandingRock rock in Rocks)
                if (rock.Form == RockForm.Boulder && Grows(GroundCovers.CoverOf((byte)world.Cover.Code(rock.Row, rock.Col))))
                {
                    boulder = rock;
                    found = true;
                    break;
                }
            Assert.That(found, Is.True, "a boulder on thin soil");
            world.Changes.Dig(boulder.Row, boulder.Col + 1, 60);
            StandLayout.CellCentre(boulder.Row, boulder.Col, FineWorld.CellM, FineWorld.ExtentM, out double postEast, out double postNorth);
            Assert.That(world.GroundAt(postEast + 1.9, postNorth), Is.LessThan(FineGround.UndugAt(world, postEast + 1.9, postNorth) - 0.01),
                "the hollow dug beside it reaches into the boulder's cell");
            Assert.That(StandingRocks.TryOfCell(world, boulder.Row, boulder.Col, out StandingRock after), Is.True);
            Assert.That(after.MidUp, Is.EqualTo(boulder.MidUp), "a rock is seated on the ground as it was made");
            Assert.That(StandingThings.TryGround(world, boulder.Row, boulder.Col, out GroundSite site), Is.True);
            Assert.That(site.RockStands, Is.True, "the ground under it knows it is there");
            ThingState pointed = new ThingState();
            pointed.SetMarks(ThingMarks.Pointed);
            Assert.That(Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, pointed, site).Words, Does.Contain("boulder"));
        }

        [Test]
        public void ARockIsOfTheRockBeneathNotThePebblesOnIt()
        {
            int sandstone = -1;
            for (int i = 0; i < StoneType.All.Count; i++)
                if (ReferenceEquals(StoneType.All[i], StoneType.Sandstone)) sandstone = i;
            for (int i = 0; i < StoneType.All.Count; i++)
            {
                StoneType s = StoneType.All[i];
                bool pebbles = s == StoneType.Quartz || s == StoneType.Rhyolite || s == StoneType.Quartzite || s == StoneType.Chert || s == StoneType.Flint || s == StoneType.Obsidian;
                Assert.That(StandingRocks.BedrockOf(i), Is.EqualTo(pebbles ? sandstone : i), s.Name);
            }
            // The fixture's quartzite blocks stand as sandstone rocks.
            foreach (StandingRock rock in Rocks)
                Assert.That(StoneType.All[rock.Stone], Is.Not.SameAs(StoneType.Quartzite), "the rock on " + rock.Row + ", " + rock.Col);
        }

        [Test]
        public void EveryShapeIsAConvexSuperellipsoid()
        {
            for (int stone = 0; stone < StoneType.All.Count; stone++)
                foreach (RockForm form in new[] { RockForm.Boulder, RockForm.Ledge })
                    for (int v = 0; v < StandingRocks.Variants; v++)
                    {
                        StandingRocks.ShapeOf(stone, form, v, out double up, out double around);
                        // Barr's superellipsoid is convex for squarenesses up to 2; these are all under 1, so it has flat faces and worn corners.
                        Assert.That(up, Is.InRange(0.1, 1.0), StoneType.All[stone].Name + " " + form + " " + v);
                        Assert.That(around, Is.InRange(0.1, 1.0), StoneType.All[stone].Name + " " + form + " " + v);
                    }
        }
    }
}
