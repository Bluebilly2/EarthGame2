using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>One group of animals, where they stand and what they are doing.</summary>
    public readonly struct AnimalSighting
    {
        public readonly AnimalSpecies Species;

        /// <summary>Where the group is, in the same metres everything else uses.</summary>
        public readonly double EastM, NorthM;

        /// <summary>How many of them. A mob is not an individual repeated.</summary>
        public readonly int GroupSize;

        /// <summary>
        /// How awake they are, 0 lying up to 1 out and feeding. The founder sees the difference:
        /// a mob grazing across a flat at dusk is not the same sight as one dozing at noon.
        /// </summary>
        public readonly double Activity01;

        public AnimalSighting(AnimalSpecies species, double eastM, double northM,
                              int groupSize, double activity01)
        {
            Species = species;
            EastM = eastM;
            NorthM = northM;
            GroupSize = groupSize;
            Activity01 = activity01;
        }
    }

    /// <summary>
    /// Where the animals are, as a function rather than as a population being simulated. Ported
    /// from v1 (slice E5b) unchanged in mechanism.
    ///
    /// <para><b>The synoptic law, on legs.</b> Presence is a stateless seeded function of place
    /// and time, not a herd of objects being stepped: what stands on a flat at dusk is
    /// <i>derivable</i>, so the tablet can know it before the founder walks there, a test can
    /// replay it exactly, and the save file never carries an animal. The same reasoning that keeps
    /// the weather forecastable keeps the fauna forecastable, and for the same reason — anything
    /// that has to be walked forward cannot be asked about tomorrow. M1.7 materialises entities
    /// from this expectation inside the interest radius (ARCHITECTURE §4.4) and dematerialises
    /// them outside it; the expectation stays a function.</para>
    ///
    /// <para><b>Position enters only to vary the draw.</b> Terrain reaches this layer solely
    /// through the capacity a supplied site produced: this asks no questions of the ground. Hand
    /// it a capacity and it tells you what that capacity looks like from where you are standing.</para>
    ///
    /// <para>Movement is unhurried seeded drift along the function — enough for a mob to graze
    /// across a flat over an hour. Not steering, not pathfinding, and nothing that reacts to the
    /// founder: an animal that fled would be state, and state is what this layer exists to avoid
    /// until the entities want it.</para>
    /// </summary>
    public sealed class AnimalPresence
    {
        /// <summary>
        /// How the ground is divided for the draw, m. Small enough that a cell rarely holds more
        /// than one group even for the densest solitary species.
        /// </summary>
        public const double CellSizeM = 100.0;

        /// <summary>How far a group wanders from where its cell put it, m.</summary>
        public const double DriftRadiusM = 40.0;

        /// <summary>Hours for that wander to come round again. A grazing mob crosses a flat.</summary>
        public const double DriftPeriodHours = 3.0;

        /// <summary>
        /// Activity floor. Nothing is ever perfectly asleep — a kangaroo is occasionally up at noon,
        /// and a model that said "never" would be lying in the tidier direction.
        /// </summary>
        public const double RestingActivity = 0.04;

        /// <summary>How wide the dawn and dusk peaks are for a crepuscular animal, hours.</summary>
        public const double TwilightWidthHours = 1.3;

        public ulong Seed { get; }

        public AnimalPresence(ulong seed)
        {
            Seed = seed;
        }

        // ---- the rhythm, on the clock the nights already run ----

        /// <summary>
        /// How active this kind is at this hour, 0 to 1.
        ///
        /// <para>Read off the <b>real</b> sunrise and sunset rather than fixed clock hours, because
        /// <see cref="SolarClock.DaylightHours"/> already knows them and they move with the season:
        /// ten hours of daylight in late winter here and fifteen in midsummer, so a crepuscular
        /// animal's two peaks move with the year. Mobs at dawn and dusk — the same hours the founder
        /// is coldest, which is the kind of coincidence this game is made of rather than one anybody
        /// arranged.</para>
        /// </summary>
        public static double Activity01(AnimalSpecies species, double hourOfDay, double daylightHours)
        {
            if (species == null) return 0.0;

            // Off SolarClock's own arithmetic rather than written out again here; the clock owns it.
            double daylight = SimMath.Clamp(daylightHours, 1.0, 23.0);
            double sunrise = SolarClock.SunriseHourFor(daylight);
            double sunset = SolarClock.SunsetHourFor(daylight);
            double h = Mod(hourOfDay, 24.0);

            double core;

            switch (species.Rhythm)
            {
                case AnimalRhythm.Diurnal:
                {
                    // Up with the light and down with it, fullest in the middle of the day.
                    core = h <= sunrise || h >= sunset
                        ? 0.0
                        : Math.Sin(Math.PI * (h - sunrise) / daylight);
                    break;
                }

                case AnimalRhythm.Nocturnal:
                {
                    double night = 24.0 - daylight;
                    double sinceSunset = Mod(h - sunset, 24.0);
                    core = sinceSunset >= night ? 0.0 : Math.Sin(Math.PI * sinceSunset / night);
                    break;
                }

                default:
                {
                    // Two peaks, one at each edge of the light. The gap between them is what makes
                    // a crepuscular animal different from a diurnal one rather than a dimmer one.
                    core = Math.Max(Bump(h, sunrise), Bump(h, sunset));
                    break;
                }
            }

            return SimMath.Clamp(RestingActivity + (1.0 - RestingActivity) * core, 0.0, 1.0);
        }

        // ---- the dawn chorus ----

        /// <summary>
        /// How far before sunrise the chorus is loudest, hours.
        ///
        /// <para><b>Before, not at.</b> Songbirds begin in civil twilight while it is still too
        /// dark to forage, and that is the whole character of the thing: the founder hears the
        /// morning before they can see it. What is well established is the <i>mechanism</i>: song
        /// onset tracks a light-intensity threshold rather than the clock, so onsets across a
        /// temperate chorus spread over roughly the half hour before sunrise, and twenty-four
        /// minutes puts a small passerine in the middle of that. The honest limit: this model has
        /// no light-intensity reading to key on, so the threshold is a fixed offset from sunrise,
        /// right on average and wrong on a heavily overcast morning.</para>
        /// </summary>
        public const double ChorusPeakBeforeSunriseHours = 0.4;

        /// <summary>How fast the chorus comes up, hours. Short: it starts almost together.</summary>
        public const double ChorusRiseHours = 0.5;

        /// <summary>
        /// How slowly it dies away, hours. Longer than the rise, because it does not stop - it
        /// thins out into ordinary morning song over an hour or more.
        /// </summary>
        public const double ChorusFallHours = 1.6;

        /// <summary>How loud the evening reprise is against the dawn, 0-1.</summary>
        public const double DuskChorusShare = 0.35;

        /// <summary>
        /// How much this species is singing, 0 to 1 — which is not the same as how active it is.
        ///
        /// <para><see cref="Activity01"/> cannot answer this and should not be asked to. A wren is
        /// diurnal, so its activity peaks at midday; its <b>song</b> peaks before sunrise. Birds
        /// sing at first light because still, cool air carries sound furthest and because it is the
        /// cheapest hour to hold a territory — reasons that have nothing to do with being awake.
        /// Wiring a chorus to activity would put the dawn chorus at noon.</para>
        /// </summary>
        public static double DawnChorus01(AnimalSpecies species, double hourOfDay, double daylightHours)
        {
            if (species == null || !species.SingsAtDawn) return 0.0;

            double daylight = SimMath.Clamp(daylightHours, 1.0, 23.0);
            double sunrise = SolarClock.SunriseHourFor(daylight);
            double sunset = SolarClock.SunsetHourFor(daylight);

            double dawn = Skew(hourOfDay, sunrise - ChorusPeakBeforeSunriseHours);
            double dusk = Skew(hourOfDay, sunset) * DuskChorusShare;

            return SimMath.Clamp01(Math.Max(dawn, dusk));
        }

        /// <summary>A peak that rises faster than it falls, wrapping across midnight.</summary>
        private static double Skew(double hour, double centre)
        {
            double d = Mod(hour - centre + 12.0, 24.0) - 12.0;      // shortest way round
            double width = d < 0.0 ? ChorusRiseHours : ChorusFallHours;
            double t = d / width;
            return Math.Exp(-t * t);
        }

        /// <summary>A soft peak around an hour, wrapping across midnight.</summary>
        private static double Bump(double hour, double centre)
        {
            double d = Mod(hour - centre + 12.0, 24.0) - 12.0;      // shortest way round
            double t = d / TwilightWidthHours;
            return Math.Exp(-t * t);
        }

        // ---- what is near ----

        /// <summary>
        /// How many individuals of this kind should be visible within a radius, on average: the
        /// standing population is the capacity, and what is <i>seen</i> is that population times
        /// how much of it is up. Walk far enough and the sightings have to add back to the density.
        /// </summary>
        public static double ExpectedSightings(double capacityPerKm2, double radiusM, double activity01)
        {
            if (capacityPerKm2 <= 0.0 || radiusM <= 0.0) return 0.0;
            double areaKm2 = Math.PI * radiusM * radiusM / 1e6;
            return capacityPerKm2 * areaKm2 * SimMath.Clamp(activity01, 0.0, 1.0);
        }

        /// <summary>
        /// The group standing closest to a point, out of groups already found, with how far off it
        /// is and which way — the one question both the fauna reading and a scenario ask about a
        /// mob the founder means to walk up to. <paramref name="species"/> may be null, which means
        /// "of any kind". Bearing is degrees clockwise from north, 0 to 360, and distance is flat
        /// metres: nothing in this layer knows how high the ground is. Ties keep the first group in
        /// the list, so this is as ordered as its input.
        /// </summary>
        public static bool Nearest(IReadOnlyList<AnimalSighting> sightings, AnimalSpecies species,
                                   double eastM, double northM, out AnimalSighting nearest,
                                   out double distanceM, out double bearingDeg)
        {
            nearest = default;
            distanceM = double.PositiveInfinity;
            bearingDeg = 0.0;

            if (sightings == null) return false;

            bool found = false;

            for (int i = 0; i < sightings.Count; i++)
            {
                AnimalSighting group = sightings[i];
                if (group.Species == null) continue;
                if (species != null && group.Species != species) continue;

                double dE = group.EastM - eastM, dN = group.NorthM - northM;
                double gap = Math.Sqrt(dE * dE + dN * dN);

                if (found && gap >= distanceM) continue;

                found = true;
                nearest = group;
                distanceM = gap;

                // Atan2 of east over north, which is the compass sense — clockwise from north —
                // rather than the mathematical sense, and wrapped so that nothing reports a
                // negative bearing at anybody.
                double deg = Math.Atan2(dE, dN) * 180.0 / Math.PI;
                bearingDeg = deg < 0.0 ? deg + 360.0 : deg;
            }

            return found;
        }

        /// <summary>
        /// The groups within <paramref name="radiusM"/> of a point, at this hour, where every square feeds the same.
        ///
        /// <para>Pure: the same arguments give the same animals standing in the same places, in any
        /// order, on any machine. Nothing here is remembered between calls.</para>
        /// </summary>
        public List<AnimalSighting> Near(AnimalSpecies species, double capacityPerKm2,
                                         double eastM, double northM, double radiusM,
                                         double hourOfDay, double daylightHours,
                                         double dayOfYear = 0.0)
        {
            if (!(capacityPerKm2 > 0.0)) return new List<AnimalSighting>();
            return Near(species, (cellX, cellZ) => capacityPerKm2, eastM, northM, radiusM, hourOfDay, daylightHours, dayOfYear);
        }

        /// <summary>
        /// The groups within <paramref name="radiusM"/> of a point, at this hour, each square drawn at the capacity
        /// <paramref name="capacityPerKm2OfSquare"/> gives it by its indices (M1.7a): a world's capacity differs square by
        /// square, and a square that feeds nothing holds nothing without moving its neighbours' groups.
        ///
        /// <para>Each square keeps its own seeded stream, so its group is the same whoever asks and whatever the other
        /// squares feed.</para>
        /// </summary>
        public List<AnimalSighting> Near(AnimalSpecies species, Func<int, int, double> capacityPerKm2OfSquare,
                                         double eastM, double northM, double radiusM,
                                         double hourOfDay, double daylightHours,
                                         double dayOfYear = 0.0)
        {
            var found = new List<AnimalSighting>();
            if (species == null || capacityPerKm2OfSquare == null || radiusM <= 0.0) return found;

            double activity = Activity01(species, hourOfDay, daylightHours);

            // Groups rather than individuals: a mob of eight is one thing standing on the flat,
            // not eight independent draws that happen to land together.
            int groupSize = Math.Max(1, species.TypicalGroupSize);
            double cellAreaKm2 = CellSizeM * CellSizeM / 1e6;

            // Continuous time, so a group's wander is smooth rather than stepping between hours.
            double hours = dayOfYear * 24.0 + hourOfDay;

            int minX = (int)Math.Floor((eastM - radiusM) / CellSizeM);
            int maxX = (int)Math.Floor((eastM + radiusM) / CellSizeM);
            int minZ = (int)Math.Floor((northM - radiusM) / CellSizeM);
            int maxZ = (int)Math.Floor((northM + radiusM) / CellSizeM);

            for (int cx = minX; cx <= maxX; cx++)
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                // The chance a square holds a group is its own capacity's, in groups, over its area.
                double chancePerCell = SimMath.Clamp(capacityPerKm2OfSquare(cx, cz) / groupSize * cellAreaKm2, 0.0, 1.0);
                if (!(chancePerCell > 0.0)) continue;

                var rng = new SimRandom(CellSeed(species, cx, cz));

                if (rng.NextDouble() >= chancePerCell) continue;

                // Where in its cell the group sits, and how it wanders from there. Both off the
                // same stream, so the draw order is the whole of the determinism.
                double jitterX = rng.NextDouble();
                double jitterZ = rng.NextDouble();
                double phase = rng.NextDouble() * 2.0 * Math.PI;

                double baseE = (cx + jitterX) * CellSizeM;
                double baseN = (cz + jitterZ) * CellSizeM;

                double angle = phase + 2.0 * Math.PI * hours / DriftPeriodHours;
                double e = baseE + Math.Cos(angle) * DriftRadiusM;
                double n = baseN + Math.Sin(angle) * DriftRadiusM;

                double dx = e - eastM, dz = n - northM;
                if (dx * dx + dz * dz > radiusM * radiusM) continue;

                found.Add(new AnimalSighting(species, e, n, groupSize, activity));
            }

            return found;
        }

        /// <summary>
        /// The seed for one cell and one species. Derived rather than stored, so the world's
        /// animals are a consequence of its seed and nothing has to remember them.
        /// </summary>
        private ulong CellSeed(AnimalSpecies species, int cellX, int cellZ)
        {
            ulong s = SimRandom.DeriveSeed(Seed, "presence");
            s = SimRandom.DeriveSeed(s, species.Name);

            // Fold the cell in through the same mixer the stream names use, so neighbouring cells
            // are as unrelated as distant ones — a grid whose neighbours correlate reads as rows
            // of animals, which is exactly what a spawn table looks like.
            unchecked
            {
                ulong packed = ((ulong)(uint)cellX << 32) ^ (uint)cellZ;
                return SimRandom.DeriveSeed(s, packed.ToString());
            }
        }

        private static double Mod(double v, double period)
        {
            double r = v % period;
            return r < 0.0 ? r + period : r;
        }
    }
}
