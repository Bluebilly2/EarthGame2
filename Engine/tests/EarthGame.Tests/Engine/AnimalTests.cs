using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// v1's AnimalTests (slice E5), ported with the capacity and the presence and re-read for Bherwerre's
    /// three animals (M1.2 promise 5). Someone who can read country should be able to say "roos will be on
    /// that flat at dusk" and be right because it is a consequence: these tests never assert that kangaroos
    /// are on flats. They assert that grass feeds grazers and that flats grow grass, and let the flat follow.
    /// </summary>
    public sealed class AnimalTests
    {
        /// <summary>A grassy flat: open, dry enough to keep the blackbutts off, on sand too thin for them.</summary>
        private static PlantSite GrassyFlat() => new PlantSite { Wetness = 0.45, SoilDepthM = 0.35, Slope = 0.03, Exposure = 0.55, Shaded = false };

        /// <summary>A bare crest: a skin of sand on rock, dry and windy.</summary>
        private static PlantSite BareCrest() => new PlantSite { Wetness = 0.10, SoilDepthM = 0.05, Slope = 0.30, Exposure = 0.98, Shaded = false };

        /// <summary>The hind dune: deep moist sand under blackbutt.</summary>
        private static PlantSite HindDuneForest() => new PlantSite { Wetness = 0.55, SoilDepthM = 1.20, Slope = 0.05, Exposure = 0.10, Shaded = false };

        /// <summary>The coast's twelve, whose suitability the sites here are fed by (WG.2c: a region's plants).</summary>
        private static readonly IReadOnlyList<PlantSpecies> Coast = Region.Bherwerre.Plants;

        private const double AtWater = 300.0;
        private const double AugustDaylightHours = 10.6;

        [Test]
        public void KangarooDensityLandsInTheBandOnGoodGround()
        {
            double flat = AnimalCapacity.PerKm2(AnimalSpecies.EasternGreyKangaroo, GrassyFlat(), Coast, AtWater);
            Assert.That(flat, Is.InRange(10.0, 30.0), "a grassy flat within reach of water gave " + flat.ToString("F1") + " kangaroos per km², against the band of 10–30");

            double crest = AnimalCapacity.PerKm2(AnimalSpecies.EasternGreyKangaroo, BareCrest(), Coast, AtWater);
            Assert.That(crest, Is.LessThan(2.0), "and a bare crest must fall toward nothing, not merely thin: got " + crest.ToString("F2") + " per km²");
            Assert.That(flat, Is.GreaterThan(crest * 5.0), "the flat must beat the crest by a wide margin or the capacity field is flat country wearing an ecology");
        }

        [Test]
        public void CapacityFollowsTheForageAndNotThePlace()
        {
            PlantSite flat = GrassyFlat(), crest = BareCrest();
            double grassOnFlat = PlantCommunity.TotalSuitability(flat, PlantForm.Grass, Coast);
            double grassOnCrest = PlantCommunity.TotalSuitability(crest, PlantForm.Grass, Coast);
            Assert.That(grassOnFlat, Is.GreaterThan(grassOnCrest), "the premise: the flat must actually grow more grass than the crest");

            double grazerFlat = AnimalCapacity.ForageScore(ForageStyle.Grazer, flat, Coast);
            double grazerCrest = AnimalCapacity.ForageScore(ForageStyle.Grazer, crest, Coast);
            Assert.That(grazerFlat, Is.GreaterThan(grazerCrest), "so the grazer's feed must follow it");

            bool everDisagree = false;
            foreach (PlantSite site in new[] { flat, crest, HindDuneForest() })
            {
                double g = AnimalCapacity.ForageScore(ForageStyle.Grazer, site, Coast);
                double b = AnimalCapacity.ForageScore(ForageStyle.Browser, site, Coast);
                if (Math.Abs(g - b) > 0.05) { everDisagree = true; break; }
            }
            Assert.That(everDisagree, Is.True, "grazing and browsing must be able to rank the same ground differently, or the forage styles are a label rather than a model");
        }

        [Test]
        public void TheLayersUnderTallTimberAreFedInItsShade()
        {
            // Under the blackbutts the shrub layer is scored shaded, as the community's own understory draw
            // scores it; a browser finds little, an insectivore finds the whole standing crop, canopy included.
            PlantSite forest = HindDuneForest();
            double timber = PlantCommunity.TotalSuitability(forest, PlantForm.Tree, Coast);
            Assert.That(timber, Is.GreaterThan(AnimalCapacity.CanopyStands), "the premise: a canopy stands here (" + timber.ToString("F2") + ")");

            PlantSite openScore = forest;
            openScore.Shaded = false;
            double shrubsInTheOpen = PlantCommunity.TotalSuitability(openScore, PlantForm.Shrub, Coast);
            double browse = AnimalCapacity.ForageScore(ForageStyle.Browser, forest, Coast);
            Assert.That(browse, Is.LessThan(shrubsInTheOpen * 0.5), "the shrubs a browser is fed by are the shaded ones: " + browse.ToString("F2") + " against " + shrubsInTheOpen.ToString("F2") + " unshaded");

            double insects = AnimalCapacity.ForageScore(ForageStyle.Insectivore, forest, Coast);
            Assert.That(insects, Is.GreaterThan(browse * 2.0), "and the canopy feeds the wren where it cannot feed a browser");
        }

        [Test]
        public void TheShorebirdLivesOnTheTidelineAndNowhereElse()
        {
            AnimalSpecies bird = AnimalSpecies.PiedOystercatcher;
            PlantSite beach = new PlantSite { Wetness = 0.2, SoilDepthM = 0.02, Slope = 0.05, Exposure = 1.0, Shaded = false };

            double onTheShore = AnimalCapacity.PerKm2(bird, beach, Coast, AtWater, shoreDistanceM: 20.0);
            double inland = AnimalCapacity.PerKm2(bird, GrassyFlat(), Coast, AtWater, shoreDistanceM: 2000.0);
            double unstated = AnimalCapacity.PerKm2(bird, GrassyFlat(), Coast, AtWater);

            Assert.That(onTheShore, Is.GreaterThan(bird.PeakDensityPerKm2 * 0.8), "the beach at its best holds the pair: " + onTheShore.ToString("F1"));
            Assert.That(inland, Is.EqualTo(0.0), "two kilometres from the water's edge there is nothing for it");
            Assert.That(unstated, Is.EqualTo(0.0), "and no shore stated is no shore");
            Assert.That(AnimalCapacity.PerKm2(bird, beach, Coast, double.PositiveInfinity, shoreDistanceM: 20.0), Is.GreaterThan(0.0), "fresh water does not enter: it drinks from what it eats");
            Assert.That(AnimalCapacity.PerKm2(AnimalSpecies.EasternGreyKangaroo, beach, Coast, AtWater, shoreDistanceM: 20.0), Is.LessThan(1.0), "while the bare beach feeds no kangaroo");
        }

        [Test]
        public void TheWalkToWaterEmptiesTheGroundEventually()
        {
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            PlantSite flat = GrassyFlat();

            double near = AnimalCapacity.PerKm2(roo, flat, Coast, 200.0);
            double mid = AnimalCapacity.PerKm2(roo, flat, Coast, roo.WaterRangeM * 0.7);
            double far = AnimalCapacity.PerKm2(roo, flat, Coast, roo.WaterRangeM * 1.2);

            Assert.That(near, Is.GreaterThan(mid), "the walk must cost something before it costs everything");
            Assert.That(mid, Is.GreaterThan(0.0), "and must not be a cliff at the comfortable distance");
            Assert.That(far, Is.EqualTo(0.0), "past its range the best grass in the world holds nothing");
            Assert.That(AnimalCapacity.PerKm2(roo, flat, Coast, double.PositiveInfinity), Is.EqualTo(0.0), "and 'no water found' is a legitimate answer that empties the ground");
        }

        [Test]
        public void ThePresenceOfAnimalsIsAFunctionAndReplaysExactly()
        {
            var a = new AnimalPresence(4001UL);
            var b = new AnimalPresence(4001UL);
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;

            var forward = new List<AnimalSighting>();
            for (double h = 5.0; h < 9.0; h += 0.5)
                forward.AddRange(a.Near(roo, 25.0, 1200.0, -800.0, 400.0, h, AugustDaylightHours));

            var backward = new List<AnimalSighting>();
            for (double h = 8.5; h >= 5.0; h -= 0.5)
                backward.InsertRange(0, b.Near(roo, 25.0, 1200.0, -800.0, 400.0, h, AugustDaylightHours));

            Assert.That(forward.Count, Is.GreaterThan(0), "there must be animals to compare");
            Assert.That(backward.Count, Is.EqualTo(forward.Count), "the same hours asked in the other order must find the same number");
            for (int i = 0; i < forward.Count; i++)
            {
                Assert.That(backward[i].EastM, Is.EqualTo(forward[i].EastM), "sighting " + i + " must stand in exactly the same place");
                Assert.That(backward[i].NorthM, Is.EqualTo(forward[i].NorthM));
            }

            var other = new AnimalPresence(4002UL);
            var elsewhere = other.Near(roo, 25.0, 1200.0, -800.0, 400.0, 6.0, AugustDaylightHours);
            var here = a.Near(roo, 25.0, 1200.0, -800.0, 400.0, 6.0, AugustDaylightHours);
            bool differs = elsewhere.Count != here.Count;
            for (int i = 0; !differs && i < here.Count; i++)
                differs = Math.Abs(elsewhere[i].EastM - here[i].EastM) > 1e-9;
            Assert.That(differs, Is.True, "a different seed must give a different set of animals");
        }

        [Test]
        public void EachKindIsAbroadInItsOwnHours()
        {
            const double Daylight = AugustDaylightHours;
            double sunrise = 12.0 - Daylight * 0.5;
            double sunset = 12.0 + Daylight * 0.5;

            double wrenAtNoon = AnimalPresence.Activity01(AnimalSpecies.SuperbFairyWren, 12.0, Daylight);
            double wrenAtMidnight = AnimalPresence.Activity01(AnimalSpecies.SuperbFairyWren, 0.0, Daylight);
            Assert.That(wrenAtNoon, Is.GreaterThan(0.8), "the wren works by day");
            Assert.That(wrenAtMidnight, Is.LessThan(0.15), "and not at midnight");

            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            double atDawn = AnimalPresence.Activity01(roo, sunrise, Daylight);
            double atDusk = AnimalPresence.Activity01(roo, sunset, Daylight);
            double atNoon = AnimalPresence.Activity01(roo, 12.0, Daylight);
            double atMidnight = AnimalPresence.Activity01(roo, 0.0, Daylight);
            Assert.That(atDawn, Is.GreaterThan(0.8), "roos are up at first light");
            Assert.That(atDusk, Is.GreaterThan(0.8), "and again at dusk");
            Assert.That(atNoon, Is.LessThan(0.5), "and lying up through the middle of the day");
            Assert.That(atMidnight, Is.LessThan(0.5), "and through the small hours");
            Assert.That(atNoon, Is.LessThan(Math.Min(atDawn, atDusk) * 0.6), "the midday trough must be deep enough to make dawn and dusk two peaks rather than one broad day");
        }

        [Test]
        public void TheRhythmFollowsTheSeasonsSunrise()
        {
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            const double Winter = 10.0, Summer = 15.0;
            double winterDawn = 12.0 - Winter * 0.5;
            double summerDawn = 12.0 - Summer * 0.5;
            Assert.That(AnimalPresence.Activity01(roo, winterDawn, Winter), Is.GreaterThan(0.8), "at seven in a ten-hour winter day the roos are up");
            Assert.That(AnimalPresence.Activity01(roo, winterDawn, Summer), Is.LessThan(0.5), "but seven o'clock in a fifteen-hour summer day is two and a half hours after first light, and they are already lying up");
            Assert.That(AnimalPresence.Activity01(roo, summerDawn, Summer), Is.GreaterThan(0.8), "half past four in summer is their dawn instead");
        }

        [Test]
        public void TheChorusIsBeforeSunriseAndNotAtNoon()
        {
            const double Daylight = AugustDaylightHours;
            double sunrise = 12.0 - Daylight * 0.5;
            AnimalSpecies wren = AnimalSpecies.SuperbFairyWren;
            double beforeSunrise = AnimalPresence.DawnChorus01(wren, sunrise - AnimalPresence.ChorusPeakBeforeSunriseHours, Daylight);
            double atNoon = AnimalPresence.DawnChorus01(wren, 12.0, Daylight);
            double atSunset = AnimalPresence.DawnChorus01(wren, 12.0 + Daylight * 0.5, Daylight);
            Assert.That(beforeSunrise, Is.GreaterThan(0.95), "the chorus peaks before first light");
            Assert.That(atNoon, Is.LessThan(0.05), "and is silent at noon, when the wren is most active");
            Assert.That(atSunset, Is.InRange(0.2, 0.5), "with a smaller reprise at dusk");
            Assert.That(AnimalPresence.DawnChorus01(AnimalSpecies.EasternGreyKangaroo, sunrise, Daylight), Is.EqualTo(0.0), "a kangaroo does not sing");
        }

        [Test]
        public void SightingsIntegrateBackToTheDensity()
        {
            var presence = new AnimalPresence(90210UL);
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            const double Capacity = 24.0;
            const double Radius = 150.0;
            const double Daylight = AugustDaylightHours;
            double sunset = 12.0 + Daylight * 0.5;
            double activity = AnimalPresence.Activity01(roo, sunset, Daylight);
            Assert.That(activity, Is.GreaterThan(0.9), "the walk is taken at their busiest hour");

            const int Samples = 900;
            const double Step = 2.0 * Radius + 20.0;
            int individuals = 0;
            for (int i = 0; i < Samples; i++)
            {
                double east = 5000.0 + i * Step;
                foreach (AnimalSighting s in presence.Near(roo, Capacity, east, 2000.0, Radius, sunset, Daylight))
                    individuals += s.GroupSize;
            }
            double areaKm2 = Samples * Math.PI * Radius * Radius / 1e6;
            double measured = individuals / areaKm2;
            Assert.That(individuals, Is.GreaterThan(100), "the transect must actually meet animals: " + individuals + " over " + areaKm2.ToString("F2") + " km²");
            Assert.That(measured, Is.EqualTo(Capacity).Within(Capacity * 0.25), "walking " + areaKm2.ToString("F1") + " km² of ground rated at " + Capacity.ToString("F0") + " per km² met " + measured.ToString("F1") + " per km²");
        }

        [Test]
        public void AMobGrazesAcrossAFlatOverAnHour()
        {
            var presence = new AnimalPresence(7UL);
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            const double Radius = 600.0;
            double sunset = 12.0 + AugustDaylightHours * 0.5;

            var atFirst = presence.Near(roo, 25.0, 0.0, 0.0, Radius, sunset, AugustDaylightHours);
            Assert.That(atFirst.Count, Is.GreaterThan(0), "there must be a mob to watch");
            var anHourLater = presence.Near(roo, 25.0, 0.0, 0.0, Radius, sunset + 1.0, AugustDaylightHours);

            AnimalSighting before = atFirst[0];
            double moved = -1.0;
            foreach (AnimalSighting after in anHourLater)
            {
                double d = Math.Sqrt((after.EastM - before.EastM) * (after.EastM - before.EastM) + (after.NorthM - before.NorthM) * (after.NorthM - before.NorthM));
                if (d < 2.0 * AnimalPresence.DriftRadiusM && (moved < 0.0 || d < moved)) moved = d;
            }
            Assert.That(moved, Is.GreaterThan(5.0), "the mob must have grazed somewhere over the hour, not stood on its mark");
            Assert.That(moved, Is.LessThan(2.0 * AnimalPresence.DriftRadiusM + 1.0), "and it must wander rather than teleport: moved " + moved.ToString("F0") + " m");
        }

        [Test]
        public void TheSpeciesSpanTheNiches()
        {
            Assert.That(AnimalSpecies.All.Count, Is.EqualTo(3), "the grazer M1.7 materialises, the shorebird on the tideline, and the bird that feeds the dawn chorus; if you added one, say here what niche it opened");
            var rhythms = new HashSet<AnimalRhythm>();
            var styles = new HashSet<ForageStyle>();
            foreach (AnimalSpecies s in AnimalSpecies.All)
            {
                rhythms.Add(s.Rhythm);
                styles.Add(s.Forage);
                Assert.That(s.PeakDensityPerKm2, Is.GreaterThan(0.0), s.Name + " must be able to exist");
                Assert.That(s.WaterRangeM, Is.GreaterThan(0.0), s.Name + " must have a range from water");
                Assert.That(AnimalSpecies.ByName(s.Name), Is.Not.Null, s.Name + " must be findable by name");
            }
            Assert.That(rhythms.Count, Is.GreaterThanOrEqualTo(2), "day and twilight must both be represented, or the day has no shape to it");
            Assert.That(styles.Count, Is.EqualTo(3), "grazer, tideline and insectivore: three ways of living, none of them a label");
            Assert.That(AnimalSpecies.LargestTypicalGroupSize, Is.EqualTo(8), "the mob is the largest group here");
        }

        private static double BearingTo(double dE, double dN)
        {
            var one = new[] { new AnimalSighting(AnimalSpecies.EasternGreyKangaroo, dE, dN, 3, 1.0) };
            Assert.That(AnimalPresence.Nearest(one, null, 0.0, 0.0, out AnimalSighting _, out double _, out double bearing), Is.True, "a list with a group in it must find that group");
            return bearing;
        }

        [Test]
        public void TheBearingIsClockwiseFromNorthAndNeverNegative()
        {
            Assert.That(BearingTo(0.0, 100.0), Is.EqualTo(0.0).Within(0.001), "due north is 0");
            Assert.That(BearingTo(100.0, 0.0), Is.EqualTo(90.0).Within(0.001), "due east is 90, not -90 and not 270");
            Assert.That(BearingTo(0.0, -100.0), Is.EqualTo(180.0).Within(0.001), "due south is 180");
            Assert.That(BearingTo(-100.0, 0.0), Is.EqualTo(270.0).Within(0.001), "due west is 270 rather than -90");
            Assert.That(BearingTo(70.0, 70.0), Is.EqualTo(45.0).Within(0.001), "north-east is 45");
            Assert.That(BearingTo(-70.0, -70.0), Is.EqualTo(225.0).Within(0.001), "south-west is 225");
        }

        [Test]
        public void TheNearestGroupIsTheClosestOneAndSaysHowFar()
        {
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            var sightings = new[]
            {
                new AnimalSighting(roo, 0.0, 300.0, 4, 1.0),
                new AnimalSighting(roo, -30.0, -40.0, 7, 1.0),
                new AnimalSighting(roo, 100.0, 0.0, 2, 1.0),
            };
            Assert.That(AnimalPresence.Nearest(sightings, null, 0.0, 0.0, out AnimalSighting nearest, out double metres, out double bearing), Is.True);
            Assert.That(nearest.GroupSize, Is.EqualTo(7), "the group of seven is the closest one");
            Assert.That(metres, Is.EqualTo(50.0).Within(0.001), "and it is fifty flat metres off, not thirty or forty");
            Assert.That(bearing, Is.EqualTo(216.87).Within(0.01), "south-west of him, in compass degrees");
        }

        [Test]
        public void ATieKeepsTheFirstGroupInTheList()
        {
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            var sightings = new[] { new AnimalSighting(roo, 0.0, 100.0, 5, 1.0), new AnimalSighting(roo, 100.0, 0.0, 9, 1.0) };
            Assert.That(AnimalPresence.Nearest(sightings, null, 0.0, 0.0, out AnimalSighting first, out double _, out double _), Is.True);
            Assert.That(first.GroupSize, Is.EqualTo(5), "the first of two equally distant groups wins");
            var reversed = new[] { sightings[1], sightings[0] };
            Assert.That(AnimalPresence.Nearest(reversed, null, 0.0, 0.0, out AnimalSighting other, out double _, out double _), Is.True);
            Assert.That(other.GroupSize, Is.EqualTo(9), "and the rule is the order, not the group");
        }

        [Test]
        public void AKindAskedForIsTheKindAnsweredAbout()
        {
            AnimalSpecies roo = AnimalSpecies.EasternGreyKangaroo;
            AnimalSpecies bird = AnimalSpecies.PiedOystercatcher;
            var sightings = new[] { new AnimalSighting(bird, 10.0, 0.0, 2, 1.0), new AnimalSighting(roo, 0.0, 200.0, 6, 1.0) };
            Assert.That(AnimalPresence.Nearest(sightings, roo, 0.0, 0.0, out AnimalSighting mob, out double far, out double _), Is.True);
            Assert.That(mob.GroupSize, Is.EqualTo(6), "asked for kangaroos, given the kangaroos");
            Assert.That(far, Is.EqualTo(200.0).Within(0.001), "at their own distance and not the bird's");
            Assert.That(AnimalPresence.Nearest(sightings, null, 0.0, 0.0, out AnimalSighting any, out double near, out double _), Is.True);
            Assert.That(any.Species, Is.SameAs(bird), "asked for anything, given the nearest thing");
            Assert.That(near, Is.EqualTo(10.0).Within(0.001));
            Assert.That(AnimalPresence.Nearest(sightings, AnimalSpecies.SuperbFairyWren, 0.0, 0.0, out AnimalSighting _, out double _, out double _), Is.False, "and a kind that is not there is absent rather than approximated by another");
        }

        [Test]
        public void NothingToSeeIsFalseRatherThanAGroupAtZeroMetres()
        {
            Assert.That(AnimalPresence.Nearest(null, null, 0.0, 0.0, out AnimalSighting none, out double gap, out double bearing), Is.False, "a null list is nothing seen");
            Assert.That(none.Species, Is.Null, "and hands back no animal at all");
            Assert.That(gap, Is.EqualTo(double.PositiveInfinity), "at no distance that could be walked");
            Assert.That(bearing, Is.EqualTo(0.0).Within(0.001));
            Assert.That(AnimalPresence.Nearest(new AnimalSighting[0], null, 0.0, 0.0, out AnimalSighting _, out double _, out double _), Is.False, "an empty list likewise");
            var withHole = new[] { default(AnimalSighting), new AnimalSighting(AnimalSpecies.EasternGreyKangaroo, 0.0, 60.0, 3, 1.0) };
            Assert.That(AnimalPresence.Nearest(withHole, null, 0.0, 0.0, out AnimalSighting real, out double metres, out double _), Is.True);
            Assert.That(real.GroupSize, Is.EqualTo(3), "the group with an animal in it wins over the hole");
            Assert.That(metres, Is.EqualTo(60.0).Within(0.001));
        }
    }
}
