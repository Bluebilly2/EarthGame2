using System;

namespace EarthGame.Engine
{
    /// <summary>How wet the founder's skin is, in the terms they would use (BF.7). Never renumbered: part two sends it.</summary>
    public enum WetLevel : byte
    {
        Dry = 0,
        Damp = 1,
        Wet = 2,
        Soaked = 3,
    }

    /// <summary>
    /// Water on a naked body (BF.7 part one, promise 3): the film the skin holds, filled by rain as it falls and by the water
    /// a founder wades or swims in, and dried by evaporation into the air, faster in wind, dry air and sun; the heat that
    /// evaporation takes, and the share of it the core pays, as a term the heat balance could add (<see cref="Warmth"/>, the
    /// hook in the contract). The founder wakes naked (FOUNDERS_PATH, ruled 2026-09-01) and clothing has no beat yet (DEBTS
    /// 2026-09-16, "clothing and sleep when they exist"); <see cref="WetClothingClo"/> is here for when it has one.
    ///
    /// <para><b>The skin's temperature.</b> <see cref="Warmth"/> has one node, the core; the skin's temperature is read off its
    /// own numbers, the sensible loss through the still-air layer and what is worn: the skin sits above the air by that loss
    /// times the layer's resistance, so this class and the balance cannot come to disagree about how warm the skin is. Water
    /// evaporating at the skin cools it; of the heat evaporation takes, the core pays the share the outer resistance holds of
    /// the whole, R_b / (R_t + R_b), and the rest is heat the cooler skin no longer loses to the air (the series circuit of
    /// tissue and air layer, solved with the evaporation drawn at the skin). The evaporation and the skin's temperature are
    /// solved together.</para>
    /// </summary>
    public sealed class Wetness
    {
        /// <summary>
        /// The water a wet skin holds once it has stopped dripping, kg/m²: 0.050, the film left on a hand dipped in water and let
        /// drip for thirty seconds, 4.99 × 10⁻³ cm (US EPA 2011, <i>Exposure Factors Handbook</i>, ch. 7, table 7-24, from US EPA
        /// 1987). The second source: 0.078 kg/m² at the moment a hand leaves the water and 0.038 after ten seconds' dripping
        /// (Pitol, Kohn and Julian 2020, PLoS ONE 15:e0238998, table 1). Only hands were measured; the whole skin at this film is
        /// an extrapolation no source makes, and hair, which holds a third of its own mass (Jachowicz 1987), is left out.
        /// </summary>
        public const double FilmKgPerM2 = 0.050;

        /// <summary>The water a whole wet skin holds, kg: <see cref="FilmKgPerM2"/> over <see cref="Warmth.SkinAreaM2"/>, about 90 g.</summary>
        public static double CapacityKg => FilmKgPerM2 * Warmth.SkinAreaM2;

        /// <summary>
        /// The body's area rain falls on from above, m²: a standing body's projected area to a source overhead, Fanger's factor
        /// at 90° (0.082) times the share of the skin that radiates (0.725) times the skin, 0.107 m². Read from
        /// <see cref="FireWarmth.ProjectedAreaM2"/>, the one owner of Fanger's tables until they move to the body's own
        /// <c>BodyProjection</c> (BF.5, main's answer 3).
        /// </summary>
        public static double TopAreaM2 => FireWarmth.ProjectedAreaM2(Posture.Standing, 90.0, 0.0);
        /// <summary>The body's area a driven rain strikes face on, m²: the same at 0° from the front (0.35), 0.457 m².</summary>
        public static double FrontAreaM2 => FireWarmth.ProjectedAreaM2(Posture.Standing, 0.0, 0.0);

        /// <summary>
        /// The free-field driving rain on a vertical surface facing the wind, kg/(m²·h) per m/s of wind: 0.222 times the rain's
        /// rate to the 0.88, "R wdr = 0.222 ⋅ U ⋅ R h^0.88" (Lacy, as Blocken and Carmeliet 2004 give it, "A review of wind-driven
        /// rain research in building science", Journal of Wind Engineering and Industrial Aerodynamics 92:1079–1130; ISO 15927-3
        /// writes it 2/9 and 8/9). Its drops fall at 4.5 m/s; drizzle drives harder and a cloudburst less.
        /// </summary>
        public const double DrivingRainCoefficient = 0.222, DrivingRainExponent = 0.88;

        /// <summary>
        /// The Lewis ratio, K/kPa: the evaporative coefficient of the skin's surface over its convective, 16.5 "at typical indoor
        /// conditions" (ASHRAE 2017, <i>Handbook — Fundamentals</i>, ch. 9, eq. 27); ISO 7933's code uses 16.7.
        /// </summary>
        public const double LewisRatioKPerKPa = 16.5;

        /// <summary>
        /// The convective heat transfer coefficient of a standing naked body, W/(m²·K), by the wind: 10.4 v^0.56, and never below
        /// its still-air 3.4, both measured on a nude sixteen-segment manikin in a wind tunnel (de Dear, Arens, Zhang and Oguro
        /// 1997, "Convective and radiative heat transfer coefficients for individual human body segments", International Journal
        /// of Biometeorology 40:141–156). ASHRAE's standing equation, 14.8 v^0.69 (from Seppänen et al. 1972), runs higher and
        /// holds only to 1.5 m/s; ISO 7933's, 8.7 v^0.6, runs lower.
        /// </summary>
        public static double ConvectiveCoefficient(double windMs) => Math.Max(3.4, 10.4 * Math.Pow(Math.Max(0.0, windMs), 0.56));

        /// <summary>
        /// The share of a garment's insulation left when it is soaked: 0.68, from clothing "water saturated at the start of each
        /// walking period (0.75 clo vs. 1.1 clo when dry)" (Castellani et al. 2001, Journal of Applied Physiology 90:939–946).
        /// Bröde et al. found the wet layer's conduction alone cost 9 to 16 per cent (2008); Pugh's accounts of walkers dying in
        /// wet wind have it lose "nearly" everything (1964, 1966), which was not read.
        /// </summary>
        public const double SoakedClothingShare = 0.68;

        /// <summary>A garment's insulation, clo, wet to <paramref name="wet01"/> of saturation.</summary>
        public static double WetClothingClo(double dryClo, double wet01) =>
            Math.Max(0.0, dryClo) * (1.0 - (1.0 - SoakedClothingShare) * SimMath.Clamp01(wet01));

        /// <summary>The specific heat of liquid water, J/(kg·K): the rain that runs off is warmed on the skin.</summary>
        public const double WaterSpecificHeat = 4186.0;

        /// <summary>The water on the skin now, kg.</summary>
        public double SkinWaterKg { get; private set; }

        /// <summary>How much of the skin is wet, 0 dry to 1 streaming: the water held against what it can hold.</summary>
        public double Wettedness01 => SimMath.Clamp01(SkinWaterKg / CapacityKg);

        /// <summary>The last step's evaporation from the wet skin, W: the heat the water took as it left.</summary>
        public double EvaporationW { get; private set; }
        /// <summary>The last step's heat taken from the skin by rain running off it, W.</summary>
        public double RunoffW { get; private set; }
        /// <summary>The last step's cost to the core of both, W: the term the balance would add.</summary>
        public double CoreLossW { get; private set; }
        /// <summary>The last step's skin temperature, °C, as the balance's numbers and the water put it.</summary>
        public double SkinC { get; private set; }

        /// <summary>The word for it now.</summary>
        public WetLevel Level => LevelOf(Wettedness01);

        /// <summary>
        /// The words' thresholds, by the share of the skin wet: damp from a tenth, wet from half, soaked when it holds all it can
        /// (this model's rule for the words).
        /// </summary>
        public static WetLevel LevelOf(double wettedness01)
        {
            if (wettedness01 >= 0.999) return WetLevel.Soaked;
            if (wettedness01 >= 0.5) return WetLevel.Wet;
            if (wettedness01 >= 0.1) return WetLevel.Damp;
            return WetLevel.Dry;
        }

        /// <summary>The word the founder would use; nothing while dry.</summary>
        public static string WordFor(WetLevel level)
        {
            switch (level)
            {
                case WetLevel.Damp: return "damp";
                case WetLevel.Wet: return "wet";
                case WetLevel.Soaked: return "soaked";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// The rain a standing body catches, kg/s: what falls on its top at the rain's rate, and what the wind drives onto its
        /// front, facing the wind, by Lacy's relation (a body turned from the wind catches less on its front; the least is its side's).
        /// </summary>
        public static double RainKgPerSecond(double rainMmPerHour, double windMs)
        {
            double r = Math.Max(0.0, rainMmPerHour);
            if (r <= 0.0) return 0.0;
            double top = r * TopAreaM2;
            double driven = DrivingRainCoefficient * Math.Max(0.0, windMs) * Math.Pow(r, DrivingRainExponent) * FrontAreaM2;
            return (top + driven) / 3600.0;
        }

        /// <summary>
        /// The most a fully wet skin at <paramref name="skinC"/> can evaporate into air, W: h_e (p_sk − p_a) over the skin, with
        /// h_e the Lewis ratio times <see cref="ConvectiveCoefficient"/> (ASHRAE 2017, ch. 9, eqs. 12 and 18: for bare skin
        /// E = w h_e (p_sk,s − p_a)); the vapour pressures from the air's humidity and the saturation at the skin
        /// (<see cref="Climate.SaturationVapourKPa"/>). Zero when the air is as damp as the skin.
        /// </summary>
        public static double MaxEvaporationW(double skinC, double airC, double relativeHumidity01, double windMs)
        {
            double pSkin = Climate.SaturationVapourKPa(skinC);
            double pAir = SimMath.Clamp01(relativeHumidity01) * Climate.SaturationVapourKPa(airC);
            double he = LewisRatioKPerKPa * ConvectiveCoefficient(windMs);
            return Math.Max(0.0, he * (pSkin - pAir) * Warmth.SkinAreaM2);
        }

        /// <summary>
        /// Put the skin wet through, as leaving water does: to all it can hold over the share of it that was in the water
        /// (1 swum, less waded), never drier than it was.
        /// </summary>
        public void Immerse(double share01)
        {
            SkinWaterKg = Math.Max(SkinWaterKg, CapacityKg * SimMath.Clamp01(share01));
        }

        /// <summary>
        /// The founder's seconds going by in the rain and air as they are: the rain filling the film and running off past it, the
        /// film evaporating at the wet share of the skin, and the core's share of the heat both take. The skin's temperature is
        /// the balance's own (<paramref name="coreC"/>, <paramref name="sensibleLossW"/> through <paramref name="boundaryClo"/>, the
        /// still-air layer and what is worn, as <see cref="Warmth.TotalInsulationClo"/> gives it), with the sun's absorbed heat
        /// (<paramref name="solarGainW"/>) warming it and the water cooling it, solved with the evaporation.
        /// </summary>
        public void Advance(double seconds, in Surroundings s, double rainMmPerHour, double coreC, double sensibleLossW, double boundaryClo, double solarGainW)
        {
            EvaporationW = RunoffW = CoreLossW = 0.0;
            if (!(seconds > 0.0)) return;
            double area = Warmth.SkinAreaM2;
            double rb = Math.Max(1e-3, boundaryClo * Warmth.CloToSI);
            // The whole resistance from core to air, by the balance's own loss; the tissue's is what the air layer does not hold.
            double rTotal = sensibleLossW > 1e-6 ? Math.Max(rb + 1e-3, (coreC - s.AirC) * area / sensibleLossW) : rb + Warmth.TissueCloWarm * Warmth.CloToSI;
            double rt = rTotal - rb;
            double share = rb / (rt + rb);
            // The skin's temperature dry and without the sun: the air plus the sensible loss over the air layer.
            double dryC = s.AirC + Math.Max(0.0, sensibleLossW) * rb / area;
            double lift = rt * rb / (area * (rt + rb));

            // The rain: what the skin cannot hold runs off, warmed on the way from the air's temperature to the skin's.
            double caught = RainKgPerSecond(rainMmPerHour, s.WindAtBodyMs) * seconds;
            double room = Math.Max(0.0, CapacityKg - SkinWaterKg);
            double held = Math.Min(caught, room), runoffKg = caught - held;
            SkinWaterKg += held;

            // The evaporation and the skin's temperature together. The skin's balance, its temperature less what the circuit
            // gives it with the water's cooling at that temperature, rises with the temperature, so it is bisected: a plain
            // fixed point oscillates in a cold wind, where a degree of the skin moves the evaporation more than it moves the skin.
            double w = Wettedness01, runoffKgPerS = runoffKg / seconds;
            double top = dryC + solarGainW * lift, hi = top;
            double lo = top - Cooling(top, w, runoffKgPerS, s) * lift;
            for (int i = 0; i < 60; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (mid - top + Cooling(mid, w, runoffKgPerS, s) * lift > 0.0) hi = mid;
                else lo = mid;
            }
            double skin = 0.5 * (lo + hi);
            double evaporation = w * MaxEvaporationW(skin, s.AirC, s.RelativeHumidity01, s.WindAtBodyMs);
            double evaporatedKg = Math.Min(SkinWaterKg, evaporation * seconds / Warmth.LatentHeatOfSweatJPerL);
            EvaporationW = evaporatedKg * Warmth.LatentHeatOfSweatJPerL / seconds;
            RunoffW = runoffKg / seconds * WaterSpecificHeat * Math.Max(0.0, skin - s.AirC);
            SkinWaterKg -= evaporatedKg;
            SkinC = skin;
            CoreLossW = (EvaporationW + RunoffW) * share;
        }

        /// <summary>The heat the water takes from a skin at <paramref name="skinC"/>, W: its evaporation and the runoff warmed to it.</summary>
        private static double Cooling(double skinC, double wettedness01, double runoffKgPerS, in Surroundings s) =>
            wettedness01 * MaxEvaporationW(skinC, s.AirC, s.RelativeHumidity01, s.WindAtBodyMs)
            + runoffKgPerS * WaterSpecificHeat * Math.Max(0.0, skinC - s.AirC);

        /// <summary>A body put back as a save states it; anything outside what the skin holds is clamped, NaN dry.</summary>
        public void Restore(double skinWaterKg)
        {
            SkinWaterKg = double.IsNaN(skinWaterKg) ? 0.0 : Math.Min(CapacityKg, Math.Max(0.0, skinWaterKg));
        }
    }
}
