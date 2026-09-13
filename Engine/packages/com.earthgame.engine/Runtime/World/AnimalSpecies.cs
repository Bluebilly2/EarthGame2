using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>How an animal makes its living, which decides what ground feeds it.</summary>
    public enum ForageStyle
    {
        /// <summary>Grass and forbs. Wants open ground with something growing on it.</summary>
        Grazer,

        /// <summary>Shrubs and the low tree layer — leaves within reach of a standing animal.</summary>
        Browser,

        /// <summary>Takes both, and is therefore harder to starve and thinner on the ground.</summary>
        Mixed,

        /// <summary>
        /// Insects, mostly off foliage and out of litter. What every small bird here lives on.
        ///
        /// <para>v1's list held three ways of eating plants and no way of eating animals, so nothing
        /// smaller than a wombat could be described at all. An insectivore is not a browser with a
        /// different appetite: it does not eat the vegetation, it eats what the vegetation feeds.</para>
        /// </summary>
        Insectivore,

        /// <summary>
        /// What the tide leaves: the worms, shellfish and crabs of the beach and the rock platform.
        /// The shorebird's living, and the one style here that owes nothing to the plants — a coast
        /// with two shores needed a way of eating that the sea provides (M1.2, Bherwerre).
        /// </summary>
        Tideline,
    }

    /// <summary>When a kind of animal is abroad. The same clock the nights run on.</summary>
    public enum AnimalRhythm
    {
        Diurnal,
        Nocturnal,

        /// <summary>Out at dawn and dusk — which are the hours the founder is coldest.</summary>
        Crepuscular,
    }

    /// <summary>How many of them are found together.</summary>
    public enum AnimalGrouping
    {
        Solitary,
        Pairs,
        SmallHerds,
        Mobs,
    }

    /// <summary>
    /// An animal, described by what it eats and what it can stand — never by where it lives. Ported from v1
    /// (slice E5) with the species table replaced by Bherwerre's: the two kinds M1.7a stands up and the bird
    /// the dawn chorus needs (ECOSYSTEM.md, "The animals of Bherwerre").
    ///
    /// <para>The sixth link of the chain, and it obeys the law the other five obey: <b>nothing is
    /// placed</b>. Nothing here records that kangaroos are found on grassy flats. It records that
    /// a kangaroo eats grass, needs water within a few kilometres, and moves in mobs — and the
    /// flats follow, because that is where the grass is. An animal put down by a spawn table is
    /// vegetation placed by a biome lookup: the same mistake at the last level.</para>
    ///
    /// <para>The time is <b>pre-human</b>, which is canon and not decoration (CANON.md): the species are
    /// the peninsula's own, justified from the regional record, and no midden has ever been made here.</para>
    /// </summary>
    public sealed class AnimalSpecies
    {
        public string Name { get; }
        public string DisplayName { get; }
        public ForageStyle Forage { get; }
        public AnimalRhythm Rhythm { get; }
        public AnimalGrouping Grouping { get; }

        /// <summary>
        /// Animals per square kilometre on ground that feeds them as well as ground can, before
        /// water and terrain take their share.
        ///
        /// <para>A ceiling, not an expectation: real country is never at its own optimum
        /// everywhere, so the field this produces sits below this figure almost everywhere and
        /// reaches it nowhere.</para>
        /// </summary>
        public double PeakDensityPerKm2 { get; }

        /// <summary>
        /// How far from fresh water this kind can live, m. Past it the ground may be thick with feed
        /// and still hold nothing.
        /// </summary>
        public double WaterRangeM { get; }

        /// <summary>Typical number found together, used by the presence layer.</summary>
        public int TypicalGroupSize { get; }

        /// <summary>
        /// Whether this kind lives in a burrow, and so needs ground it can dig.
        ///
        /// <para>Its own fact, and it has to be. v1 first inferred it from being solitary and
        /// nocturnal — true of the wombat and of nothing else there, so the inference worked and
        /// was still wrong: a sabotage that made the wombat diurnal silently removed its need for
        /// diggable soil. Two facts that happen to travel together are not one fact. None of
        /// Bherwerre's three burrows; the property stays for the first that does.</para>
        /// </summary>
        public bool Burrows { get; }

        /// <summary>
        /// Whether this animal joins the dawn chorus.
        ///
        /// <para>Its own property for the reason <see cref="Burrows"/> is: plenty of small diurnal
        /// birds do not chorus, and singing before first light is a specific behaviour rather than
        /// a consequence of being awake.</para>
        /// </summary>
        public bool SingsAtDawn { get; }

        private AnimalSpecies(string name, string displayName, ForageStyle forage,
                              AnimalRhythm rhythm, AnimalGrouping grouping,
                              double peakDensityPerKm2, double waterRangeM, int typicalGroupSize,
                              bool burrows = false, bool singsAtDawn = false)
        {
            SingsAtDawn = singsAtDawn;
            Name = name;
            DisplayName = displayName;
            Forage = forage;
            Rhythm = rhythm;
            Grouping = grouping;
            PeakDensityPerKm2 = peakDensityPerKm2;
            WaterRangeM = waterRangeM;
            TypicalGroupSize = typicalGroupSize;
            Burrows = burrows;
        }

        // ---- the animals of Bherwerre, before people ----

        /// <summary>
        /// Eastern grey kangaroo. Grazer of open woodland and flats, out at dawn and dusk.
        ///
        /// <para><b>Band: 10–30 / km² on good south-eastern woodland.</b> Its flanks are published even
        /// where the band itself is not: aerial survey of the NSW inland plains gives 3.18 / km²
        /// across semi-arid rangeland, the poor-habitat end, while periurban populations at Coffs
        /// Harbour run 20–490 / km² (<i>Australian Mammalogy</i> AM17010), with a coastal-headland
        /// population at 5.4/ha described as being at capacity. Good pre-human woodland belongs
        /// between those, and 30 / km² as a ceiling puts the realised field inside the band.</para>
        /// </summary>
        public static readonly AnimalSpecies EasternGreyKangaroo = new AnimalSpecies(
            "EasternGreyKangaroo", "eastern grey kangaroo", ForageStyle.Grazer,
            AnimalRhythm.Crepuscular, AnimalGrouping.Mobs, 30.0, 4000.0, 8);

        /// <summary>
        /// Pied oystercatcher. The shorebird of the tideline: a pair works a stretch of beach and
        /// rock platform by day, prising shellfish and probing the wet sand, and nests above the
        /// high-water mark.
        ///
        /// <para><b>Density derived from territory, and the derivation is the citation.</b> The species
        /// is counted as breeding pairs per kilometre of shore (a threatened species in New South
        /// Wales; its recovery plan and the NSW threatened-species profile put pairs a kilometre or
        /// more apart on ocean beaches). One pair per kilometre of shore, on the two-hundred-metre
        /// strip of beach and platform a pair actually works, is ten birds per square kilometre of
        /// that strip — a ceiling for the best shore, as every figure here is. Fresh water does not
        /// enter: it drinks from what it eats and from the sea, so its range from fresh water is
        /// unbounded and <see cref="AnimalCapacity"/> asks the tideline no question about it.</para>
        /// </summary>
        public static readonly AnimalSpecies PiedOystercatcher = new AnimalSpecies(
            "PiedOystercatcher", "pied oystercatcher", ForageStyle.Tideline,
            AnimalRhythm.Diurnal, AnimalGrouping.Pairs, 10.0, double.PositiveInfinity, 2);

        /// <summary>
        /// Superb fairy-wren. The small bird of the heath and the forest edge, and the voice of the dawn.
        ///
        /// <para><b>Density derived from territory, and the derivation is the citation.</b> The
        /// published quantities for <i>Malurus cyaneus</i> are territory size and group size, not
        /// birds per square kilometre — colour-banded populations are counted in groups holding
        /// ground. Territories run from about 0.4 to 4.5 ha with one to two hectares usual in good
        /// habitat, and a group is a breeding pair with helpers, two to five birds. Four birds on
        /// 2.5 ha is 160 per km², four on 1.5 ha is 270; 250 is the top of that band and is meant to
        /// be, since <see cref="AnimalCapacity"/> scales it down by forage, water and slope.</para>
        ///
        /// <para>Sedentary and tiny-ranged: a wren works a patch of dense low cover and does not
        /// cross open country, so its distance to water is three hundred metres rather than the
        /// kilometres a kangaroo will walk. <see cref="AnimalGrouping.SmallHerds"/> is the closest
        /// the enum comes to a family party.</para>
        /// </summary>
        public static readonly AnimalSpecies SuperbFairyWren = new AnimalSpecies(
            "SuperbFairyWren", "superb fairy-wren", ForageStyle.Insectivore,
            AnimalRhythm.Diurnal, AnimalGrouping.SmallHerds, 250.0, 300.0, 4,
            singsAtDawn: true);

        private static readonly AnimalSpecies[] AllSpecies =
        {
            EasternGreyKangaroo, PiedOystercatcher, SuperbFairyWren,
        };

        public static IReadOnlyList<AnimalSpecies> All => AllSpecies;

        /// <summary>
        /// The largest <see cref="TypicalGroupSize"/> any of these kinds has — the kangaroo's mob of
        /// eight, until a species with a bigger one is added here. Derived rather than written down,
        /// because a drawing budget for "a whole group" typed as a number would be this fact kept in
        /// two places (v1 drew six of a mob of eight for that reason).
        /// </summary>
        public static readonly int LargestTypicalGroupSize = LargestGroup();

        private static int LargestGroup()
        {
            int most = 1;
            for (int i = 0; i < AllSpecies.Length; i++)
                if (AllSpecies[i].TypicalGroupSize > most) most = AllSpecies[i].TypicalGroupSize;
            return most;
        }

        public static AnimalSpecies ByName(string name)
        {
            for (int i = 0; i < AllSpecies.Length; i++)
                if (AllSpecies[i].Name == name) return AllSpecies[i];
            return null;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// How many animals a piece of ground will hold — the population the vegetation supports, or
    /// the tide.
    ///
    /// <para><b>Equilibrium, not dynamics.</b> This is the number the plants would settle at, not
    /// the year-by-year eating and dying that settles it there. Growth, drought response and
    /// predation are M1.7's and later, and when they arrive they replace this with the thing that
    /// produces it.</para>
    ///
    /// <para><b>It asks the plant layer rather than re-deriving it.</b> Forage comes out of
    /// <see cref="PlantCommunity.TotalSuitability"/> — the same scoring that decides which plants
    /// stand here — so an animal cannot come to disagree with the vegetation it is eating. Change
    /// a plant's moisture tolerance and the herds move, with nobody editing this file. And it asks
    /// it the way the community answers: where tall timber stands, the layers beneath are scored in
    /// its shade, as the understory draw scores them.</para>
    /// </summary>
    public static class AnimalCapacity
    {
        /// <summary>
        /// What a grazer's feed is made of. Grass first and forbs second, because a kangaroo lives
        /// on the sward and takes herbs as they come rather than the other way round.
        /// </summary>
        public const double GrassShare = 0.75;

        /// <summary>
        /// The standing crop, summed across the five plant layers, at which a site feeds an
        /// insectivore as well as it ever will. Out of a possible five.
        ///
        /// <para>Not a citation: nothing in this world models invertebrate abundance, and no
        /// reference would honestly convert plant suitability into insect biomass. So the number is
        /// made <i>arguable</i> instead of hidden in a multiplier: a site whose five layers total
        /// about 2.3 — moderately good in every layer, or excellent in two or three — is as good as
        /// it gets for a bird that eats insects. A person who knows country can call that wrong.
        /// The relationship underneath is real: invertebrate biomass tracks primary productivity.</para>
        /// </summary>
        public const double FullyFeedingLayerTotal = 2.3;

        /// <summary>
        /// Tree suitability at and above which the ground beneath is scored as shaded: the canopy
        /// the community's own draw would put there.
        /// </summary>
        public const double CanopyStands = 0.5;

        /// <summary>
        /// How far from the water's edge a shorebird works, m: the beach from the wrack line to the
        /// low tide, and the platform. Past it the tideline feeds nothing.
        /// </summary>
        public const double TidelineReachM = 300.0;

        /// <summary>
        /// Soil a burrowing animal needs to get underground, m. Below this the ground is rock with
        /// a skin on it and a burrower cannot live there however good the grass is.
        /// </summary>
        public const double BurrowSoilDepthM = 0.45;

        /// <summary>
        /// How steep is too steep. Grazing animals work slopes but not cliffs, and the plants have
        /// their own limit well before this — so it bites only on ground the plants tolerate and
        /// the animals do not.
        /// </summary>
        public const double MaxWorkableSlope = 0.6;

        /// <summary>Animals per square kilometre this ground supports, at equilibrium.</summary>
        /// <param name="waterDistanceM">
        /// Metres to the nearest fresh water. The drainage layer knows it; this only asks. Infinity
        /// is a legitimate answer and means "none found", which empties the ground of everything
        /// that has to drink.
        /// </param>
        /// <param name="shoreDistanceM">Metres to the tideline, for the kinds that live off it; infinity inland.</param>
        public static double PerKm2(AnimalSpecies species, in PlantSite site, double waterDistanceM,
                                    double shoreDistanceM = double.PositiveInfinity)
        {
            if (species == null) return 0.0;

            double forage = ForageScore(species.Forage, site, shoreDistanceM);
            if (forage <= 0.0) return 0.0;

            // Water is a veto with a soft edge rather than a fence: the ground thins out as the
            // walk to water lengthens and empties past the species' range. The tideline's water is
            // the sea, and "no fresh water found" empties nothing there.
            double water = species.Forage == ForageStyle.Tideline ? 1.0 : WaterFactor(waterDistanceM, species.WaterRangeM);
            if (water <= 0.0) return 0.0;

            // Ground an animal cannot work, which is not the same question the plants answered.
            double slope = site.Slope >= MaxWorkableSlope
                ? 0.0
                : 1.0 - site.Slope / MaxWorkableSlope;

            // A burrower needs somewhere to dig. Nothing in the plant layer asks this, because a
            // tussock does not care whether there is a metre of soil or ten centimetres so long as
            // its roots fit — and a burrower very much does.
            double ground = species.Burrows
                ? SimMath.Clamp01(site.SoilDepthM / BurrowSoilDepthM)
                : 1.0;

            return species.PeakDensityPerKm2 * forage * water * slope * ground;
        }

        /// <summary>
        /// How much feed of the right kind this ground grows, 0 to 1 — read off the plant layer's
        /// own suitability rather than worked out again here, with the layers under a standing
        /// canopy scored in its shade; or, for the tideline, how near the shore is.
        /// </summary>
        public static double ForageScore(ForageStyle style, in PlantSite site, double shoreDistanceM = double.PositiveInfinity)
        {
            if (style == ForageStyle.Tideline)
            {
                if (double.IsNaN(shoreDistanceM) || shoreDistanceM < 0.0) return 0.0;
                return SimMath.Clamp01(1.0 - shoreDistanceM / TidelineReachM);
            }

            double tall = PlantCommunity.TotalSuitability(site, PlantForm.Tree);
            PlantSite below = site;
            if (tall >= CanopyStands) below.Shaded = true;

            double grass = PlantCommunity.TotalSuitability(below, PlantForm.Grass);
            double herb = PlantCommunity.TotalSuitability(below, PlantForm.Herb);

            // Browse is what a standing animal can reach: the shrub layer and the low trees. A
            // mature blackbutt's canopy is thirty metres up and might as well be weather — which is
            // why the tall-tree score is deliberately absent from this sum, and why a closed forest
            // of big timber feeds a browser far less than its greenness suggests.
            double shrub = PlantCommunity.TotalSuitability(below, PlantForm.Shrub);
            double lowTree = PlantCommunity.TotalSuitability(below, PlantForm.SmallTree);

            double graze = grass * GrassShare + herb * (1.0 - GrassShare);
            double browse = Math.Max(shrub, lowTree);

            switch (style)
            {
                case ForageStyle.Grazer: return SimMath.Clamp01(graze);
                case ForageStyle.Browser: return SimMath.Clamp01(browse);

                case ForageStyle.Insectivore:
                {
                    // An insectivore is fed by the whole standing crop rather than by any one layer
                    // of it, because insects live on all of them. The tall canopy counts here where
                    // it does not for a browser: a small bird works the foliage thirty metres up as
                    // readily as the shrub beside it.
                    double standingCrop = grass + herb + shrub + lowTree + tall;
                    return SimMath.Clamp01(standingCrop / FullyFeedingLayerTotal);
                }

                default: return SimMath.Clamp01(0.5 * (graze + browse));
            }
        }

        /// <summary>
        /// What the walk to water costs a population, 0 to 1. Full value close in, falling away to
        /// nothing at the species' range.
        /// </summary>
        public static double WaterFactor(double distanceM, double rangeM)
        {
            if (double.IsNaN(distanceM)) return 0.0;
            if (distanceM <= 0.0) return 1.0;
            if (rangeM <= 0.0) return 0.0;

            // Stated rather than left to the clamp below, which would say the same: past its range, nothing.
            if (double.IsInfinity(distanceM) || distanceM >= rangeM) return 0.0;

            // Flat out to a third of the range, then falling. Animals do not ration a short walk
            // and cannot afford a long one.
            const double Comfortable = 1.0 / 3.0;
            double t = distanceM / rangeM;
            if (t <= Comfortable) return 1.0;

            return SimMath.Clamp01((1.0 - t) / (1.0 - Comfortable));
        }
    }
}
