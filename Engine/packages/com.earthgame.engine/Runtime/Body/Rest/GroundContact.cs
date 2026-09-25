using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// A soil as heat moves in it (BF.7): its conductivity and its heat capacity by volume, from which its effusivity, how hard
    /// it pulls heat from what touches it. Kersten's equations give it from the soil's dry density and its water, so part two
    /// can read it off the world's soil (<see cref="SoilModel"/>'s wetness and its sand) rather than a table of kinds.
    /// </summary>
    public readonly struct SoilHeat
    {
        /// <summary>Thermal conductivity, W/(m·K).</summary>
        public readonly double ConductivityWmK;
        /// <summary>Heat capacity per unit volume, J/(m³·K).</summary>
        public readonly double HeatCapacityJPerM3K;

        public SoilHeat(double conductivityWmK, double heatCapacityJPerM3K)
        {
            ConductivityWmK = Math.Max(1e-3, conductivityWmK);
            HeatCapacityJPerM3K = Math.Max(1.0, heatCapacityJPerM3K);
        }

        /// <summary>The effusivity, √(kρc), J/(m²·K·s^½).</summary>
        public double Effusivity => Math.Sqrt(ConductivityWmK * HeatCapacityJPerM3K);

        /// <summary>The volumetric heat capacity of water, J/(m³·K), which Farouki's formula scales the soil's by.</summary>
        public const double WaterHeatCapacityJPerM3K = 4.186e6;

        /// <summary>
        /// A soil's heat capacity by volume, J/(m³·K): "C_U = (γd/γw)(0.18 + 1.0 w/100) C_w" for unfrozen soil, its solids at
        /// 0.18 of water's specific heat (Farouki 1981, <i>Thermal properties of soils</i>, US Army CRREL Monograph 81-1, p. 3).
        /// </summary>
        public static double HeatCapacityOf(double dryDensityKgM3, double water01) =>
            Math.Max(0.0, dryDensityKgM3) / 1000.0 * (0.18 + Math.Max(0.0, water01)) * WaterHeatCapacityJPerM3K;

        /// <summary>
        /// A sandy soil at a dry density and a water content (share of dry mass), by Kersten's unfrozen equation in Farouki's
        /// metric form: k = 0.1442 [0.7 log w + 0.4] 10^(0.6243 γd), w in per cent, γd in g/cm³, fitted to within 25 per cent for
        /// water of 1 per cent or more (Kersten 1949, as Farouki 1981 gives it, p. 103). Below a per cent it is held there.
        /// </summary>
        public static SoilHeat Sandy(double dryDensityKgM3, double water01)
        {
            double w = Math.Max(1.0, water01 * 100.0), g = dryDensityKgM3 / 1000.0;
            double k = 0.1442 * (0.7 * Math.Log10(w) + 0.4) * Math.Pow(10.0, 0.6243 * g);
            return new SoilHeat(k, HeatCapacityOf(dryDensityKgM3, water01));
        }

        /// <summary>
        /// A silt or clay soil (a loam) likewise: k = 0.1442 [0.9 log w − 0.2] 10^(0.6243 γd), valid for water of 7 per cent or
        /// more (the same source); below 7 per cent it is held there.
        /// </summary>
        public static SoilHeat SiltClay(double dryDensityKgM3, double water01)
        {
            double w = Math.Max(7.0, water01 * 100.0), g = dryDensityKgM3 / 1000.0;
            double k = 0.1442 * (0.9 * Math.Log10(w) - 0.2) * Math.Pow(10.0, 0.6243 * g);
            return new SoilHeat(k, HeatCapacityOf(dryDensityKgM3, water01));
        }

        /// <summary>
        /// Dry sand, oven-dry quartz sand at 1576 kg/m³: 0.288 W/(m·K) at 27.3 °C by guarded hot plate (Farouki 1981, table 22),
        /// its heat capacity by Farouki's formula. A dune's sand dry to the depth a body warms is the least pull of any ground.
        /// </summary>
        public static readonly SoilHeat DrySand = new SoilHeat(0.288, HeatCapacityOf(1576.0, 0.0));

        /// <summary>Moist sand, a beach's or a dune's below its dry skin: sand at 1600 kg/m³ holding a tenth of its mass in water.</summary>
        public static readonly SoilHeat MoistSand = Sandy(1600.0, 0.10);

        /// <summary>A forest's loam: silt and clay at 1300 kg/m³ holding a fifth of its mass in water.</summary>
        public static readonly SoilHeat Loam = SiltClay(1300.0, 0.20);
    }

    /// <summary>
    /// A lying body and the ground under it (BF.7 part one, promise 6): the heat that leaves through the skin pressed to the
    /// ground, through the body's own tissue, any bedding between, and the ground, in series. The ground takes most at first
    /// and less as the soil under the body warms: a semi-infinite solid touched by a warm surface draws
    /// q = k ΔT / √(παt) = e ΔT / √(πt) (Lienhard and Lienhard 2024, <i>A Heat Transfer Textbook</i>, 6th ed., eq. 5.54), until
    /// the heat spreads sideways as fast as it goes down and the loss settles to the steady flow from a disc on the ground,
    /// Q = 4 R k ΔT (the same, table 5.4, item 12). Bedding is its compressed thickness over its conductivity.
    /// </summary>
    public static class GroundContact
    {
        /// <summary>
        /// The share of a lying body's skin pressed to the ground: 0.17. Volunteers lying supine on a flat board touched it with
        /// 0.34 ± 0.05 m² of their 1.96 m² (Sanak et al. 2025, "Conductive heat loss in simulated outdoor settings", International
        /// Journal of Emergency Medicine 18:235). A naked body lying supine exchanges no heat by convection over 15.6 per cent of
        /// its skin, the floor and its own folds together (Kurazumi et al. 2004, European Journal of Applied Physiology
        /// 93:273–285); a body curled on its side presses less and was not measured.
        /// </summary>
        public const double LyingContactShare = 0.17;

        /// <summary>The skin pressed to the ground, m²: <see cref="LyingContactShare"/> of <see cref="Warmth.SkinAreaM2"/>.</summary>
        public static double ContactAreaM2 => LyingContactShare * Warmth.SkinAreaM2;

        /// <summary>
        /// The ground's resistance under the contact after <paramref name="hoursLying"/>, m²·K/W: the transient's √(πt)/e while
        /// that is the less, then the steady disc's area over its 4Rk (a disc of the contact's area, radius R), which it never
        /// exceeds. Near nothing at the moment of lying down: the first minutes on cold ground are the coldest.
        /// </summary>
        public static double GroundResistanceM2KPerW(in SoilHeat soil, double hoursLying)
        {
            double t = Math.Max(1.0, hoursLying * 3600.0);
            double transient = Math.Sqrt(Math.PI * t) / soil.Effusivity;
            double radius = Math.Sqrt(ContactAreaM2 / Math.PI);
            double steady = ContactAreaM2 / (4.0 * radius * soil.ConductivityWmK);
            return Math.Min(transient, steady);
        }

        /// <summary>
        /// The thickness of loose bedding retained under a lying body: 0.3 of its loose depth. An estimate: no measurement of
        /// natural bedding compressed under a person was found. It is v1's figure for leaf litter (its SLICE2_3 insulation
        /// table), and it keeps the army's advice honest: a bough bed of "15 to 30 cm" (US Army FM 31-70, <i>Basic Cold Weather
        /// Manual</i>, 1968, 3-54b) or "a 30-centimeter layer" (FM 21-76, <i>Survival</i>) lies 5 to 10 cm thick under a body.
        /// </summary>
        public const double BeddingRetainedUnderBody = 0.3;

        /// <summary>
        /// The conductivity of loose dry plant fibre at a bulk density, W/(m·K): k = 0.0444 + 2.72 × 10⁻⁴ ρ, fitted to 65
        /// guarded-hot-plate measurements of straw bales (Costes et al. 2017, "Thermal conductivity of straw bales", Buildings
        /// 7:11), the nearest measured analogue for grass, bracken and paperbark, none of which was found measured. Dry leaf
        /// litter lies in the same band: 0.041 to 0.046 W/(m·K) comminuted, 0.061 whole (Zwanger and Müller 2026, Materials
        /// 19:425).
        /// </summary>
        public static double LooseFibreConductivityWmK(double bulkDensityKgM3) => 0.0444 + 2.72e-4 * Math.Max(0.0, bulkDensityKgM3);

        /// <summary>
        /// A bed's resistance under a lying body, m²·K/W: its loose depth (its mass over its bulk density and the area it is
        /// spread over) retained at <see cref="BeddingRetainedUnderBody"/>, over its conductivity at the density it is pressed
        /// to. Zero for no bed.
        /// </summary>
        public static double BeddingResistanceM2KPerW(double massKg, double bulkDensityKgM3, double areaM2)
        {
            if (!(massKg > 0.0) || !(bulkDensityKgM3 > 0.0) || !(areaM2 > 0.0)) return 0.0;
            double loose = massKg / (bulkDensityKgM3 * areaM2);
            double pressed = loose * BeddingRetainedUnderBody;
            double k = LooseFibreConductivityWmK(bulkDensityKgM3 / BeddingRetainedUnderBody);
            return pressed / k;
        }

        /// <summary>
        /// The heat a lying body loses into the ground, W: over <see cref="ContactAreaM2"/>, from the core at
        /// <paramref name="coreC"/> through the tissue (<paramref name="tissueResistanceM2KPerW"/>, the body's own, as
        /// <see cref="Warmth"/> has it), the bed and the ground, to the ground's own temperature below.
        /// </summary>
        public static double LossW(double coreC, double groundC, double tissueResistanceM2KPerW, double beddingResistanceM2KPerW, double groundResistanceM2KPerW)
        {
            double r = Math.Max(1e-4, tissueResistanceM2KPerW) + Math.Max(0.0, beddingResistanceM2KPerW) + Math.Max(0.0, groundResistanceM2KPerW);
            return ContactAreaM2 * (coreC - groundC) / r;
        }
    }
}
