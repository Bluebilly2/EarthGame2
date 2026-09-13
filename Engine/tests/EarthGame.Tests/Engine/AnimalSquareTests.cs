using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Presence from each square's own capacity (M1.7a promise 1): the world's capacity layers differ square by square, and
    /// a group is drawn at the rate its own square feeds, while the draw asked with a single capacity keeps every group it
    /// found before.
    /// </summary>
    public sealed class AnimalSquareTests
    {
        private const double AugustDaylightHours = 10.6;
        private const ulong Seed = 4001UL;

        private static bool Same(AnimalSighting a, AnimalSighting b) =>
            a.Species == b.Species && a.EastM == b.EastM && a.NorthM == b.NorthM && a.GroupSize == b.GroupSize && a.Activity01 == b.Activity01;

        [Test]
        public void ASingleCapacityDrawsWhatEverySquareAtThatCapacityDraws()
        {
            AnimalPresence presence = new AnimalPresence(Seed);
            foreach (AnimalSpecies species in new[] { AnimalSpecies.EasternGreyKangaroo, AnimalSpecies.PiedOystercatcher })
            {
                List<AnimalSighting> single = presence.Near(species, 25.0, 1200.0, -800.0, 1500.0, 6.5, AugustDaylightHours, 3.0);
                List<AnimalSighting> squares = presence.Near(species, (cx, cz) => 25.0, 1200.0, -800.0, 1500.0, 6.5, AugustDaylightHours, 3.0);
                Assert.That(single.Count, Is.GreaterThan(0), species + ": there must be groups to compare");
                Assert.That(squares.Count, Is.EqualTo(single.Count), species + ": the same groups");
                for (int i = 0; i < single.Count; i++)
                    Assert.That(Same(squares[i], single[i]), Is.True, species + ": group " + i + " in the same place, in the same order");
            }
        }

        [Test]
        public void ASquareThatFeedsNothingHoldsNothingAndItsNeighboursKeepTheirGroups()
        {
            AnimalPresence presence = new AnimalPresence(Seed);
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            // Everything west of east 0 feeds nothing; everything east of it feeds 25 a square kilometre.
            List<AnimalSighting> everywhere = presence.Near(roo, 25.0, 0.0, 0.0, 2000.0, 18.0, AugustDaylightHours);
            List<AnimalSighting> eastOnly = presence.Near(roo, (cx, cz) => cx >= 0 ? 25.0 : 0.0, 0.0, 0.0, 2000.0, 18.0, AugustDaylightHours);

            // A group wanders at most the drift from a place inside its own square, so one lying 40 m or more east of the
            // line came from a feeding square, and one lying 40 m or more west of it did not.
            int kept = 0;
            foreach (AnimalSighting g in everywhere)
            {
                if (g.EastM < AnimalPresence.DriftRadiusM) continue;
                kept++;
                Assert.That(eastOnly.Exists(h => Same(h, g)), Is.True, "a group of a feeding square keeps its place: " + g.EastM.ToString("F1") + ", " + g.NorthM.ToString("F1"));
            }
            Assert.That(kept, Is.GreaterThan(5), "there must be groups east of the line to keep");
            foreach (AnimalSighting g in eastOnly)
                Assert.That(g.EastM, Is.GreaterThanOrEqualTo(-AnimalPresence.DriftRadiusM), "no group of a square that feeds nothing: " + g.EastM.ToString("F1"));
        }

        [Test]
        public void ASquaresGroupIsTheSameWhoeverAsks()
        {
            AnimalPresence presence = new AnimalPresence(Seed);
            AnimalSpecies bird = AnimalSpecies.PiedOystercatcher;
            Func<int, int, double> shore = (cx, cz) => cz % 3 == 0 ? 10.0 : 0.0;
            // Two founders 300 m apart ask round themselves; what both reach is the same birds in the same places.
            List<AnimalSighting> fromWest = presence.Near(bird, shore, -150.0, 0.0, 600.0, 11.0, AugustDaylightHours, 40.0);
            List<AnimalSighting> fromEast = presence.Near(bird, shore, 150.0, 0.0, 600.0, 11.0, AugustDaylightHours, 40.0);
            int shared = 0;
            foreach (AnimalSighting g in fromWest)
            {
                double toEast = Math.Sqrt((g.EastM - 150.0) * (g.EastM - 150.0) + g.NorthM * g.NorthM);
                if (toEast > 600.0) continue;
                shared++;
                Assert.That(fromEast.Exists(h => Same(h, g)), Is.True, "a pair both founders reach is found by both");
            }
            Assert.That(shared, Is.GreaterThan(0), "there must be pairs both reach");
        }
    }
}
