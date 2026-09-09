using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>
    /// A kind of stone, described by the properties that decide what it can be used for. Ported from v1 with
    /// three stones added for Bherwerre (v1's open item E0: the Sydney Basin has no flint; what the peninsula
    /// offers is silcrete, quartz and rhyolite beach pebbles, and quartzite — ECOSYSTEM.md, the archaeological
    /// record of the Jervis Bay coast).
    ///
    /// <para>Following GAME_DESIGN §8, a stone is not a label with a use attached. Flint is not
    /// "the knapping rock" — it is a cryptocrystalline silicate whose isotropic structure makes
    /// it fracture conchoidally, which is <b>why</b> a struck flake leaves an edge. Obsidian does
    /// the same thing better and chips more easily, sandstone does not do it at all but abrades
    /// other stone, and granite does neither but survives heat. A player who knows any of that
    /// from real life should be able to act on it here without being told (§3).</para>
    ///
    /// <para>Everything below is real published mineralogy, and stays engine-free so it can be
    /// checked against references without Unity.</para>
    /// </summary>
    public sealed class StoneType
    {
        public string Name { get; }

        /// <summary>Bulk density, kg/m³.</summary>
        public double DensityKgM3 { get; }

        /// <summary>Mohs hardness, 1–10. Above about 5.5 it will scratch steel.</summary>
        public double MohsHardness { get; }

        /// <summary>
        /// How cleanly it breaks in smooth curved shells rather than crumbling or splitting along
        /// planes, 0–1. This, not hardness, is what makes a stone knappable: it decides whether a
        /// struck flake carries a continuous edge.
        /// </summary>
        public double ConchoidalFracture { get; }

        /// <summary>Typical grain size in millimetres. Fine grain holds a finer edge.</summary>
        public double GrainSizeMm { get; }

        /// <summary>Resistance to crack growth, MPa·m^0.5. High values resist knapping and shattering alike.</summary>
        public double FractureToughness { get; }

        /// <summary>Survives repeated fire without spalling — the property that matters for hearths.</summary>
        public bool ThermallyStable { get; }

        /// <summary>What it is, in a sentence, for the tablet to report.</summary>
        public string Description { get; }

        public StoneType(string name, double densityKgM3, double mohsHardness,
                         double conchoidalFracture, double grainSizeMm, double fractureToughness,
                         bool thermallyStable, string description)
        {
            Name = name;
            DensityKgM3 = densityKgM3;
            MohsHardness = mohsHardness;
            ConchoidalFracture = conchoidalFracture;
            GrainSizeMm = grainSizeMm;
            FractureToughness = fractureToughness;
            ThermallyStable = thermallyStable;
            Description = description;
        }

        /// <summary>
        /// How good a cutting edge this stone can hold, 0–1: it must fracture conchoidally, be
        /// fine-grained, and be hard enough that the edge survives use. An emergent value, not an
        /// authored one — change the mineralogy and this changes with it.
        /// </summary>
        public double EdgeQuality
        {
            get
            {
                double fine = 1.0 / (1.0 + GrainSizeMm * 4.0);
                double hard = SimMath.Clamp01((MohsHardness - 3.0) / 4.0);
                return SimMath.Clamp01(ConchoidalFracture * fine * (0.35 + 0.65 * hard));
            }
        }

        /// <summary>
        /// How readily it can be worked at all, 0–1. Very tough stone resists flaking; stone that
        /// does not fracture conchoidally cannot be flaked usefully however soft it is.
        /// </summary>
        public double Knappability
        {
            get
            {
                double tough = SimMath.Clamp01(1.0 - (FractureToughness - 0.3) / 2.2);
                return SimMath.Clamp01(ConchoidalFracture * (0.3 + 0.7 * tough));
            }
        }

        /// <summary>
        /// How well it takes a beating without breaking, 0–1 — a hammerstone, a pounder, an anvil.
        ///
        /// <para>The opposite requirement to an edge, and it falls out of the same numbers. What is
        /// wanted is a stone that will <b>not</b> fracture conchoidally, is tough rather than hard,
        /// and is heavy for its size. Flint makes the best knife in this list and one of the worst
        /// hammers; basalt is the other way round. Nothing anywhere says so — it is what the
        /// mineralogy comes to.</para>
        /// </summary>
        public double PoundingQuality
        {
            get
            {
                double tough = SimMath.Clamp01((FractureToughness - 0.4) / 1.8);
                double solid = SimMath.Clamp01(1.0 - 0.85 * ConchoidalFracture);
                double coherent = 1.0 / (1.0 + GrainSizeMm * 0.25);
                double heavy = SimMath.Clamp01((DensityKgM3 - 2200.0) / 700.0);
                double hard = SimMath.Clamp01((MohsHardness - 2.5) / 3.0);
                return SimMath.Clamp01(tough * solid * coherent * (0.55 + 0.25 * heavy + 0.20 * hard));
            }
        }

        /// <summary>Abrades other stone: the basis of grinding, sharpening and querns.</summary>
        public bool IsAbrasive => ConchoidalFracture < 0.35 && GrainSizeMm >= 0.05 && MohsHardness >= 5.5;

        public override string ToString() => Name;

        // ---- the catalogue: real stones, real numbers ----

        public static readonly StoneType Flint = new StoneType(
            "Flint", 2600, 7.0, 0.95, 0.001, 0.9, false,
            "Cryptocrystalline silica formed in chalk. Breaks in smooth shells, holding an edge "
            + "sharper than surgical steel. Shatters if heated hard.");

        public static readonly StoneType Chert = new StoneType(
            "Chert", 2600, 6.8, 0.85, 0.004, 1.0, false,
            "Flint's coarser cousin, common in sedimentary country. Knaps well, though the edge "
            + "is a little less fine.");

        public static readonly StoneType Obsidian = new StoneType(
            "Obsidian", 2400, 5.5, 0.99, 0.0001, 0.7, false,
            "Volcanic glass. The finest edge available to stone, and the most brittle — it chips "
            + "if you look at it wrong.");

        public static readonly StoneType Quartzite = new StoneType(
            "Quartzite", 2650, 7.0, 0.55, 0.3, 1.8, true,
            "Metamorphosed sandstone, hard and stubborn. Workable with effort, and it takes heat "
            + "without spalling.");

        public static readonly StoneType Sandstone = new StoneType(
            "Sandstone", 2300, 6.5, 0.10, 0.5, 1.2, true,
            "Cemented quartz grains. Useless for an edge, excellent for grinding one — and it is "
            + "what this coast's cliffs are built from.");

        public static readonly StoneType Basalt = new StoneType(
            "Basalt", 2900, 6.0, 0.45, 0.5, 2.2, true,
            "Fine-grained volcanic rock. Too tough to flake finely, but it makes a fine hammer or "
            + "an axe ground to shape, and it laughs at fire.");

        public static readonly StoneType Granite = new StoneType(
            "Granite", 2700, 6.5, 0.20, 3.0, 2.5, true,
            "Coarse intrusive rock of quartz, feldspar and mica. It will not flake, but it will "
            + "hold heat and take a beating.");

        public static readonly StoneType Shale = new StoneType(
            "Shale", 2400, 3.0, 0.15, 0.02, 0.5, false,
            "Laminated mudstone. Splits into flat plates along its bedding and is too soft to keep "
            + "an edge.");

        /// <summary>
        /// Silcrete: sand and gravel cemented by silica under a long dry weathering, capping old land surfaces
        /// across south-eastern Australia and the commonest knapped stone of the New South Wales coast. Grain
        /// size from its parent sand; microquartz cement gives the conchoidal fracture (Webb and Domanski,
        /// "The relationship between lithology, flaking properties and artefact manufacture for Australian
        /// silcretes", Archaeometry 50 (2008)).
        /// </summary>
        public static readonly StoneType Silcrete = new StoneType(
            "Silcrete", 2600, 7.0, 0.80, 0.05, 1.1, false,
            "Old sand cemented to glass by silica in a dry age. Flakes cleanly to a hard, slightly "
            + "grainy edge; the everyday knapping stone of this coast.");

        /// <summary>
        /// Vein and pebble quartz: pure crystalline silica, hard, but with internal flaws that make its flakes
        /// short and unpredictable (a fracture toughness above flint's and a coarse grain). Knapped through
        /// the Australian record wherever finer stone was scarce.
        /// </summary>
        public static readonly StoneType Quartz = new StoneType(
            "Quartz", 2650, 7.0, 0.50, 0.5, 1.3, false,
            "Milky crystalline silica, from veins and as pebbles on the beach. Hard enough for any "
            + "edge, but it breaks where it pleases; a sharp flake here and there among the shatter.");

        /// <summary>
        /// Rhyolite: a fine-grained to glassy volcanic rock of quartz and feldspar; on this coast it arrives as
        /// beach pebbles, rolled from the volcanics to the north. Fine groundmass gives a usable conchoidal
        /// fracture; the pebble's rounded cortex is the platform the first flake comes off.
        /// </summary>
        public static readonly StoneType Rhyolite = new StoneType(
            "Rhyolite", 2500, 6.5, 0.70, 0.05, 1.3, true,
            "A pale volcanic rock with a fine, almost glassy grain, rolled to pebbles on the beach. "
            + "Flakes well from a cobble's rounded face and stands heat.");

        /// <summary>Every stone the world knows about.</summary>
        public static IReadOnlyList<StoneType> All { get; } = new[]
        {
            Flint, Chert, Obsidian, Quartzite, Sandstone, Basalt, Granite, Shale, Silcrete, Quartz, Rhyolite,
        };

        public static StoneType ByName(string name)
        {
            foreach (StoneType s in All)
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }
    }
}
