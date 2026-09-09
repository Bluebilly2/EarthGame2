using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>What shape a plant is, which decides what it does to everything under it.</summary>
    public enum PlantForm
    {
        /// <summary>Full-height timber. Casts the shade everything else has to live with.</summary>
        Tree,

        /// <summary>A small tree: half the height, a fraction of the shade.</summary>
        SmallTree,

        /// <summary>Woody, waist to head high.</summary>
        Shrub,

        /// <summary>Soft, knee to waist high.</summary>
        Herb,

        /// <summary>Tussock. The ground cover under everything else.</summary>
        Grass,
    }

    /// <summary>Everything about a square metre that decides what will grow on it.</summary>
    public struct PlantSite
    {
        /// <summary>0 a shedding spur, 1 a soak.</summary>
        public double Wetness;

        /// <summary>Soil to root in, m.</summary>
        public double SoilDepthM;

        /// <summary>Rise over run.</summary>
        public double Slope;

        /// <summary>0 in the lee of a hill, 1 on an open crest or the seaward face of a dune.</summary>
        public double Exposure;

        /// <summary>Whether something taller is already standing over it.</summary>
        public bool Shaded;
    }

    /// <summary>
    /// A plant, described by what it can stand rather than by where it lives. Ported from v1 (slice E4) with
    /// the species table replaced: v1's were the dry sclerophyll plants of the Southern Highlands; these are
    /// Bherwerre's, from the dune forest to the swamp, each with its source in ECOSYSTEM.md.
    ///
    /// <para>Nothing here records that coast banksia grows on the foredune. It records that coast banksia wants
    /// dry ground, will root in almost nothing, and does not mind salt wind — and the dune follows. That is the
    /// whole difference between a world with an ecology and a world with a biome lookup (GAME_DESIGN §6, §8,
    /// and <c>Docs/ECOSYSTEM.md</c>).</para>
    ///
    /// <para>Suitability multiplies the conditions rather than averaging them, because that is how
    /// tolerance really works: one condition a plant cannot meet rules it out no matter how good
    /// the rest are. A blackbutt needing half a metre of sand does not grow in ten centimetres of it because
    /// the moisture happens to be perfect.</para>
    /// </summary>
    public sealed class PlantSpecies
    {
        public string Name { get; }
        public string DisplayName { get; }
        public PlantForm Form { get; }

        /// <summary>Where on the wet-dry gradient it does best, 0 to 1.</summary>
        public double MoistureOptimum { get; }

        /// <summary>How far either side of that it still persists.</summary>
        public double MoistureBreadth { get; }

        /// <summary>Soil it needs to root in, m.</summary>
        public double MinSoilDepthM { get; }

        /// <summary>
        /// Soil past which this plant is gone, m. Infinite for everything that is not a pioneer.
        ///
        /// <para>The other half of a tolerance. Every species here is described by what it can <b>stand</b>,
        /// and for all but one that is a floor: a blackbutt needs half a metre of sand and is absent below it.
        /// But a sand-binder is defined the other way round. <see cref="Spinifex"/> is not on a foredune
        /// because it loves bare sand; it is there because it is the only thing that will grow there, and the
        /// moment there is real soil it is beaten by everything that wanted real soil all along. A pioneer is
        /// a plant that loses wherever conditions are good.</para>
        ///
        /// <para>Competition rather than physiology, so it belongs on the plant and not in the
        /// draw: <see cref="PlantCommunity"/> already weights by suitability squared, and that alone
        /// would still hand a spur to a sand-binder scoring 0.54 against a banksia's 0.46. The
        /// ceiling is what says the sand-binder is not in that contest at all.</para>
        /// </summary>
        public double MaxSoilDepthM { get; }

        /// <summary>The steepest face it will hold on to, rise over run.</summary>
        public double MaxSlope { get; }

        /// <summary>0 needs full sun, 1 thrives under a canopy.</summary>
        public double ShadeTolerance { get; }

        /// <summary>0 needs shelter, 1 stands on an open crest in the salt wind.</summary>
        public double ExposureTolerance { get; }

        public double MinHeightM { get; }
        public double MaxHeightM { get; }

        /// <summary>
        /// Thickness of the outer bark that comes away in usable strips, m. Zero for anything
        /// without a woody trunk, and zero for a trunk whose bark does not strip.
        ///
        /// <para>The property that decides how much a standing tree can give, and it is per-species
        /// because bark is the most visibly species-specific thing about a eucalypt: a bangalay
        /// carries rough fibrous bark to its branches, a blackbutt is rough below and smooth above,
        /// and a paperbark sheds sheets that come away in the hand. Anyone who can tell those trees
        /// apart is telling them apart by this.</para>
        /// </summary>
        public double StrippableBarkM { get; }

        private PlantSpecies(string name, string displayName, PlantForm form,
                             double moistureOptimum, double moistureBreadth,
                             double minSoilDepthM, double maxSlope,
                             double shadeTolerance, double exposureTolerance,
                             double minHeightM, double maxHeightM,
                             double maxSoilDepthM = double.PositiveInfinity,
                             double strippableBarkM = 0.0)
        {
            Name = name;
            DisplayName = displayName;
            Form = form;
            MoistureOptimum = moistureOptimum;
            MoistureBreadth = moistureBreadth;
            MinSoilDepthM = minSoilDepthM;
            MaxSoilDepthM = maxSoilDepthM;
            MaxSlope = maxSlope;
            ShadeTolerance = shadeTolerance;
            ExposureTolerance = exposureTolerance;
            MinHeightM = minHeightM;
            MaxHeightM = maxHeightM;
            StrippableBarkM = strippableBarkM;
        }

        /// <summary>
        /// How well this plant can live here, 0 to 1. A product, so any single condition it cannot
        /// meet takes the whole score to nothing.
        /// </summary>
        public double Suitability(in PlantSite site)
        {
            if (site.SoilDepthM < MinSoilDepthM) return 0.0;
            if (site.Slope >= MaxSlope) return 0.0;

            // A tolerance curve rather than a band: a plant thins out away from its optimum
            // instead of stopping at a line.
            double d = (site.Wetness - MoistureOptimum) / MoistureBreadth;
            double moisture = Math.Exp(-d * d);

            // Root room. The minimum is already a veto above; past it a plant reaches full vigour
            // within a modest margin rather than crawling up to it. v1's first version divided by
            // the minimum plus 200 mm, which left a tree a quarter above its own minimum running
            // at fifteen per cent - and handed every mid slope to the smallest tree.
            double soil = SimMath.Clamp01((site.SoilDepthM - MinSoilDepthM)
                                          / Math.Max(0.10, 0.35 * MinSoilDepthM));

            // And the same ramp coming back down, for a pioneer with a ceiling. This is the whole
            // of the ceiling and there is no veto above to go with the floor's: the ramp reaches
            // zero exactly at the limit and is clamped below it, so deeper ground is already ruled
            // out by arithmetic. A veto beside it would be a second statement of one rule that
            // could never disagree because it could never fire — which a sabotage found, by
            // removing it and changing nothing.
            if (!double.IsPositiveInfinity(MaxSoilDepthM))
            {
                soil = Math.Min(soil, SimMath.Clamp01((MaxSoilDepthM - site.SoilDepthM)
                                                      / Math.Max(0.05, 0.35 * MaxSoilDepthM)));
            }

            double slope = SimMath.Clamp01((MaxSlope - site.Slope) / MaxSlope);

            // Light. Under a canopy only the shade-tolerant do well; in the open, only the
            // wind-hard stand on an exposed crest.
            double light = site.Shaded
                ? ShadeTolerance
                : 1.0 - 0.6 * (1.0 - ExposureTolerance) * site.Exposure;

            return SimMath.Clamp01(moisture * soil * slope * SimMath.Clamp01(light));
        }

        /// <summary>How tall an individual of this species grows here, m.</summary>
        public double HeightAt(in PlantSite site, double roll)
        {
            double vigour = Suitability(site);
            double t = SimMath.Clamp01(roll) * 0.5 + vigour * 0.5;
            return MinHeightM + (MaxHeightM - MinHeightM) * t;
        }

        // ---- the plants of Bherwerre: the dune forest, the heath, the swamp (ECOSYSTEM.md, "The plants of Bherwerre") ----

        /// <summary>Blackbutt. The tall forest on the deeper, moist sands behind the dunes; the timber of this coast.</summary>
        public static readonly PlantSpecies Blackbutt = new PlantSpecies(
            "Blackbutt", "blackbutt", PlantForm.Tree,
            moistureOptimum: 0.55, moistureBreadth: 0.24,
            minSoilDepthM: 0.50, maxSlope: 0.55,
            shadeTolerance: 0.30, exposureTolerance: 0.35,
            minHeightM: 20.0, maxHeightM: 40.0,
            strippableBarkM: 0.008);          // rough and fibrous below, smooth above; comes away in short slabs

        /// <summary>Bangalay. The eucalypt nearest the sea and around the swamps: sand, salt wind and wet feet.</summary>
        public static readonly PlantSpecies Bangalay = new PlantSpecies(
            "Bangalay", "bangalay", PlantForm.Tree,
            moistureOptimum: 0.65, moistureBreadth: 0.28,
            minSoilDepthM: 0.40, maxSlope: 0.50,
            shadeTolerance: 0.30, exposureTolerance: 0.75,
            minHeightM: 12.0, maxHeightM: 25.0,
            strippableBarkM: 0.012);          // rough fibrous bark to the branches

        /// <summary>Old-man banksia. The heathy woodland on dry sand behind the dunes, out of the worst wind.</summary>
        public static readonly PlantSpecies OldManBanksia = new PlantSpecies(
            "OldManBanksia", "old-man banksia", PlantForm.SmallTree,
            moistureOptimum: 0.35, moistureBreadth: 0.20,
            minSoilDepthM: 0.15, maxSlope: 0.60,
            shadeTolerance: 0.20, exposureTolerance: 0.60,
            minHeightM: 4.0, maxHeightM: 12.0);

        /// <summary>Coast banksia. The seaward face of the dune, where nothing else woody stands the salt.</summary>
        public static readonly PlantSpecies CoastBanksia = new PlantSpecies(
            "CoastBanksia", "coast banksia", PlantForm.SmallTree,
            moistureOptimum: 0.30, moistureBreadth: 0.22,
            minSoilDepthM: 0.12, maxSlope: 0.60,
            shadeTolerance: 0.15, exposureTolerance: 0.98,
            minHeightM: 5.0, maxHeightM: 15.0);

        /// <summary>Swamp paperbark. The rim of the swamp and the lake, feet in the water; bark in sheets.</summary>
        public static readonly PlantSpecies SwampPaperbark = new PlantSpecies(
            "SwampPaperbark", "swamp paperbark", PlantForm.SmallTree,
            moistureOptimum: 0.92, moistureBreadth: 0.14,
            minSoilDepthM: 0.20, maxSlope: 0.40,
            shadeTolerance: 0.35, exposureTolerance: 0.50,
            minHeightM: 3.0, maxHeightM: 9.0,
            strippableBarkM: 0.015);          // papery layers that come away by the sheet

        /// <summary>Grass tree. Poor sandy heath in full sun, and the fire drill's spindle.</summary>
        public static readonly PlantSpecies GrassTree = new PlantSpecies(
            "GrassTree", "grass tree", PlantForm.Shrub,
            moistureOptimum: 0.30, moistureBreadth: 0.24,
            minSoilDepthM: 0.06, maxSlope: 0.65,
            shadeTolerance: 0.10, exposureTolerance: 0.85,
            minHeightM: 1.0, maxHeightM: 2.5);

        /// <summary>Heath banksia. The heath itself, head high and dense, on the poorer and damper sands.</summary>
        public static readonly PlantSpecies HeathBanksia = new PlantSpecies(
            "HeathBanksia", "heath banksia", PlantForm.Shrub,
            moistureOptimum: 0.45, moistureBreadth: 0.30,
            minSoilDepthM: 0.10, maxSlope: 0.65,
            shadeTolerance: 0.20, exposureTolerance: 0.90,
            minHeightM: 1.5, maxHeightM: 4.0);

        /// <summary>Bracken. Wants the shade, so it grows under things rather than beside them.</summary>
        public static readonly PlantSpecies Bracken = new PlantSpecies(
            "Bracken", "bracken", PlantForm.Herb,
            moistureOptimum: 0.65, moistureBreadth: 0.26,
            minSoilDepthM: 0.20, maxSlope: 0.65,
            shadeTolerance: 0.95, exposureTolerance: 0.20,
            minHeightM: 0.5, maxHeightM: 1.4);

        /// <summary>Lomandra. The dune toe, the forest floor and the creek edge, and the fibre the first cordage comes from.</summary>
        public static readonly PlantSpecies Lomandra = new PlantSpecies(
            "Lomandra", "lomandra", PlantForm.Herb,
            moistureOptimum: 0.70, moistureBreadth: 0.28,
            minSoilDepthM: 0.05, maxSlope: 0.70,
            shadeTolerance: 0.60, exposureTolerance: 0.60,
            minHeightM: 0.4, maxHeightM: 1.0);

        /// <summary>Saw-sedge. The sedgeland of the swamp and the lake shore, where the ground is water half the year.</summary>
        public static readonly PlantSpecies SawSedge = new PlantSpecies(
            "SawSedge", "saw-sedge", PlantForm.Herb,
            moistureOptimum: 0.95, moistureBreadth: 0.12,
            minSoilDepthM: 0.05, maxSlope: 0.50,
            shadeTolerance: 0.40, exposureTolerance: 0.60,
            minHeightM: 0.6, maxHeightM: 1.5);

        /// <summary>Kangaroo grass. Everywhere the trees and the heath are not, and the first bedding.</summary>
        public static readonly PlantSpecies KangarooGrass = new PlantSpecies(
            "KangarooGrass", "kangaroo grass", PlantForm.Grass,
            moistureOptimum: 0.40, moistureBreadth: 0.34,
            minSoilDepthM: 0.05, maxSlope: 0.72,
            shadeTolerance: 0.30, exposureTolerance: 0.90,
            minHeightM: 0.3, maxHeightM: 0.8);

        /// <summary>
        /// Spinifex, the sand-binder of the foredune — and the first fibre a founder waking on a beach can reach.
        ///
        /// <para>It earns its place by tolerance rather than by address, like everything else here. It roots in
        /// almost pure sand, needs full sun, and does not care about salt wind — nothing in this list stands more
        /// of it. What it cannot do is compete: <see cref="MaxSoilDepthM"/> takes it out wherever real soil has
        /// formed, which is why it holds the foredune and appears nowhere inland. The beach itself stays bare,
        /// because 20 mm of sand is under even this plant's floor — the founder still has to walk to the dune.</para>
        ///
        /// <para>Deliberately the <i>second</i>-best string in the world. Its runners are shorter and weaker than a
        /// lomandra leaf, so the cord it lays is workable rather than good, and the walk to the dune toe is still
        /// worth making.</para>
        /// </summary>
        public static readonly PlantSpecies Spinifex = new PlantSpecies(
            "Spinifex", "spinifex", PlantForm.Grass,
            moistureOptimum: 0.25, moistureBreadth: 0.30,
            minSoilDepthM: 0.02, maxSlope: 0.60,
            shadeTolerance: 0.05, exposureTolerance: 1.00,
            minHeightM: 0.2, maxHeightM: 0.5,
            maxSoilDepthM: 0.20);

        private static readonly PlantSpecies[] AllSpecies =
        {
            Blackbutt, Bangalay, OldManBanksia, CoastBanksia, SwampPaperbark,
            GrassTree, HeathBanksia, Bracken, Lomandra, SawSedge, KangarooGrass, Spinifex,
        };

        public static IReadOnlyList<PlantSpecies> All => AllSpecies;

        public static PlantSpecies ByName(string name)
        {
            for (int i = 0; i < AllSpecies.Length; i++)
                if (AllSpecies[i].Name == name) return AllSpecies[i];
            return null;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Who wins a site.
    ///
    /// <para>Sites are contested rather than assigned. Every species is scored, and the winner is
    /// drawn in proportion to the square of its suitability — so the best-suited plant dominates a
    /// stand without ever being the only thing in it, which is what a real stand looks like. Near
    /// the edges of a species' range the scores converge, and that is what turns one community
    /// into the next as a gradual change rather than a line ruled across the hill.</para>
    ///
    /// <para>The overstory is resolved first, because winning changes the site for everything
    /// underneath: where a tree stands, the ground below it is shaded, and the understory is then
    /// scored against a shaded site. That one ordering is the whole reason bracken grows under the
    /// blackbutts and not out in the heath beside them.</para>
    /// </summary>
    public static class PlantCommunity
    {
        /// <summary>
        /// How sharply the best-suited species dominates. Squared: a plant twice as well suited is
        /// four times as likely to hold the ground - strongly dominant, which real eucalypt stands
        /// are, while still leaving room for the second species that real stands also have. Cubed
        /// took a mid slope to ninety-six per cent one tree, which is a stamp wearing a
        /// probability distribution.
        /// </summary>
        public const double DominanceExponent = 2.0;

        /// <summary>Below this nothing of that form can hold the site at all.</summary>
        public const double MinimumViable = 0.02;

        /// <summary>The overstory here, or null where nothing tall can stand.</summary>
        public static PlantSpecies Canopy(in PlantSite site, double roll)
            => Draw(site, roll, PlantForm.Tree, PlantForm.SmallTree);

        /// <summary>The understory here, scored against whatever the canopy is doing to the light.</summary>
        public static PlantSpecies Understory(in PlantSite site, double roll)
            => Draw(site, roll, PlantForm.Shrub, PlantForm.Herb, PlantForm.Grass);

        /// <summary>How well this ground supports a given form at all, 0 to 1.</summary>
        public static double TotalSuitability(in PlantSite site, PlantForm form)
        {
            double best = 0.0;
            IReadOnlyList<PlantSpecies> all = PlantSpecies.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Form != form) continue;
                double s = all[i].Suitability(site);
                if (s > best) best = s;
            }
            return best;
        }

        private static PlantSpecies Draw(in PlantSite site, double roll, params PlantForm[] forms)
        {
            IReadOnlyList<PlantSpecies> all = PlantSpecies.All;

            double total = 0.0;
            for (int i = 0; i < all.Count; i++)
            {
                if (!Matches(all[i].Form, forms)) continue;
                double s = all[i].Suitability(site);
                if (s < MinimumViable) continue;
                total += Math.Pow(s, DominanceExponent);
            }

            if (total <= 0.0) return null;

            double target = SimMath.Clamp01(roll) * total;
            double running = 0.0;

            for (int i = 0; i < all.Count; i++)
            {
                if (!Matches(all[i].Form, forms)) continue;
                double s = all[i].Suitability(site);
                if (s < MinimumViable) continue;

                running += Math.Pow(s, DominanceExponent);
                if (target <= running) return all[i];
            }

            // Only reachable on floating-point drift at the very top of the range.
            for (int i = all.Count - 1; i >= 0; i--)
                if (Matches(all[i].Form, forms) && all[i].Suitability(site) >= MinimumViable)
                    return all[i];

            return null;
        }

        private static bool Matches(PlantForm form, PlantForm[] forms)
        {
            for (int i = 0; i < forms.Length; i++) if (forms[i] == form) return true;
            return false;
        }
    }
}
