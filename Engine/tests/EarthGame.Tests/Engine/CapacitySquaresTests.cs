using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// What each square of presence feeds (M1.7a promise 1): the mean of a kind's capacity layer over the cells whose centres
    /// lie in the square, worked out once, with nothing beyond the layer.
    /// </summary>
    public sealed class CapacitySquaresTests
    {
        /// <summary>A layer on the made coast's grid: 161 cells 10 m apart, their centres from 800 m west of the centre to 800 m east.</summary>
        private static RegionRaster Layer(Func<int, int, float> law) =>
            TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "capacity", law, CapacitySquares.Unit);

        [Test]
        public void ASquareFeedsTheMeanOfTheCellsWhoseCentresItHolds()
        {
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo, bird = AnimalSpecies.PiedOystercatcher;
            CapacitySquares squares = new CapacitySquares(TestRasters.MadeExtentM);
            // The kangaroo's capacity is each cell's column, counted from the west edge; the bird's its row, from the north.
            squares.Add(roo, Layer((row, col) => col));
            squares.Add(bird, Layer((row, col) => row));

            Assert.That(squares.PerKm2(roo, 0, 0), Is.EqualTo(84.5).Within(1e-9), "the centre to 100 m east holds the columns 0 to 90 m east, 80 to 89");
            Assert.That(squares.PerKm2(roo, -8, 3), Is.EqualTo(4.5).Within(1e-9), "800 to 700 m west holds columns 0 to 9, whatever the row");
            Assert.That(squares.PerKm2(roo, 8, -2), Is.EqualTo(160.0).Within(1e-9), "800 m east and on holds the east edge's column alone");
            Assert.That(squares.PerKm2(bird, 5, 0), Is.EqualTo(75.5).Within(1e-9), "the centre to 100 m north holds rows 71 to 80");
            Assert.That(squares.PerKm2(bird, 5, -1), Is.EqualTo(85.5).Within(1e-9), "100 m south to the centre holds rows 81 to 90");
            Assert.That(squares.PerKm2(bird, -2, -8), Is.EqualTo(155.5).Within(1e-9), "800 to 700 m south holds rows 151 to 160");

            Assert.That(squares.PerKm2(roo, 9, 0), Is.Zero, "a square beyond the layer feeds nothing");
            Assert.That(squares.PerKm2(roo, 0, -9), Is.Zero);
            Assert.That(squares.Feeds(roo), Is.True);
            Assert.That(squares.Feeds(AnimalSpecies.SuperbFairyWren), Is.False, "a kind whose layer was not added");
            Assert.That(squares.PerKm2(AnimalSpecies.SuperbFairyWren, 0, 0), Is.Zero, "feeds nothing");
        }

        [Test]
        public void ALayerInAnotherUnitOrOfAnotherExtentIsRefusedAndSoIsAKindTwice()
        {
            CapacitySquares squares = new CapacitySquares(TestRasters.MadeExtentM);
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            Assert.Throws<ArgumentException>(() => squares.Add(roo, TestRasters.FromLaw(81, 10.0, 800.0, "small", (row, col) => 1f, CapacitySquares.Unit)), "a layer of another extent");
            Assert.Throws<ArgumentException>(() => squares.Add(roo,
                TestRasters.FromLaw(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "metres", (row, col) => 1f)), "a layer in metres");
            Assert.Throws<ArgumentException>(() => squares.Add(roo,
                TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "codes", "water", (row, col) => 1u, null)), "a layer of codes");
            squares.Add(roo, Layer((row, col) => 1f));
            Assert.Throws<ArgumentException>(() => squares.Add(roo, Layer((row, col) => 2f)), "a kind added twice");
            Assert.That(squares.PerKm2(roo, 0, 0), Is.EqualTo(1.0), "and the first stands");
            Assert.Throws<ArgumentException>(() => new WorldState(1UL, Region.Bherwerre, Region.Bherwerre.WakeClock(), capacity: squares),
                "squares of another extent than the world's region");
        }
    }
}
