using System;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>How fire is rubbed from wood (BF.5). Never renumbered.</summary>
    public enum FrictionMethod : byte
    {
        /// <summary>A thin straight drill spun between the palms in a socket on a hearth board, a notch cut to the socket's edge.</summary>
        HandDrill = 0,
        /// <summary>A blunt stick rubbed hard up and down a groove in a board, the dust gathering at the groove's end.</summary>
        FirePlough = 1,
        /// <summary>A spindle spun by the cord of a bow, pressed down with a bearing block: the one friction method measured (Duncan 2021).</summary>
        BowDrill = 2,
    }

    /// <summary>What an attempt at friction fire came to (BF.5). Never renumbered.</summary>
    public enum FrictionOutcome : byte
    {
        /// <summary>A coal formed in the dust.</summary>
        Coal = 0,
        /// <summary>The hearth is too dense: it polishes to a glaze and gives no dust.</summary>
        TooHard = 1,
        /// <summary>The hearth is too light: it crumbles to coarse dust that will not hold its heat.</summary>
        TooSoft = 2,
        /// <summary>The wood's water took the heat: air-dry, the same wood and pace would have made a coal.</summary>
        TooWet = 3,
        /// <summary>The arms gave out before the dust took a coal: the pair or the pace is too slow.</summary>
        Tired = 4,
    }

    /// <summary>One attempt at friction fire judged (BF.5 part one, promise 6): the numbers of the rub and what it came to, in words.</summary>
    public readonly struct FrictionAttempt
    {
        public readonly FrictionMethod Method;
        public readonly FrictionOutcome Outcome;
        /// <summary>The work the hands put into the rub, W: friction's force times its speed. The body spends several times this.</summary>
        public readonly double PowerW;
        /// <summary>The share of the rub's heat that goes into the hearth rather than the drill, by the two woods' effusivities.</summary>
        public readonly double HearthShare;
        /// <summary>The heat reaching the hearth, W.</summary>
        public readonly double HearthHeatW;
        /// <summary>The heat on the hearth where the rub is fiercest, W/m²: a drill's rim, where the notch is cut; a plough's groove.</summary>
        public readonly double HearthFluxWm2;
        /// <summary>The temperature at which the dust becomes a coal, °C.</summary>
        public readonly double EmberC;
        /// <summary>When the rubbed wood reaches pyrolysis and the dust smokes, s.</summary>
        public readonly double SmokeSeconds;
        /// <summary>When the rubbed surface reaches the coal's temperature, s.</summary>
        public readonly double HotSeconds;
        /// <summary>When a coal would form at this pace, s: the surface hot and a coal's worth of hot dust gathered; infinite when the wood gives no dust.</summary>
        public readonly double CoalSeconds;
        /// <summary>How long the hands keep this pace, s.</summary>
        public readonly double EnduranceSeconds;
        /// <summary>The dust the rub makes, kg/s.</summary>
        public readonly double DustKgPerS;
        /// <summary>The coal's mass when one forms, kg.</summary>
        public readonly double CoalKg;
        public readonly string Words;

        public FrictionAttempt(FrictionMethod method, FrictionOutcome outcome, double powerW, double hearthShare, double hearthFluxWm2, double smokeSeconds,
                               double hotSeconds, double coalSeconds, double enduranceSeconds, double dustKgPerS, string words)
        {
            Method = method;
            Outcome = outcome;
            PowerW = powerW;
            HearthShare = hearthShare;
            HearthHeatW = powerW * hearthShare;
            HearthFluxWm2 = hearthFluxWm2;
            EmberC = FrictionFire.EmberC;
            SmokeSeconds = smokeSeconds;
            HotSeconds = hotSeconds;
            CoalSeconds = coalSeconds;
            EnduranceSeconds = enduranceSeconds;
            DustKgPerS = dustKgPerS;
            CoalKg = outcome == FrictionOutcome.Coal ? FrictionFire.CoalKg : 0.0;
            Words = words;
        }

        public bool Made => Outcome == FrictionOutcome.Coal;
    }

    /// <summary>
    /// Fire by friction (BF.5 part one, promise 6): the hand drill, the fire plough and the bow drill, judged from the two
    /// woods' density and water and the body's strength, as a chain a person can follow and a tablet can explain.
    /// <list type="number">
    /// <item><b>The power in.</b> The hands press down with a force and move the rub at a speed; friction turns μ × force ×
    /// speed of it to heat (for a spun tip, whose rubbing speed runs from nothing at the centre to the rim's, two-thirds of the
    /// rim's). The body's work capacity (thirst's throttle, <see cref="Hydration.WorkCapacity01"/>) scales it.</item>
    /// <item><b>The heat reaching the hearth.</b> The rub's heat parts between the two woods as their thermal effusivities,
    /// e = √(kρc), as between two bodies in contact at a heat source between them; k and c are the Wood Handbook's for the
    /// wood's density and water.</item>
    /// <item><b>The temperature.</b> A spun tip rubs fastest, and so heats fastest, at its rim: under a uniform press the heat
    /// there is 1.5 times the tip's mean, which is why the notch is cut to the socket's edge. Under that flux the hearth's
    /// surface rises as a semi-infinite solid's does, ΔT = (2q/e)√(t/π), its water's latent heat driven off on the way. The
    /// dust smokes at <see cref="SmokeC"/> and glows into a coal at <see cref="EmberC"/>.</item>
    /// <item><b>The coal.</b> Once the surface is that hot, the dust ground off is hot enough to glow, and a coal holds when
    /// <see cref="CoalKg"/> of it has gathered. How much dust the rub makes falls out of the hearth's density, the friction-fire
    /// band of <see cref="Wood.FrictionFireOf"/> (a dense wood polishes, a punky one crumbles).</item>
    /// <item><b>The arms.</b> The pace can be kept for the method's endurance; a coal that would come later does not come.</item>
    /// </list>
    /// The chain is held to the one friction fire measured end to end, Duncan's bow drill (2021: 60 N down, the bow at 1.86 m/s,
    /// about 21 W, a coal in 23 to 24 s, a 0.8 g pile of dust), and to Hough's timings of the hand drill and the plough
    /// (1890). The hand drill's and the plough's forces and speeds and every endurance are estimates, as is the dust's yield:
    /// the literature of friction fire is ethnography and practice, and no one has measured a hand drill (DEBTS).
    /// </summary>
    public static class FrictionFire
    {
        /// <summary>
        /// The kinetic friction of dry wood on wood, μ: 0.25, the figure Duncan's bow drill model takes ("in the range of 0.2 -
        /// 0.3"; Duncan 2021, "The Physics of Fire by Friction", University of Dayton eCommons 418). Measured across the grain,
        /// Scots pine on Scots pine slides at 0.17 (Aira et al. 2014, Materiales de Construcción 64(315) e030); the engineering
        /// tables give clean dry wood 0.25 to 0.5.
        /// </summary>
        public const double WoodOnWoodFriction = 0.25;

        /// <summary>A spun tip's heat at its rim against its mean, under a uniform press: its rubbing speed goes as the radius, whose mean over a disc is two-thirds of the rim's.</summary>
        public const double RimOverMean = 1.5;

        /// <summary>The downward force a person keeps on a hand drill while spinning it, N: 40, an estimate (a hard press of the palms; no hand drill has been measured).</summary>
        public const double HandDrillForceN = 40.0;
        /// <summary>The mean speed of a hand drill's rim under the palms, m/s: 0.9, an estimate (strokes of a quarter of a metre, back and forth about twice a second).</summary>
        public const double HandDrillRimMs = 0.9;
        /// <summary>
        /// How long a person keeps a hand drill at full pace, s: 60, an estimate. Arm work above its critical power runs on a
        /// reserve of about 9 kJ for recreationally active men over a critical power of about 90 W (La Monica et al. 2018,
        /// Respiratory Physiology and Neurobiology 249:1–6), so a pace near 250 W of the body's lasts about a minute; practice
        /// speaks of a minute's hard spinning before the arms burn.
        /// </summary>
        public const double HandDrillEnduranceSeconds = 60.0;

        /// <summary>The downward force a person leans onto a fire plough, N: 100, an estimate (the upper body's weight pressed through the arms).</summary>
        public const double PloughForceN = 100.0;
        /// <summary>The mean speed of a fire plough's strokes, m/s: 1.2, an estimate (strokes of about six inches, Hough's Samoan's, some four times a second).</summary>
        public const double PloughStrokeMs = 1.2;
        /// <summary>The length of a fire plough's groove, m: 0.15, the Samoan plough's six-inch stroke (Hough 1890).</summary>
        public const double PloughGrooveM = 0.15;
        /// <summary>The width a fire plough's blunted tip rubs in its groove, m: 5 mm, an estimate; the heat spreads over the groove's length at this width.</summary>
        public const double PloughTipM = 0.005;
        /// <summary>How long a person keeps a fire plough at full pace, s: 60, an estimate, the hand drill's by the same reserve.</summary>
        public const double PloughEnduranceSeconds = 60.0;

        /// <summary>The downward force on a bow drill's bearing block, N: 60, Duncan's measured by bathroom scale over four repeats (2021).</summary>
        public const double BowDrillForceN = 60.0;
        /// <summary>The bow's mean speed, m/s: 1.86, Duncan's (2021: 35 full strokes of about 22 inches in 21 s); the cord wraps the spindle, so its rim moves with the bow.</summary>
        public const double BowDrillRimMs = 1.86;
        /// <summary>How long a person keeps a bow drill at full pace, s: 120, an estimate; the bow spares the arms the hand drill's press.</summary>
        public const double BowDrillEnduranceSeconds = 120.0;

        /// <summary>
        /// The temperature at which the dust starts to pyrolyse and smoke, °C: about 250 (McAllister, Finney and Cohen 2011: wood
        /// hot enough to pyrolyse is "greater than roughly 250°C", the band below it only drying).
        /// </summary>
        public const double SmokeC = 250.0;

        /// <summary>
        /// The temperature at which a heap of wood's dust glows into a coal, °C: 340. The IFA's GESTIS-DUST-EX database gives the
        /// glowing temperature of 5 mm layers of wood dust on a hot plate as 310 to 340 °C (beech 310 to 320, softwood 340,
        /// "wood" 330) and of fine charcoal dust 230 to 320; Pastier et al. (2013, EN 50281-2-1) found 330 to 350 for wood dust.
        /// Duncan takes 370 °C (700 °F, after Baugh's soldering-iron tests on friction char), within his range of 340 to 430.
        /// </summary>
        public const double EmberC = 340.0;

        /// <summary>
        /// The dust a rub grinds off, kg per joule of friction, in a wood at the middle of the friction band: 1.8 mg a joule. From
        /// Duncan's bow drill: a 0.8 g pile after about 25 s at 21 W is 1.5 mg a joule, from an elm whose band
        /// (<see cref="Wood.FrictionFireOf"/>, 600 kg/m³) is 0.83. One measurement, so an estimate for every other wood.
        /// </summary>
        public const double DustKgPerJ = 1.8e-6;

        /// <summary>
        /// The hot dust that holds its glow as a coal, kg: 0.1 g, the heated mass of Duncan's model (2021); Hough's Hupa drill
        /// made a coal "the size of a pea" (1890).
        /// </summary>
        public const double CoalKg = 0.1e-3;

        /// <summary>
        /// The wood ground away where the rub is hottest, kg per joule of its heat: 0.9 mg a joule, an estimate. Most of a notch's
        /// dust is ground where the rub is cooler and never glows (Duncan's 0.8 g pile round a 0.1 g coal); the grains that make
        /// the coal are heated by the rub's whole heat as they come away, so each kilogram of them can be given no more than the
        /// rub's heat over this. Placed so <see cref="MostDustMoisture"/> falls at a fifth of dry mass, where the words begin to
        /// call wood damp (<see cref="ThingWords.MoistureWord"/>): air-dry wood makes a coal and damp wood does not.
        /// </summary>
        public const double HotGrindKgPerJ = 0.9e-6;

        /// <summary>
        /// The wettest wood whose ground grains can still reach the coal's temperature, as a share of dry mass: the rub's heat
        /// per kilogram ground (1 / <see cref="HotGrindKgPerJ"/>) must heat a kilogram of wood to <see cref="EmberC"/> (the Wood
        /// Handbook's heat, <see cref="Combustion.HeatToRaiseJPerKg"/>) and boil its water off. About 0.20: friction fire wants
        /// air-dry wood, and wood at the fibre saturation point only steams.
        /// </summary>
        public static readonly double MostDustMoisture =
            (1.0 / HotGrindKgPerJ - Combustion.HeatToRaiseJPerKg(Combustion.AmbientC, EmberC)) / Combustion.WaterDriveOffJPerKg;

        /// <summary>The water the woods are judged dry against when a failure's cause is named: air-dry laboratory wood, <see cref="Combustion.CribMoisture"/>.</summary>
        public static double DryMoisture => Combustion.CribMoisture;

        /// <summary>
        /// A failure is the water's when the water made the coal this much slower than air-dry wood would have: a fifth. This
        /// model's rule for the words, not a measurement: below it a pair that fails is named for its woods, not its water.
        /// </summary>
        public const double WaterBlamedFrom = 1.2;

        /// <summary>
        /// The Wood Handbook's thermal conductivity of wood across the grain, W/(m·K), by its specific gravity and water:
        /// k = G (0.1941 + 0.4064 M) + 0.01864 with M a fraction (Glass and Zelinka 2010, Wood Handbook FPL-GTR-190, ch. 4,
        /// eq. 4-15, for specific gravities over 0.3 and moisture under 25 per cent; held there above it).
        /// </summary>
        public static double ConductivityWmK(double densityKgM3, double moisture)
        {
            double g = Math.Max(0.0, densityKgM3) / 1000.0;
            double m = SimMath.Clamp(moisture, 0.0, 0.25);
            return g * (0.1941 + 0.4064 * m) + 0.01864;
        }

        /// <summary>
        /// A wood's thermal effusivity, J/(m²·K·s^½): √(kρc), with c the dry wood's mean specific heat over the rub's rise (the
        /// Wood Handbook's, <see cref="Combustion.HeatToRaiseJPerKg"/>) and its water's 4.18 kJ/(kg·K) carried in the same volume.
        /// </summary>
        public static double EffusivityOf(double densityKgM3, double moisture)
        {
            double u = Math.Max(0.0, moisture);
            double c = Combustion.HeatToRaiseJPerKg(Combustion.AmbientC, EmberC) / (EmberC - Combustion.AmbientC);
            return Math.Sqrt(ConductivityWmK(densityKgM3, u) * densityKgM3 * (c + u * 4186.0));
        }

        private static double DensityOf(Wood w) => w != null ? w.DensityDryKgM3 : FuelPiece.PlainDensityKgM3;

        /// <summary>
        /// An attempt at friction fire: the method, the drill (or plough stick) and its water and thickness, the hearth and its
        /// water, and what thirst leaves of the body (1 fresh). The thickness is a drill's tip's; a plough rubs with its blunted
        /// tip whatever its thickness.
        /// </summary>
        public static FrictionAttempt Judge(FrictionMethod method, Wood drill, double drillMoisture, double drillThicknessM, Wood hearth, double hearthMoisture, double capacity01) =>
            Judge(method, DensityOf(drill), drillMoisture, drillThicknessM, DensityOf(hearth), hearth != null ? hearth.Species.DisplayName : "wood", hearthMoisture, capacity01);

        /// <summary>
        /// The same judgement from the woods' densities alone, for a drill or a hearth of no row in the wood table (a punk log, a
        /// stalk the table does not hold): what friction fire reads of a wood is its density and its water, and nothing else.
        /// </summary>
        public static FrictionAttempt Judge(FrictionMethod method, double drillDensityKgM3, double drillMoisture, double drillThicknessM,
                                            double hearthDensityKgM3, string hearthName, double hearthMoisture, double capacity01)
        {
            double capacity = SimMath.Clamp01(capacity01);
            double thick = Math.Max(0.002, drillThicknessM);
            double hotM2, power, endurance;
            switch (method)
            {
                case FrictionMethod.FirePlough:
                    // The plough's heat spreads along its groove; where the dust gathers is no hotter than the rest of it.
                    hotM2 = PloughGrooveM * PloughTipM;
                    power = WoodOnWoodFriction * PloughForceN * PloughStrokeMs * capacity;
                    endurance = PloughEnduranceSeconds;
                    break;
                case FrictionMethod.BowDrill:
                    hotM2 = Math.PI / 4.0 * thick * thick / RimOverMean;
                    power = WoodOnWoodFriction * BowDrillForceN * BowDrillRimMs * (2.0 / 3.0) * capacity;
                    endurance = BowDrillEnduranceSeconds;
                    break;
                default:
                    hotM2 = Math.PI / 4.0 * thick * thick / RimOverMean;
                    power = WoodOnWoodFriction * HandDrillForceN * HandDrillRimMs * (2.0 / 3.0) * capacity;
                    endurance = HandDrillEnduranceSeconds;
                    break;
            }
            double hearthDensity = Math.Max(1.0, hearthDensityKgM3), drillDensity = Math.Max(1.0, drillDensityKgM3);
            hearthName = string.IsNullOrEmpty(hearthName) ? "wood" : hearthName;

            double eh = EffusivityOf(hearthDensity, hearthMoisture), ed = EffusivityOf(drillDensity, drillMoisture);
            double share = eh / (eh + ed);
            double flux = power * share / hotM2;

            double band = Wood.FrictionFireOf(hearthDensity);
            if (band <= 0.0)
            {
                bool hard = hearthDensity >= 850.0;
                string why = hard
                    ? "the " + hearthName + " is too hard (" + Kg(hearthDensity) + "): the rub polishes it to a glaze and makes no dust"
                    : "the " + hearthName + " is too soft (" + Kg(hearthDensity) + "): it crumbles to coarse dust that will not hold its heat";
                return new FrictionAttempt(method, hard ? FrictionOutcome.TooHard : FrictionOutcome.TooSoft, power, share, flux, double.PositiveInfinity,
                                           double.PositiveInfinity, double.PositiveInfinity, endurance, 0.0, why);
            }
            if (!(power > 0.0))
                return new FrictionAttempt(method, FrictionOutcome.Tired, 0.0, share, 0.0, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, 0.0, 0.0,
                                           "there is no strength left in you to work it");

            double dust = DustKgPerJ * power * band * (0.5 + 0.5 * Wood.FrictionFireOf(drillDensity));
            // Each wood's surface must be dried by the heat it takes, and the dust is ground from both.
            double water = share * Math.Max(0.0, hearthMoisture) + (1.0 - share) * Math.Max(0.0, drillMoisture);
            double coal = CoalSeconds(eh, flux, water, dust, out double smoke, out double hot);
            // What the same woods would do air-dry, for the words when their water is why.
            double ehDry = EffusivityOf(hearthDensity, DryMoisture), edDry = EffusivityOf(drillDensity, DryMoisture);
            double fluxDry = power * (ehDry / (ehDry + edDry)) / hotM2;
            double coalDry = CoalSeconds(ehDry, fluxDry, DryMoisture, dust, out _, out _);
            string dryWould = coalDry <= endurance ? "; air-dry it would have taken a coal in " + Seconds(coalDry) : "";
            if (water > MostDustMoisture)
            {
                // Every grain the rub grinds off brings its water with it: past this, boiling it takes more heat than the rub gives.
                bool hearthWetter = hearthMoisture >= drillMoisture;
                string which = hearthWetter ? "the " + hearthName + " hearth" : (method == FrictionMethod.FirePlough ? "the plough stick" : "the drill");
                double wet = Math.Max(hearthMoisture, drillMoisture);
                string word = ThingWords.MoistureWord(wet);
                string state = word == "dry" ? "holds " + Percent(wet) + " water, dry to the hand and not dry enough for friction fire" : "is " + word + " (" + Percent(wet) + " water)";
                return new FrictionAttempt(method, FrictionOutcome.TooWet, power, share, flux, smoke, double.PositiveInfinity, double.PositiveInfinity, endurance, dust,
                    which + " " + state + ": the dust steams and will not glow, its water taking more heat than the rub gives it; friction fire wants wood drier than "
                    + Percent(MostDustMoisture) + dryWould);
            }
            if (coal <= endurance)
                return new FrictionAttempt(method, FrictionOutcome.Coal, power, share, flux, smoke, hot, coal, endurance, dust,
                    "a coal in the dust after " + Seconds(coal) + ": it smoked at " + Seconds(smoke) + " and glowed at " + Seconds(hot));

            // Would the same woods, air-dry, have made a coal at this pace? Then their water is why.
            double wetter = Math.Max(hearthMoisture, drillMoisture);
            // The water is named as the cause when it is what put the coal out of reach: air-dry would have made one, and the water
            // added a fifth or more to the time. A marginal pair tipped by a little water is the pair's failure.
            if (wetter > DryMoisture && coalDry <= endurance && coal >= WaterBlamedFrom * coalDry)
            {
                bool hearthWetter = hearthMoisture >= drillMoisture;
                string which = hearthWetter ? "the " + hearthName + " hearth" : (method == FrictionMethod.FirePlough ? "the plough stick" : "the drill");
                string word = ThingWords.MoistureWord(wetter);
                string state = word == "dry" ? "holds " + Percent(wetter) + " water, dry to the hand and not dry enough for friction fire" : "is " + word + " (" + Percent(wetter) + " water)";
                return new FrictionAttempt(method, FrictionOutcome.TooWet, power, share, flux, smoke, hot, coal, endurance, dust,
                    which + " " + state + ": the heat went into drying it, and your arms gave out at " + Seconds(endurance) + dryWould);
            }
            string pace = method != FrictionMethod.FirePlough && thick > 0.015 ? "; the drill is thick, and its broad tip spreads the heat" : "";
            string at = double.IsPositiveInfinity(hot) ? "the dust never grew hot enough to glow" : "the dust was smoking but wanted " + Seconds(coal) + " to take a coal";
            string drier = wetter > DryMoisture && coalDry <= endurance ? " (air-dry, " + Seconds(coalDry) + ")" : "";
            return new FrictionAttempt(method, FrictionOutcome.Tired, power, share, flux, smoke, hot, coal, endurance, dust,
                "your arms gave out at " + Seconds(endurance) + " before a coal formed: " + at + drier + pace);
        }

        /// <summary>
        /// When a coal forms, s: the rubbed surface brought to the coal's temperature (a semi-infinite solid under the flux, its
        /// water's latent heat driven off on the way), then a coal's worth of hot dust gathered, its water driven off too.
        /// </summary>
        private static double CoalSeconds(double effusivity, double flux, double moisture, double dustKgPerS, out double smokeSeconds, out double hotSeconds)
        {
            double u = Math.Max(0.0, moisture);
            double c = Combustion.HeatToRaiseJPerKg(Combustion.AmbientC, EmberC) / (EmberC - Combustion.AmbientC);
            double latent = 1.0 + u * Combustion.WaterBoilingLatentJPerKg / (c * (EmberC - Combustion.AmbientC));
            smokeSeconds = SurfaceSeconds(effusivity, flux, SmokeC - Combustion.AmbientC) * latent;
            hotSeconds = SurfaceSeconds(effusivity, flux, EmberC - Combustion.AmbientC) * latent;
            if (!(dustKgPerS > 0.0)) return double.PositiveInfinity;
            return hotSeconds + CoalKg * latent / dustKgPerS;
        }

        /// <summary>The time a semi-infinite solid's surface takes to rise by <paramref name="riseK"/> under a steady flux: t = (π/4)(e ΔT / q)².</summary>
        private static double SurfaceSeconds(double effusivity, double flux, double riseK)
        {
            if (!(flux > 0.0)) return double.PositiveInfinity;
            double x = effusivity * riseK / flux;
            return Math.PI / 4.0 * x * x;
        }

        private static string Seconds(double s) => double.IsPositiveInfinity(s) ? "never" : Math.Max(1.0, Math.Round(s)).ToString("0", CultureInfo.InvariantCulture) + " s";
        private static string Percent(double share) => Math.Round(share * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %";
        private static string Kg(double density) => Math.Round(density).ToString("0", CultureInfo.InvariantCulture) + " kg/m³";
    }
}
