using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Taking what lies (M1.5b promises 1 to 3): a thing lying is found by its cell, kind and index where the layout puts
    /// it; the world keeps what is taken beside the layer, by the bit; a founder takes a thing within reach, and a cobble
    /// comes up as the stone its cell names.
    /// </summary>
    public sealed class LooseTakenTests
    {
        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        /// <summary>The cell whose centre is (300, -300) on the made coast.</summary>
        private const int Row = 110, Col = 110;

        private static int StoneCode(StoneType stone)
        {
            for (int i = 0; i < StoneType.All.Count; i++)
                if (ReferenceEquals(StoneType.All[i], stone)) return i + 1;
            throw new ArgumentException(stone.Name);
        }

        /// <summary>The made coast with three sticks and two cobbles on one cell, and silcrete named everywhere.</summary>
        private static WorldState World(bool withStone = true)
        {
            RegionRaster loose = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_loose", "loose",
                (row, col) => row == Row && col == Col ? LooseCodes.Pack(3, 2) : 0u, null);
            RegionRaster stone = withStone
                ? TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_stone", "stone",
                    (row, col) => (uint)StoneCode(StoneType.Silcrete), null)
                : null;
            return new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()), 0, null, null, null, null, loose, stone);
        }

        [Test]
        public void AThingIsFoundWhereTheLayoutPutsItAndTakenOnlyOnce()
        {
            WorldState w = World();
            LyingThing stick = new LyingThing(Row, Col, StandLayout.Kind.Stick, 1);
            StandLayout.CellCentre(Row, Col, TestRasters.MadeCellM, TestRasters.MadeExtentM, out double centreEast, out double centreNorth);
            Assert.That((centreEast, centreNorth), Is.EqualTo((300.0, -300.0)));
            Assert.That(LyingThings.TryFind(w, stick, out Double3 at), Is.True);
            StandLayout.Place(Row, Col, StandLayout.Kind.Stick, 1, 1000, out int eastCm, out int northCm, out _);
            Assert.That(at.X, Is.EqualTo(300.0 + eastCm / 100.0).Within(1e-9), "east, as the layout puts it");
            Assert.That(at.Z, Is.EqualTo(-300.0 + northCm / 100.0).Within(1e-9), "north");
            Assert.That(at.Y, Is.EqualTo(w.GroundAt(at.X, at.Z)).Within(1e-9), "on the ground the server holds");
            Assert.That(LyingThings.TryFind(w, new LyingThing(Row, Col, StandLayout.Kind.Stick, 3), out _), Is.False, "the cell holds three sticks");
            Assert.That(LyingThings.TryFind(w, new LyingThing(Row, Col, StandLayout.Kind.Cobble, 2), out _), Is.False, "and two cobbles");
            Assert.That(LyingThings.TryFind(w, new LyingThing(Row, Col, StandLayout.Kind.Trunk, 0), out _), Is.False, "a trunk is not a thing lying");
            Assert.That(LyingThings.TryFind(w, new LyingThing(Row + 1, Col, StandLayout.Kind.Stick, 0), out _), Is.False, "nothing lies next door");

            Assert.That(w.Taken.Take(stick), Is.True);
            Assert.That(w.Taken.Take(stick), Is.False, "once");
            Assert.That(w.Taken.IsTaken(stick), Is.True);
            Assert.That(w.Taken.IsTaken(new LyingThing(Row, Col, StandLayout.Kind.Stick, 0)), Is.False, "the stick beside it keeps its place");
            Assert.That(LyingThings.TryFind(w, stick, out _), Is.False, "taken is gone");
            Assert.That(LyingThings.TryFind(w, new LyingThing(Row, Col, StandLayout.Kind.Stick, 2), out _), Is.True, "and nothing moves up into its place");
            Assert.That(w.Taken.Cells()[0].Sticks, Is.EqualTo((ushort)2), "a bit for index 1");
        }

        [Test]
        public void TheTakingsMergeByTheBitAndRefuseWhatNoCodeCounts()
        {
            LooseTaken taken = new LooseTaken();
            taken.Merge(new LooseTaken.Cell { Row = 5, Col = 7, Sticks = 1 });
            taken.Merge(new LooseTaken.Cell { Row = 5, Col = 7, Sticks = 4, Cobbles = 2 });
            taken.Merge(new LooseTaken.Cell { Row = 2, Col = 9, Cobbles = 1 });
            taken.Merge(new LooseTaken.Cell { Row = 2, Col = 3 });
            List<LooseTaken.Cell> cells = taken.Cells();
            Assert.That(cells.Count, Is.EqualTo(2), "a cell with nothing taken is no cell");
            Assert.That((cells[0].Row, cells[0].Col), Is.EqualTo((2, 9)), "by row, then column");
            Assert.That(cells[1].Sticks, Is.EqualTo((ushort)5), "added to, never put back");
            Assert.That(cells[1].Cobbles, Is.EqualTo((ushort)2));
            Assert.Throws<ArgumentException>(() => taken.Merge(new LooseTaken.Cell { Row = 1, Col = 1, Sticks = 0x8000 }), "a sixteenth stick");
            Assert.That(taken.Take(new LyingThing(1, 1, StandLayout.Kind.Stick, LooseCodes.MaxEach)), Is.False, "past the most a code counts");
            Assert.That(taken.Take(new LyingThing(1, 1, StandLayout.Kind.Trunk, 0)), Is.False, "a trunk");
            Assert.That(taken.Count, Is.EqualTo(2));
        }

        [Test]
        public void AFounderTakesAThingWithinReachAndACobbleComesUpAsItsStone()
        {
            WorldState w = World();
            w.Step(0.05);
            LyingThing cobble = new LyingThing(Row, Col, StandLayout.Kind.Cobble, 0);
            Assert.That(LyingThings.TryFind(w, cobble, out Double3 at), Is.True);
            Double3 eye = new Double3(at.X + 1.0, at.Y + MoverConfig.Default.EyeHeight(Stance.Standing), at.Z);
            Hands hands = new Hands();
            ulong next = w.Entities.NextId;
            Assert.That(hands.PickUpLying(w, cobble, new Double3(at.X + 9.0, eye.Y, at.Z)), Is.EqualTo(VerbOutcome.OutOfReach), "9 m off");
            Assert.That(hands.PickUpLying(w, cobble, eye), Is.EqualTo(VerbOutcome.Done));
            Assert.That(hands.Things[0].Definition, Is.SameAs(DefinitionCatalogue.CobbleOf(StoneType.Silcrete)), "the stone its cell names");
            Assert.That(hands.Things[0].Id, Is.EqualTo(next), "an id of its own");
            Assert.That(hands.Things[0].SpawnTick, Is.EqualTo(w.Tick));
            Assert.That(w.Entities.NextId, Is.EqualTo(next + 1), "never used again");
            Assert.That(w.Entities.Count, Is.Zero, "it never lay in the store");
            Assert.That(w.Taken.IsTaken(cobble), Is.True);
            Assert.That(hands.PickUpLying(w, cobble, eye), Is.EqualTo(VerbOutcome.NotThere), "taken is gone");

            Assert.That(hands.PutDown(w, at, eye, 0f), Is.EqualTo(VerbOutcome.Done), "put down, it is an entity like any other");
            Assert.That(w.Entities.TryGet(next, out Entity e), Is.True);
            Assert.That(e.Definition, Is.SameAs(DefinitionCatalogue.CobbleOf(StoneType.Silcrete)));
            Assert.That(LyingThings.TryFind(w, cobble, out _), Is.False, "and never back in the layer");

            LyingThing stick = new LyingThing(Row, Col, StandLayout.Kind.Stick, 0);
            Assert.That(LyingThings.TryFind(w, stick, out Double3 stickAt), Is.True);
            Assert.That(hands.PickUpLying(w, stick, new Double3(stickAt.X, stickAt.Y + 1.65, stickAt.Z)), Is.EqualTo(VerbOutcome.Done));
            Assert.That(hands.Things[0].Definition, Is.SameAs(DefinitionCatalogue.Stick));

            Assert.That(LyingThings.DefinitionOf(World(withStone: false), cobble), Is.SameAs(DefinitionCatalogue.Cobble), "a world with no stone named gives the plain cobble");
        }
    }
}
