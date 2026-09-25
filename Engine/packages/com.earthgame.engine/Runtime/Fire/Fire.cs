using System;
using System.Collections.Generic;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>What a fire is doing, as a person looking at it would say (BF.5). Never renumbered: part two sends it.</summary>
    public enum FirePhase : byte
    {
        /// <summary>Nothing burns or glows.</summary>
        Out = 0,
        /// <summary>No flame, but wood is still turning to char under a glow: it smokes.</summary>
        Smouldering = 1,
        /// <summary>Something on it is in flame.</summary>
        Flaming = 2,
        /// <summary>No flame and no wood left to smoke: a bed of glowing char.</summary>
        Embers = 3,
    }

    /// <summary>The air at a fire (BF.5): the wind at the fuel, m/s. Part two reads it from the weather at the fire's height.</summary>
    public readonly struct FireAir
    {
        public readonly double WindMs;

        public FireAir(double windMs)
        {
            WindMs = Math.Max(0.0, windMs);
        }

        public static readonly FireAir Still = new FireAir(0.0);
    }

    /// <summary>
    /// A fire (BF.5, part one): pieces of fuel laid together, stepped through time by the physics in <see cref="Combustion"/>.
    /// Nothing here is told to burn: each piece's heat is the share of its view the rest of the fire fills, and whether it
    /// catches, holds a flame, glows, smoulders or dies falls out of that heat against its thickness, its water and the wind.
    ///
    /// <para><b>What a piece sees.</b> A piece sees the outsides of the other pieces alight around it, in the share
    /// F = A_others / (A_others + A_self): a twig beside a burning log sees almost nothing but fire; a log beside a twig sees
    /// almost none; three logs together see two-thirds fire each. It gets <see cref="Combustion.FireFluxWm2"/> in that share,
    /// and a flaming stick adds its own flame's heat (<see cref="Combustion.OwnFlameFluxWm2"/>). This rule of the view is
    /// this model's (an estimate standing for the view factors of a real lay), and it is what makes a fire need company: a
    /// lone thick log goes out, three together hold, and a log laid on a few twigs chars without catching.</para>
    ///
    /// <para><b>A piece's life.</b> Unlit, it heats toward catching at the rate <see cref="Combustion.IgnitionSeconds"/> gives
    /// for the heat on it, and cools back when that heat fails. Caught, it flames if the gas its burning surface gives off is
    /// at least the critical mass flux for its water and the wind (<see cref="Combustion.CriticalMassFluxKgM2s"/>), and glows
    /// if not. Flaming, its surface burns inward at the crib's rate for its thickness over its density
    /// (<see cref="Combustion.CribBurnKgM2s"/>) scaled by how much of a crib's heat it has, its water and the wind; a fifth of the wood it burns stays as char, the rest
    /// burns as gas, and its water goes off with it, costing its latent heat. Glowing, its char burns on its surface and any
    /// wood under it goes on turning to char in the fire's heat, its gas lost as smoke; it flames again when it can. A bundle
    /// of tinder burns as fast as a flame crosses it (<see cref="Combustion.BundleSpreadMs"/>).</para>
    ///
    /// <para><b>Time.</b> <see cref="Advance"/> takes any span and walks it in steps of at most <see cref="MaxStepSeconds"/>, so
    /// the server's step and a slow layer's catch-up give the same fire to within the steps' own error. Pure and deterministic:
    /// the same pieces and the same air give the same fire.</para>
    /// </summary>
    public sealed class Fire
    {
        /// <summary>The longest step the fire is walked in, s: short beside a kindling stick's minutes and a tinder bundle's seconds.</summary>
        public const double MaxStepSeconds = 1.0;

        /// <summary>A piece with less than this left, kg, is gone.</summary>
        private const double GoneKg = 1e-7;

        private readonly List<FuelPiece> _pieces = new List<FuelPiece>();
        private double[] _flux = new double[8];

        /// <summary>The pieces on it, in the order they were laid.</summary>
        public IReadOnlyList<FuelPiece> Pieces => _pieces;

        /// <summary>The heat its flames released over the last advance, W: the gas they burnt.</summary>
        public double FlameHeatW { get; private set; }
        /// <summary>The heat its glowing char released over the last advance, W.</summary>
        public double GlowHeatW { get; private set; }
        /// <summary>The heat its water took away over the last advance, W: the latent heat of what its pieces gave off as steam.</summary>
        public double WaterCostW { get; private set; }
        /// <summary>The heat it gave its surroundings over the last advance, W: flame and glow, less the water's cost.</summary>
        public double HeatReleaseW => Math.Max(0.0, FlameHeatW + GlowHeatW - WaterCostW);
        /// <summary>The dry wood its pieces lost over the last advance, kg/s: the rate it eats its fuel.</summary>
        public double BurnKgPerS { get; private set; }

        /// <summary>Lay a piece on the fire, unlit.</summary>
        public void Add(FuelPiece piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (!_pieces.Contains(piece)) _pieces.Add(piece);
        }

        /// <summary>
        /// Put a flame to a piece of the fire (a tinder bundle a coal has caught, <see cref="Tinder"/>): it catches at once, and
        /// flames if it can hold a flame and glows if not. False when it could not flame: damp tinder smokes.
        /// </summary>
        public bool Kindle(FuelPiece piece, in FireAir air)
        {
            Add(piece);
            if (piece.Phase == BurnPhase.Spent) return false;
            piece.Ignition01 = 1.0;
            bool flames = CanFlame(piece, 0.0, air);
            piece.Phase = flames ? BurnPhase.Flaming : BurnPhase.Glowing;
            return flames;
        }

        /// <summary>What the fire is doing now, from its pieces.</summary>
        public FirePhase Phase
        {
            get
            {
                bool glow = false, smoke = false;
                for (int i = 0; i < _pieces.Count; i++)
                {
                    FuelPiece p = _pieces[i];
                    if (p.Phase == BurnPhase.Flaming) return FirePhase.Flaming;
                    if (p.Phase == BurnPhase.Glowing)
                    {
                        glow = true;
                        if (p.WoodKg > GoneKg) smoke = true;
                    }
                }
                return smoke ? FirePhase.Smouldering : glow ? FirePhase.Embers : FirePhase.Out;
            }
        }

        /// <summary>The dry wood still on it, unburnt, kg.</summary>
        public double WoodKg
        {
            get
            {
                double kg = 0.0;
                for (int i = 0; i < _pieces.Count; i++) kg += _pieces[i].WoodKg;
                return kg;
            }
        }

        /// <summary>The char glowing or waiting to glow on it, kg: its embers.</summary>
        public double CharKg
        {
            get
            {
                double kg = 0.0;
                for (int i = 0; i < _pieces.Count; i++) kg += _pieces[i].CharKg;
                return kg;
            }
        }

        /// <summary>
        /// The heat on a piece of this fire now, W/m²: the share of its view the other pieces alight fill, times the heat inside
        /// a fire. Its own flame's heat is not in it (<see cref="Combustion.OwnFlameFluxWm2"/>).
        /// </summary>
        public double FluxOnWm2(FuelPiece piece)
        {
            double hot = 0.0;
            for (int i = 0; i < _pieces.Count; i++)
                if (_pieces[i].IsAlight && !ReferenceEquals(_pieces[i], piece)) hot += _pieces[i].SurfaceM2;
            return Seen(hot, piece.SurfaceM2) * Combustion.FireFluxWm2;
        }

        private static double Seen(double othersM2, double selfM2) => othersM2 > 0.0 ? othersM2 / (othersM2 + selfM2) : 0.0;

        /// <summary>Walk the fire through <paramref name="seconds"/> in the air given, in steps of at most <see cref="MaxStepSeconds"/>.</summary>
        public void Advance(double seconds, in FireAir air)
        {
            if (!(seconds > 0.0) || double.IsInfinity(seconds)) return;
            int steps = (int)Math.Ceiling(seconds / MaxStepSeconds);
            double h = seconds / steps;
            double flameJ = 0.0, glowJ = 0.0, waterJ = 0.0, burnt = 0.0;
            for (int i = 0; i < steps; i++) Step(h, air, ref flameJ, ref glowJ, ref waterJ, ref burnt);
            FlameHeatW = flameJ / seconds;
            GlowHeatW = glowJ / seconds;
            WaterCostW = waterJ / seconds;
            BurnKgPerS = burnt / seconds;
        }

        private void Step(double h, in FireAir air, ref double flameJ, ref double glowJ, ref double waterJ, ref double burnt)
        {
            int n = _pieces.Count;
            if (_flux.Length < n) _flux = new double[Math.Max(n, 2 * _flux.Length)];
            // What each piece sees is judged at the step's start, so the order the pieces were laid in changes nothing.
            double hot = 0.0;
            for (int i = 0; i < n; i++) if (_pieces[i].IsAlight) hot += _pieces[i].SurfaceM2;
            for (int i = 0; i < n; i++)
            {
                FuelPiece p = _pieces[i];
                double others = hot - (p.IsAlight ? p.SurfaceM2 : 0.0);
                _flux[i] = Seen(others, p.SurfaceM2) * Combustion.FireFluxWm2;
            }
            for (int i = 0; i < n; i++)
            {
                FuelPiece p = _pieces[i];
                switch (p.Phase)
                {
                    case BurnPhase.Unlit: Heat(p, _flux[i], h, air); break;
                    case BurnPhase.Flaming: Burn(p, _flux[i], h, air, ref flameJ, ref waterJ, ref burnt); break;
                    case BurnPhase.Glowing: Glow(p, _flux[i], h, air, ref glowJ, ref waterJ, ref burnt); break;
                }
            }
        }

        /// <summary>An unlit piece heats toward catching, or cools back; when it catches it flames if it can hold one.</summary>
        private static void Heat(FuelPiece p, double flux, double h, in FireAir air)
        {
            double t = Combustion.IgnitionSeconds(p.DensityKgM3, p.ThicknessM, p.Moisture, flux);
            if (t < double.PositiveInfinity) p.Ignition01 += h / t;
            else if (p.Ignition01 > 0.0)
                // Cooling back as fast as it would have heated at twice the critical flux: an estimate of a surface's losses.
                p.Ignition01 = Math.Max(0.0, p.Ignition01 - h / Combustion.IgnitionSeconds(p.DensityKgM3, p.ThicknessM, p.Moisture, 2.0 * Combustion.CriticalFluxWm2));
            if (p.Ignition01 < 1.0) return;
            p.Ignition01 = 1.0;
            p.Phase = CanFlame(p, flux, air) ? BurnPhase.Flaming : BurnPhase.Glowing;
        }

        /// <summary>
        /// How fast a stick's burning surface moves in, m/s, under the heat around it and its own flame's when it has one: the
        /// crib's burning rate for its laid thickness over its density, scaled by the heat it gains over the critical against what
        /// a crib gives, by its water and by the wind. Zero when the heat on it does not pass the critical flux.
        /// </summary>
        public static double RegressionMs(FuelPiece p, double flux, bool ownFlame, in FireAir air) =>
            BurnKgM2s(p, flux, ownFlame, air, p.Moisture) / p.DensityKgM3;

        /// <summary>The wood a stick's burning surface gives off, kg/(m²·s), at a moisture: see <see cref="RegressionMs"/>.</summary>
        private static double BurnKgM2s(FuelPiece p, double flux, bool ownFlame, in FireAir air, double moisture)
        {
            double own = ownFlame ? Combustion.OwnFlameFluxWm2(p.ThicknessM) : 0.0;
            double gain = flux + own - Combustion.CriticalFluxWm2;
            if (!(gain > 0.0)) return 0.0;
            double crib = Combustion.FireFluxWm2 + Combustion.OwnFlameFluxWm2(p.ThicknessM) - Combustion.CriticalFluxWm2;
            double share = Math.Min(1.0, gain / crib);
            return Combustion.CribBurnKgM2s(p.StartThicknessM) * share * Combustion.MoistureBurnFactor(moisture) * Combustion.FlameWindFactor(air.WindMs);
        }

        /// <summary>Whether a piece under this heat, with its own flame's, would give off gas enough to hold a flame in this wind.</summary>
        public static bool CanFlame(FuelPiece p, double flux, in FireAir air)
        {
            if (p.WoodKg <= GoneKg) return false;
            if (p.IsBundle) return Combustion.BundleSpreadMs(p.Moisture, air.WindMs) > 0.0 && air.WindMs < BundleBlowOutMs;
            double gas = BurnKgM2s(p, flux, true, air, p.Moisture) * (1.0 - Combustion.CharYield);
            return gas >= Combustion.CriticalMassFluxKgM2s(p.Moisture, air.WindMs);
        }

        /// <summary>
        /// A wind that puts out a tinder bundle's small flame, m/s: 8. An estimate: Rothermel and Anderson's needle-bed fires
        /// were already being cooled at 3.6 m/s (INT-30) and a small flame blows out before a large one; a bundle is sheltered
        /// in the hands and the lay while it catches, which part three's verbs will say.
        /// </summary>
        public const double BundleBlowOutMs = 8.0;

        private static void Burn(FuelPiece p, double flux, double h, in FireAir air, ref double flameJ, ref double waterJ, ref double burnt)
        {
            if (!CanFlame(p, flux, air))
            {
                p.Phase = BurnPhase.Glowing;
                return;
            }
            double before = p.WoodKg;
            double dm;
            if (p.IsBundle)
                dm = Math.Min(before, p.StartDryKg * Combustion.BundleSpreadMs(p.Moisture, air.WindMs) / p.BundleM * h);
            else
            {
                double v = RegressionMs(p, flux, true, air);
                double d = p.ThicknessM, dNew = Math.Max(0.0, d - 2.0 * v * h);
                dm = before * (1.0 - dNew * dNew / (d * d));
                p.ThicknessM = dNew;
            }
            double gas = Consume(p, before, dm, ref waterJ, ref burnt);
            double heat = gas * Combustion.GasHeatMJPerKg(p.Wood) * 1e6 * Combustion.FlameEfficiency;
            flameJ += heat;
            p.HeatGivenJ += heat;
            if (p.WoodKg <= GoneKg)
            {
                p.WoodKg = 0.0;
                p.Phase = p.CharKg > GoneKg ? BurnPhase.Glowing : BurnPhase.Spent;
            }
        }

        private static void Glow(FuelPiece p, double flux, double h, in FireAir air, ref double glowJ, ref double waterJ, ref double burnt)
        {
            // The char burns on the piece's outside, fed by the air.
            double charBurnt = Math.Min(p.CharKg, Combustion.CharGlowKgM2s(air.WindMs) * p.SurfaceM2 * h);
            p.CharKg -= charBurnt;
            double heat = charBurnt * Combustion.CharHeatMJPerKg * 1e6;
            glowJ += heat;
            p.HeatGivenJ += heat;
            // Wood still under the char goes on turning to char in the fire's heat, without a flame: its gas is smoke.
            bool charring = false;
            if (p.WoodKg > GoneKg && !p.IsBundle)
            {
                double v = RegressionMs(p, flux, false, air);
                if (v > 0.0)
                {
                    charring = true;
                    double before = p.WoodKg, d = p.ThicknessM, dNew = Math.Max(0.0, d - 2.0 * v * h);
                    double dm = before * (1.0 - dNew * dNew / (d * d));
                    p.ThicknessM = dNew;
                    Consume(p, before, dm, ref waterJ, ref burnt);
                }
            }
            if (p.WoodKg <= GoneKg) p.WoodKg = 0.0;
            if (p.WoodKg > GoneKg && CanFlame(p, flux, air)) p.Phase = BurnPhase.Flaming;
            else if (p.CharKg <= GoneKg)
            {
                p.CharKg = 0.0;
                if (p.WoodKg <= GoneKg) p.Phase = BurnPhase.Spent;
                else if (!charring)
                {
                    // Its glow is spent and nothing round it keeps its wood charring: it goes out, a charred piece cooling.
                    p.Phase = BurnPhase.Unlit;
                    p.Ignition01 = 0.0;
                }
            }
        }

        /// <summary>
        /// Takes dry wood off a piece as its surface burns: a fifth to char, the rest to gas (returned, kg); its water goes with
        /// the wood it was in, costing its latent heat, so the wood left keeps the moisture it had.
        /// </summary>
        private static double Consume(FuelPiece p, double before, double dm, ref double waterJ, ref double burnt)
        {
            dm = Math.Min(before, Math.Max(0.0, dm));
            double water = before > 0.0 ? p.WaterKg * dm / before : 0.0;
            p.WoodKg = before - dm;
            p.WaterKg = Math.Max(0.0, p.WaterKg - water);
            p.CharKg += Combustion.CharYield * dm;
            double cost = water * Combustion.WaterLatentJPerKg;
            waterJ += cost;
            p.HeatGivenJ -= cost;
            burnt += dm;
            return (1.0 - Combustion.CharYield) * dm;
        }

        // ---- words ----

        /// <summary>The fire in a person's words: what it is doing and how much heat it gives.</summary>
        public string Describe()
        {
            int alight = 0, laid = 0;
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].IsAlight) alight++;
                else if (_pieces[i].Phase == BurnPhase.Unlit) laid++;
            }
            string kw = (HeatReleaseW / 1000.0).ToString(HeatReleaseW < 10000.0 ? "0.0" : "0", CultureInfo.InvariantCulture) + " kW";
            string pieces = alight.ToString(CultureInfo.InvariantCulture) + (alight == 1 ? " piece" : " pieces") + " alight";
            if (laid > 0) pieces += ", " + laid.ToString(CultureInfo.InvariantCulture) + " not yet caught";
            switch (Phase)
            {
                case FirePhase.Flaming: return "a fire in flame, about " + kw + ": " + pieces;
                case FirePhase.Smouldering: return "a smouldering fire, about " + kw + ": it smokes, and nothing on it holds a flame";
                case FirePhase.Embers: return "a bed of embers, about " + kw;
                default: return laid > 0 ? "a fire laid and not lit" : "a dead fire";
            }
        }

        /// <summary>
        /// Why a piece of this fire is not in flame, in words (GAME_DESIGN §23), or its state when it is: the fire too small
        /// around it, its water, the wind, or that it is heating and when it should catch.
        /// </summary>
        public string WhyNot(FuelPiece p, in FireAir air)
        {
            if (p == null) return string.Empty;
            string what = FuelClasses.WordFor(p.Class);
            double flux = FluxOnWm2(p);
            switch (p.Phase)
            {
                case BurnPhase.Spent: return "the " + what + " has burnt to ash";
                case BurnPhase.Flaming: return "the " + what + " is burning";
                case BurnPhase.Glowing:
                    if (p.WoodKg <= GoneKg) return "the " + what + " is burnt to glowing char";
                    if (p.IsBundle && p.Moisture >= Combustion.BundleExtinctionMoisture)
                        return "the tinder smokes and will not flame: it is " + ThingWords.MoistureWord(p.Moisture) + " (" + Percent(p.Moisture) + " water), and a flame will not cross fibre wetter than " + Percent(Combustion.BundleExtinctionMoisture);
                    if (p.IsBundle) return "the wind is too strong for the tinder's flame";
                    FireAir calm = new FireAir(Math.Min(air.WindMs, 1.0));
                    if (CanFlame(p, flux, calm)) return "the " + what + " smoulders: the wind strips its flame";
                    // Would it flame were it air-dry, in the same heat and air? Then its water is why.
                    if (WouldFlameAt(p, flux, calm, Combustion.CribMoisture))
                        return "the " + what + " smoulders: it is " + ThingWords.MoistureWord(p.Moisture) + " (" + Percent(p.Moisture) + " water) and its steam smothers the flame";
                    return "the " + what + " smoulders: too little fire around it to keep a flame";
                default:
                    if (p.IsBundle && p.Moisture >= Combustion.BundleExtinctionMoisture)
                        return "the tinder is " + ThingWords.MoistureWord(p.Moisture) + " (" + Percent(p.Moisture) + " water): a flame will not cross fibre wetter than " + Percent(Combustion.BundleExtinctionMoisture);
                    if (!(flux > Combustion.CriticalFluxWm2))
                        return flux > 0.0 ? "the " + what + " is warming but the fire is too small around it to bring it to a flame" : "nothing alight near the " + what;
                    double left = (1.0 - p.Ignition01) * Combustion.IgnitionSeconds(p.DensityKgM3, p.ThicknessM, p.Moisture, flux);
                    return "the " + what + " is heating: it should catch in " + Work.About(left) + " if the fire holds";
            }
        }

        /// <summary>Whether a stick would hold a flame under this heat and air were its water <paramref name="moisture"/>: the flame test of <see cref="CanFlame"/> at another moisture.</summary>
        private static bool WouldFlameAt(FuelPiece p, double flux, in FireAir air, double moisture) =>
            p.WoodKg > GoneKg && !p.IsBundle
            && BurnKgM2s(p, flux, true, air, moisture) * (1.0 - Combustion.CharYield) >= Combustion.CriticalMassFluxKgM2s(moisture, air.WindMs);

        private static string Percent(double share) => Math.Round(share * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %";
    }
}
