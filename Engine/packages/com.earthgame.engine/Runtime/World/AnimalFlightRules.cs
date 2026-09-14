using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// How a kind of animal answers a founder (M1.7c): how near a founder sends a group running, how far and how fast it
    /// runs, and how fast it walks back to where presence puts it once it has stood a while. The distances as built are the
    /// measured distances of animals nobody harms: kangaroos took flight from a person walking up at 78 to 90 m (Wolf and
    /// Croft 2010, Applied Animal Behaviour Science 126), pied oystercatchers at 39 to 83 m (Weston and others 2012, Emu
    /// 112). The speeds are design inside the measured bounds: a mob's 7 m/s between what kangaroos chose in an enclosure and
    /// the records, the oystercatcher's 15 m/s the floor of the shorebirds' band (Alerstam and others 2007). The runs'
    /// lengths rest on nothing read (DEBTS). William left the distances to the agent (CANON ruling 29) and asked for them as
    /// the panel's sliders (ruling 30): the stand-up holds a set per kind, moved by a developer's settings, never saved.
    /// </summary>
    public sealed class AnimalFlightRules
    {
        public const double KangarooFleeWithinM = 80.0;
        public const double KangarooRunM = 150.0;
        public const double KangarooRunMs = 7.0;
        public const double KangarooWalkMs = 1.5;
        public const double OystercatcherFleeWithinM = 60.0;
        public const double OystercatcherRunM = 200.0;
        public const double OystercatcherRunMs = 15.0;
        public const double OystercatcherWalkMs = 4.0;

        /// <summary>How long a group that has run stands before it walks back, seconds: design, not a citation.</summary>
        public const double SettleSeconds = 20.0;

        /// <summary>How near a founder must come to any member, m, for the group to run.</summary>
        public double FleeWithinM;
        /// <summary>How far the group runs, m, along the ground.</summary>
        public double RunM;
        /// <summary>How fast it runs, m/s.</summary>
        public double RunMs;
        /// <summary>How fast it walks back, m/s.</summary>
        public double WalkMs;

        public AnimalFlightRules(double fleeWithinM, double runM, double runMs, double walkMs)
        {
            FleeWithinM = fleeWithinM;
            RunM = runM;
            RunMs = runMs;
            WalkMs = walkMs;
        }

        /// <summary>The rules a kind is built with, fresh; null for a kind no rules are held for.</summary>
        public static AnimalFlightRules DefaultsFor(AnimalSpecies species)
        {
            if (species == AnimalSpecies.EasternGreyKangaroo) return new AnimalFlightRules(KangarooFleeWithinM, KangarooRunM, KangarooRunMs, KangarooWalkMs);
            if (species == AnimalSpecies.PiedOystercatcher) return new AnimalFlightRules(OystercatcherFleeWithinM, OystercatcherRunM, OystercatcherRunMs, OystercatcherWalkMs);
            return null;
        }
    }

    /// <summary>A flight as it begins (M1.7c): which group, from which founder, how far off they were and which way it runs; the host records it.</summary>
    public readonly struct AnimalFlight
    {
        public readonly AnimalSpecies Species;
        public readonly int CellX, CellZ;
        public readonly double GroupEastM, GroupNorthM;
        public readonly double FounderEastM, FounderNorthM;
        /// <summary>From the founder to the nearest member, m, when the group took flight.</summary>
        public readonly double DistanceM;
        /// <summary>Which way it runs, degrees clockwise from north.</summary>
        public readonly double BearingDeg;

        public AnimalFlight(AnimalSpecies species, int cellX, int cellZ, double groupEastM, double groupNorthM,
                            double founderEastM, double founderNorthM, double distanceM, double bearingDeg)
        {
            Species = species;
            CellX = cellX;
            CellZ = cellZ;
            GroupEastM = groupEastM;
            GroupNorthM = groupNorthM;
            FounderEastM = founderEastM;
            FounderNorthM = founderNorthM;
            DistanceM = distanceM;
            BearingDeg = bearingDeg;
        }
    }
}
