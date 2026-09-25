using System;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>What happened when a coal was laid in tinder (BF.5). Never renumbered.</summary>
    public enum TinderOutcome : byte
    {
        /// <summary>The tinder burst into flame.</summary>
        Flame = 0,
        /// <summary>The tinder smoked round the coal and would not flame: it is too damp for a flame to cross.</summary>
        TooDamp = 1,
        /// <summary>The coal burnt out before the tinder round it caught.</summary>
        CoalDied = 2,
        /// <summary>The tinder smoulders round the coal in still air: it wants blowing on.</summary>
        Smoulders = 3,
    }

    /// <summary>A coal laid in tinder, judged (BF.5 part one, promise 7): what happened, when, and the words for it.</summary>
    public readonly struct TinderCatch
    {
        public readonly TinderOutcome Outcome;
        /// <summary>Seconds from laying the coal in to the flame, or to the coal's death or the smoke's giving up.</summary>
        public readonly double Seconds;
        /// <summary>The heat the coal gives while it glows, W.</summary>
        public readonly double CoalHeatW;
        /// <summary>How long the coal would glow, s.</summary>
        public readonly double CoalLifeSeconds;
        public readonly string Words;

        public TinderCatch(TinderOutcome outcome, double seconds, double coalHeatW, double coalLifeSeconds, string words)
        {
            Outcome = outcome;
            Seconds = seconds;
            CoalHeatW = coalHeatW;
            CoalLifeSeconds = coalLifeSeconds;
            Words = words;
        }

        public bool Caught => Outcome == TinderOutcome.Flame;
    }

    /// <summary>
    /// Tinder catching from a coal (BF.5 part one, promise 7): a friction coal, a pellet of char dust, is laid in a bundle of
    /// fine fibre and blown on. The coal glows at the char's rate for the air on it (<see cref="Combustion.CharGlowKgM2s"/>),
    /// and its heat must bring the fibres round it to catching, their water driven off first, before it burns out; then a
    /// flame must be able to cross the fibre, which it will not when the fibre is wetter than Rothermel and Anderson's
    /// extinction moisture (<see cref="Combustion.BundleExtinctionMoisture"/>), and does not in still air, where the glow
    /// smoulders into the tinder without flame until someone blows. "Damp tinder fails and says why" (FOUNDERS_PATH) is the
    /// last of those.
    /// </summary>
    public static class Tinder
    {
        /// <summary>
        /// A friction coal's density, kg/m³: 250, the pellet of loose char dust that gathers in a hearth's notch. An estimate, of
        /// the order of loose powders' packing; it sets the coal's surface, and so how fast it glows and how long it lasts.
        /// </summary>
        public const double CoalDensityKgM3 = 250.0;

        /// <summary>
        /// The breath a person blows into tinder with, m/s at the coal: 3. People blowing out reach a mean 12 m/s 25 mm from the
        /// lips (peaks of 6 to 64; Geoghegan et al. 2017, International Journal of Legal Medicine 131:1193–1201); a jet slows as
        /// it spreads, and a steady blow from a hand's breadth off reaches the coal at about a quarter of that (an estimate).
        /// </summary>
        public const double BlowMs = 3.0;

        /// <summary>
        /// The least air past a smouldering coal and its tinder that turns the smoulder to flame, m/s: 1.2, where small smouldering
        /// piles went over to flame in Salehizadeh et al.'s tunnel (2021, Frontiers in Mechanical Engineering 7:630324: 1.2 m/s for
        /// a 16 g pile, 1.4 for an 8 g one); the literature puts the transition at 1 to 5 m/s (Santoso et al. 2019, Frontiers in
        /// Mechanical Engineering 5:49). In still air a coal smoulders into tinder; a breath or a breeze brings the flame.
        /// </summary>
        public const double FlameAirMs = 1.2;

        /// <summary>
        /// The tinder round a coal that must catch before a flame can stand in the bundle, m: a shell a centimetre thick round the
        /// coal. An estimate, placed so a blown coal of a tenth of a gram brings dry tinder to flame in ten to fifteen seconds, the
        /// order of the blowing a friction coal wants.
        /// </summary>
        public const double CatchShellM = 0.01;

        /// <summary>The share of a coal's heat that goes into the tinder round it rather than into the air: a half, an estimate for a coal wrapped in a nest.</summary>
        public const double CoalHeatToTinder = 0.5;

        /// <summary>A coal's surface, m², as a ball of its mass at the coal's density.</summary>
        public static double CoalSurfaceM2(double coalKg)
        {
            double v = Math.Max(0.0, coalKg) / CoalDensityKgM3;
            double r = Math.Pow(3.0 * v / (4.0 * Math.PI), 1.0 / 3.0);
            return 4.0 * Math.PI * r * r;
        }

        /// <summary>The heat a coal gives as it glows in air moving at <paramref name="airMs"/>, W.</summary>
        public static double CoalHeatW(double coalKg, double airMs) => Combustion.CharGlowKgM2s(airMs) * CoalSurfaceM2(coalKg) * Combustion.CharHeatMJPerKg * 1e6;

        /// <summary>How long a coal glows in air moving at <paramref name="airMs"/>, s.</summary>
        public static double CoalLifeSeconds(double coalKg, double airMs)
        {
            double rate = Combustion.CharGlowKgM2s(airMs) * CoalSurfaceM2(coalKg);
            return rate > 0.0 ? coalKg / rate : double.PositiveInfinity;
        }

        /// <summary>
        /// Lay a coal of <paramref name="coalKg"/> in a bundle of tinder, blowing on it or not, in a wind of <paramref name="windMs"/>
        /// at the tinder: whether it flames, when, and why not.
        /// </summary>
        public static TinderCatch Catch(double coalKg, FuelPiece tinder, bool blowing, double windMs)
        {
            if (tinder == null || !(coalKg > 0.0))
                return new TinderCatch(TinderOutcome.CoalDied, 0.0, 0.0, 0.0, tinder == null ? "no tinder to lay the coal in" : "no coal");
            double air = Math.Max(Math.Max(0.0, windMs), blowing ? BlowMs : 0.0);
            double heat = CoalHeatW(coalKg, air);
            double life = CoalLifeSeconds(coalKg, air);
            double flux = heat / CoalSurfaceM2(coalKg);
            double u = tinder.Moisture;
            string what = tinder.IsBundle ? "the tinder" : "the " + FuelClasses.WordFor(tinder.Class);

            // The fibres touching the coal heat through under its glow, as a thin piece does.
            double fibre = Combustion.IgnitionSeconds(tinder.DensityKgM3, tinder.ThicknessM, u, CoalHeatToTinder * flux);
            // And the tinder round it must be given its heat, water and all, before the coal is spent. A nest of fibre wraps the
            // coal, takes its share of the coal's heat and keeps it in its own fluff; a solid stick touching the coal takes only
            // what its breadth subtends of the coal's heat, and loses heat from its bare wood at about the critical flux.
            double r = Math.Pow(3.0 * coalKg / CoalDensityKgM3 / (4.0 * Math.PI), 1.0 / 3.0);
            double zoneKg, given, lost;
            if (tinder.IsBundle)
            {
                double shellM3 = 4.0 / 3.0 * Math.PI * (Math.Pow(r + CatchShellM, 3.0) - r * r * r);
                double bulk = tinder.StartDryKg / (Math.PI / 6.0 * tinder.BundleM * tinder.BundleM * tinder.BundleM);
                zoneKg = Math.Min(tinder.WoodKg, bulk * shellM3);
                given = CoalHeatToTinder * heat;
                lost = 0.0;
            }
            else
            {
                double d = tinder.ThicknessM, reach = Math.Min(tinder.LengthM, 2.0 * (r + CatchShellM));
                zoneKg = Math.Min(tinder.WoodKg, tinder.DensityKgM3 * Math.PI / 4.0 * d * d * reach);
                given = CoalHeatToTinder * Math.Min(1.0, d / (Math.PI * r)) * heat;
                lost = Combustion.CriticalFluxWm2 * Math.PI * d * reach;
            }
            double need = zoneKg * (Combustion.HeatToIgniteJPerKg + u * Combustion.WaterDriveOffJPerKg);
            double warm = given > lost ? need / (given - lost) : double.PositiveInfinity;
            double caught = Math.Max(fibre, warm);

            if (!(caught < life))
            {
                string why = u >= 0.20 ? "its water took the coal's heat (" + Percent(u) + ")"
                    : !tinder.IsBundle || tinder.ThicknessM >= FuelClasses.TinderUnderM ? "it is too coarse to take a coal: tinder is fibre finer than a millimetre"
                    : "the coal was too small for it";
                return new TinderCatch(TinderOutcome.CoalDied, life, heat, life, "the coal burnt out before " + what + " caught: " + why);
            }
            if (u >= Combustion.BundleExtinctionMoisture)
                return new TinderCatch(TinderOutcome.TooDamp, caught, heat, life,
                    what + " smoked round the coal and would not flame: it is " + ThingWords.MoistureWord(u) + " (" + Percent(u) + " water), and a flame will not cross fibre wetter than " + Percent(Combustion.BundleExtinctionMoisture));
            if (air < FlameAirMs)
                return new TinderCatch(TinderOutcome.Smoulders, caught, heat, life, what + " smoulders round the coal: blow on it to bring the flame");
            // The flame takes the bundle as fast as it can cross the fibre from the coal outward, a quarter of the way across.
            double take = tinder.IsBundle ? 0.25 * tinder.BundleM / Math.Max(1e-9, Combustion.BundleSpreadMs(u, air)) : 0.0;
            double seconds = caught + take;
            if (!(seconds < life + take))
                return new TinderCatch(TinderOutcome.CoalDied, life, heat, life, "the coal burnt out before " + what + " caught");
            return new TinderCatch(TinderOutcome.Flame, seconds, heat, life,
                what + " caught from the coal and burst into flame after " + Work.About(seconds).Replace("about ", string.Empty));
        }

        private static string Percent(double share) => Math.Round(share * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %";
    }
}
