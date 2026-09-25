using System;

namespace EarthGame.Engine
{
    /// <summary>How hungry the founder is, in the terms they would use (BF.7). Never renumbered: part two sends it.</summary>
    public enum HungerLevel : byte
    {
        Fed = 0,
        Hungry = 1,
        VeryHungry = 2,
        Weak = 3,
        Starving = 4,
        Wasting = 5,
    }

    /// <summary>
    /// Hunger as an energy balance (BF.7 part one, promise 1): the body's stores of fuel, what it spends drawn from them in the
    /// order the fasting body draws them, what eating puts back, what a deficit costs, the words at each stage and when a fast
    /// kills. The spending is not this class's: it is the heat balance's own metabolic watts (<see cref="Warmth.ProductionW"/>,
    /// the basal rate, the work of moving and shivering), passed in, so nothing here prices a step twice.
    ///
    /// <para><b>The stores.</b> Liver glycogen, muscle glycogen, fat and body protein, as Cahill tabled them for a 70 kg man
    /// (1983, "President's address. Starvation", Transactions of the American Clinical and Climatological Association 94:1–21,
    /// table 1), the fat at a forager's share of body mass (<see cref="StartFatShare"/>); their energies are Chow and Hall's
    /// (2008, "The dynamics of human body weight change", PLoS Computational Biology 4:e1000045).</para>
    ///
    /// <para><b>The order of drawing.</b> Food being absorbed pays first. Then the liver's glycogen, which keeps the blood's
    /// glucose for about a day; the muscles' glycogen pays half of any work above rest; and the rest of the deficit comes from
    /// fat and protein in the shares the fasting body burns them, protein a fifth of it at first and an eighth once the brain
    /// runs on ketones, until the fat that can be burnt is gone and protein pays everything. A fast kills when the protein lost
    /// passes what life allows (<see cref="LethalProteinShare"/>): at rest, in about nine weeks, as the hunger strikers of 1981
    /// died between the 46th day and the 73rd.</para>
    ///
    /// <para><b>The fast and the meal.</b> The fast adapts (the brain on ketones, protein spared, the salt and water shed) only
    /// while the liver's glycogen is low, and eating undoes it once the liver is full again. A surplus refills the glycogen,
    /// gives back the protein a deficit took in the share it took it, and lays the rest down as fat. So a founder who eats
    /// every day spends protein each night, has it back each morning, and never enters the fast.</para>
    /// </summary>
    public sealed class Hunger
    {
        // ---- the stores and their energies ----

        /// <summary>Liver glycogen of a fed 70 kg man, kg: 0.07 (Cahill 1983, table 1; Magnusson et al. 1992 measured about 54 g four hours after a meal).</summary>
        public const double LiverGlycogenKg = 0.07;
        /// <summary>Muscle glycogen, kg: 0.40 (Cahill 1983, table 1; 0.3 to 0.7 in Murray and Rosenbloom's review, 2018).</summary>
        public const double MuscleGlycogenKg = 0.40;
        /// <summary>The energy of glycogen, J/kg: 17.6 MJ (Chow and Hall 2008).</summary>
        public const double GlycogenJPerKg = 17.6e6;
        /// <summary>Water stored with each kilogram of glycogen, kg: 2.7 (Chow and Hall 2008; "at least 3 g", Murray and Rosenbloom 2018).</summary>
        public const double GlycogenWaterPerKg = 2.7;
        /// <summary>The energy of body fat, J/kg of triglyceride: 39.5 MJ (Chow and Hall 2008; Hall 2008).</summary>
        public const double FatJPerKg = 39.5e6;
        /// <summary>The energy of body protein, J/kg: 19.7 MJ (Chow and Hall 2008).</summary>
        public const double ProteinJPerKg = 19.7e6;
        /// <summary>Water carried by each kilogram of protein in lean tissue, kg: 1.6, which puts lean tissue at 7.6 MJ/kg (Hall 2008, "What is the required energy deficit per unit weight loss?", International Journal of Obesity 32:573–576).</summary>
        public const double LeanWaterPerProtein = 1.6;

        /// <summary>
        /// The founder's fat as a share of body mass: 0.135, the Hadza men's, measured among foragers who walk 11 km a day
        /// (13.5 ± 4.2 per cent, Pontzer et al. 2012, "Hunter-gatherer energetics and human obesity", PLoS ONE 7:e40503); the
        /// Minnesota men began at 13.9 (Keys et al. 1950). Cahill's reference man carries 12 kg, a fuller figure. 9.45 kg here.
        /// </summary>
        public const double StartFatShare = 0.135;

        /// <summary>
        /// The fat a fast cannot burn, as a share of body mass: 0.03, an estimate for the fat in cells and nerves; the Minnesota men
        /// ended six months of semi-starvation alive with 2.67 kg of fat (Keys et al. 1950, table 166). Past it, "a catastrophic
        /// protein catabolism will develop" (Gétaz et al. 2012, Swiss Medical Weekly 142:w13675).
        /// </summary>
        public const double FatFloorShare = 0.03;

        /// <summary>The body's protein, kg: 6 (Cahill 1983, table 1; "approximately 6-8 kg", Owen et al. 1967).</summary>
        public const double BodyProteinKg = 6.0;

        /// <summary>
        /// The share of body protein whose loss kills: a half, the top of "a third to a half of the body protein stores", whose
        /// breakdown "is believed incompatible with life" (Kerndt et al. 1982, "Fasting: the history, pathophysiology and
        /// complications", Western Journal of Medicine 137:379–399). Not lower: the Minnesota men lost "roughly 2.5 kg. of
        /// protein" in 24 weeks and lived (Keys et al. 1950, p. 333). 3 kg.
        /// </summary>
        public const double LethalProteinShare = 0.5;

        // ---- how the body draws on them ----

        /// <summary>
        /// The time constant of the liver's glycogen in a fast, hours: 24, its 70 g drawn at first at about 2.9 g an hour, the net
        /// glycogenolysis of 4.3 µmol/(kg·min) Rothman et al. measured by NMR over a fast's first 22 hours (1991, Science
        /// 254:573–576, as Roden, Petersen and Shulman 2001 report it), so that it "maintains blood glucose for 12-16 hours" (Cahill
        /// 1983) and is 93 per cent gone at 64 h (they measured 83).
        /// </summary>
        public const double LiverDrawHours = 24.0;

        /// <summary>
        /// The share of work above rest paid from muscle glycogen while it is full: 0.5. In cold air carbohydrate supplied 51 per
        /// cent of a shivering body's energy against 18 at rest (Vallerand and Jacobs 1989, as Young, Sawka and Pandolf 1996 give
        /// it), and "muscle glycogen is responsible for most of the increase in heat production" as shivering intensifies (Haman
        /// et al. 2005, Journal of Physiology 566:247–256); scaled by how full the glycogen is, as the fuel shifts to fat when it
        /// runs low without the heat falling (Haman et al. 2004). An estimate for walking as for shivering.
        /// </summary>
        public const double WorkGlycogenShare = 0.5;

        /// <summary>
        /// The share of the stores' deficit paid from protein at a fast's start: 0.18. Six men fasting eight days drew 14 per cent
        /// of their energy from protein (Cahill et al. 1966, Journal of Clinical Investigation 45:1751–1769); Levanzin's fast drew
        /// 16 to 19 (Benedict 1915, in Keys et al. 1950, table 261); 75 g a day, about a fifth, on days three and four (Cahill 1983).
        /// </summary>
        public const double ProteinShareEarly = 0.18;

        /// <summary>
        /// The share once the brain runs on ketones: 0.12. A lean man fasting 36 days lost 5 to 7 g of nitrogen a day after the
        /// third week, about 12 per cent of his energy (Kerndt et al. 1982); "only about 10%" in prolonged fasting (Gétaz et al.
        /// 2012); obese fasters spare more, 3 to 4 g of nitrogen (Cahill 1983).
        /// </summary>
        public const double ProteinShareLate = 0.12;

        /// <summary>
        /// The time constant of the fasting body's adaptation, days: 7, running while the liver's glycogen is below
        /// <see cref="VeryHungryLiver"/> of its store, which in a fast is from about the 22nd hour, the end of the postabsorptive
        /// phase ("6 to 24 hours after beginning fasting, during which cerebral glucose requirements are maintained primarily via
        /// glycogenolysis", Felig's stages as Kerndt et al. 1982, p. 385, give them). An estimate: ketosis from the third or
        /// fourth day and the brain on ketones from the second week (Cahill 1983), a lean faster's nitrogen falling "gradually
        /// over the next two weeks" (Kerndt et al. 1982).
        /// </summary>
        public const double AdaptationDays = 7.0;

        /// <summary>
        /// The time constant of the adaptation's undoing while the liver's glycogen is full again (at <see cref="HungryLiver"/> of
        /// its store or more), days: 1. An estimate: eating ends the fast. Between the two marks the adaptation holds.
        /// </summary>
        public const double RefedDays = 1.0;

        /// <summary>
        /// The fall of the basal rate per unit of active tissue a fully adapted body shows: 0.155, the Minnesota men's after six
        /// months (Keys et al. 1950, <i>The Biology of Human Starvation</i>, p. 329: "a decline of 15.5 per cent"); with the 26.9
        /// per cent of their active tissue they lost (p. 330), their basal rate fell 38.89 per cent a man (p. 328), which this
        /// reproduces within a point. A total fast's 10 to 15 per cent (Cahill 1983) and 12 per cent at ten days (Laurens et al.
        /// 2021) are of the same order.
        /// </summary>
        public const double BasalAdaptation = 0.155;

        /// <summary>The share of body mass that is active tissue: 0.575, the Minnesota men's before (38.80 of 67.53 kg, Keys et al. 1950, table 166).</summary>
        public const double ActiveTissueShare = 38.80 / 67.53;

        /// <summary>
        /// The active tissue lost with each kilogram of protein, kg: 10.11 / 2.5, the Minnesota men's "10.11 kg. of active
        /// tissue", which, "free of extracellular water, represents roughly 2.5 kg. of protein" (Keys et al. 1950, p. 333).
        /// </summary>
        public const double ActiveTissuePerProtein = 10.11 / 2.5;

        /// <summary>
        /// The salt and water a fast sheds in its first days, kg: 2.25, "saline diuresis in non-obese 2-2.5 Kg" (Cahill 1983,
        /// p. 14); the reason a fast's first week loses "0.9 kg per day" and the third "0.3 kg" (Kerndt et al. 1982). It comes with
        /// the adaptation, at twice its pace, and goes back on refeeding; it is weight on the scale and not the thirst's account.
        /// </summary>
        public const double SaltWaterKg = 2.25;

        /// <summary>
        /// The time constant of absorbing a meal, hours: 3. An estimate within "gastrointestinal absorption of substrate, 1–8
        /// hours" (Cahill 1983, the first phase of starvation).
        /// </summary>
        public const double GutHours = 3.0;

        /// <summary>
        /// The share of the body's maximal insulation held in its fat and skin: 0.125. That superficial shell "could account for
        /// only 10-15% of overall It,max at rest", its insulation linear in the fat's thickness (Veicsteinas, Ferretti and Rennie
        /// 1982, Journal of Applied Physiology 52:1557–1564). What the fat lost takes of the tissue's insulation.
        /// </summary>
        public const double FatShellInsulationShare = 0.125;

        // ---- the words and the capacity, by weight lost ----

        /// <summary>
        /// The words' thresholds: hungry when the liver has given a fifth of its glycogen (about five hours after a meal, when
        /// "liver begins to return its stored glycogen", Cahill 1983), very hungry at three-fifths (the first day), weak once the
        /// fast has adapted a third (the fourth day, ketosis having subdued "the voracious sensation of hunger experienced during
        /// the first days of fasting", World Medical Association 2006), starving at a tenth of body weight lost ("Close medical monitoring is recommended after a 10% of
        /// weight loss", Gétaz et al. 2012) and wasting at 18 per cent ("Serious medical problems begin at a loss of approximately
        /// 18%", the same).
        /// </summary>
        public const double HungryLiver = 0.8, VeryHungryLiver = 0.4, WeakAdaptation = 0.35, StarvingWeightLoss = 0.10, WastingWeightLoss = 0.18;

        // What the deficit leaves of the founder's work, by the share of body weight lost, interpolated from the published figures:
        // nothing below 5 per cent (Friedl 1995, "When does energy deficit affect soldier physical performance?", Institute of
        // Medicine), a first decline at about 10 per cent, maximal lift 24 per cent down at 16 per cent lost (Ranger-I, the same),
        // back strength 28 and aerobic capacity 43 per cent down at the Minnesota men's 24 per cent (Keys et al. 1950), and, an
        // estimate, 0.4 near death.
        private static readonly double[] LossPoints = { 0.00, 0.05, 0.10, 0.16, 0.24, 0.30 };
        private static readonly double[] CapacityPoints = { 1.00, 1.00, 0.95, 0.76, 0.645, 0.40 };

        // ---- the state ----

        /// <summary>Glycogen in the liver, kg.</summary>
        public double LiverKg { get; private set; } = LiverGlycogenKg;
        /// <summary>Glycogen in the muscles, kg.</summary>
        public double MuscleKg { get; private set; } = MuscleGlycogenKg;
        /// <summary>Body fat, kg.</summary>
        public double FatKg { get; private set; } = StartFatKg;
        /// <summary>Body protein burnt, kg.</summary>
        public double ProteinLostKg { get; private set; }
        /// <summary>How far the body has adapted to a fast, 0 fed to 1.</summary>
        public double Adaptation01 { get; private set; }
        /// <summary>Food eaten and not yet absorbed, J.</summary>
        public double GutJ { get; private set; }

        /// <summary>The fat a founder starts with, kg.</summary>
        public static double StartFatKg => StartFatShare * Warmth.MassKg;
        /// <summary>The fat a fast cannot burn, kg.</summary>
        public static double FatFloorKg => FatFloorShare * Warmth.MassKg;

        /// <summary>The lean tissue lost with the protein, kg: the scale's account (Hall 2008).</summary>
        public double LeanLostKg => ProteinLostKg * (1.0 + LeanWaterPerProtein);

        /// <summary>The active tissue lost with the protein, kg: the basal rate's account (Keys et al. 1950).</summary>
        public double ActiveTissueLostKg => ProteinLostKg * ActiveTissuePerProtein;

        /// <summary>The share of the fat that can burn which has been burnt, 0 to 1.</summary>
        public double FatSpent01 => SimMath.Clamp01((StartFatKg - FatKg) / (StartFatKg - FatFloorKg));

        /// <summary>
        /// The share of the stores' deficit paid from protein now, from the early share to the late one as the fast adapts; and
        /// the share of a surplus that gives protein lost back, since "for a given individual, the P-ratio during refeeding is
        /// strongly correlated with the P-ratio during semi-starvation" (Dulloo, Jacquet and Girardier 1996, "Autoregulation of
        /// body composition during weight recovery in human: the Minnesota Experiment revisited", International Journal of
        /// Obesity 20:393–405).
        /// </summary>
        public double ProteinShare01 => ProteinShareLate + (ProteinShareEarly - ProteinShareLate) * (1.0 - Adaptation01);

        /// <summary>The salt and water shed by the fast now, kg.</summary>
        public double SaltWaterLostKg => SaltWaterKg * (1.0 - Math.Pow(1.0 - Adaptation01, 3.5));

        /// <summary>The body weight lost, kg: glycogen with its water, fat, lean tissue and the fast's salt and water.</summary>
        public double WeightLostKg =>
            (LiverGlycogenKg - LiverKg + MuscleGlycogenKg - MuscleKg) * (1.0 + GlycogenWaterPerKg)
            + Math.Max(0.0, StartFatKg - FatKg) + LeanLostKg + SaltWaterLostKg;

        /// <summary>The share of body weight lost.</summary>
        public double WeightLoss01 => WeightLostKg / Warmth.MassKg;

        /// <summary>The word for it now.</summary>
        public HungerLevel Level
        {
            get
            {
                double loss = WeightLoss01;
                if (loss >= WastingWeightLoss) return HungerLevel.Wasting;
                if (loss >= StarvingWeightLoss) return HungerLevel.Starving;
                if (Adaptation01 >= WeakAdaptation) return HungerLevel.Weak;
                double liver = LiverKg / LiverGlycogenKg;
                if (liver < VeryHungryLiver) return HungerLevel.VeryHungry;
                if (liver < HungryLiver) return HungerLevel.Hungry;
                return HungerLevel.Fed;
            }
        }

        /// <summary>The word the founder would use; nothing while fed.</summary>
        public static string WordFor(HungerLevel level)
        {
            switch (level)
            {
                case HungerLevel.Hungry: return "hungry";
                case HungerLevel.VeryHungry: return "very hungry";
                case HungerLevel.Weak: return "weak with hunger";
                case HungerLevel.Starving: return "starving";
                case HungerLevel.Wasting: return "wasting";
                default: return string.Empty;
            }
        }

        /// <summary>What the deficit leaves of the founder's work, 0 to 1, by the table.</summary>
        public double WorkCapacity01 => CapacityOf(WeightLoss01);

        /// <summary>What a body that has lost this share of its weight can do, 0 to 1; the one owner for every reader.</summary>
        public static double CapacityOf(double weightLoss01)
        {
            double loss = Math.Max(0.0, weightLoss01);
            if (loss >= LossPoints[LossPoints.Length - 1]) return CapacityPoints[CapacityPoints.Length - 1];
            for (int i = 1; i < LossPoints.Length; i++)
            {
                if (loss > LossPoints[i]) continue;
                double t = (loss - LossPoints[i - 1]) / (LossPoints[i] - LossPoints[i - 1]);
                return CapacityPoints[i - 1] + t * (CapacityPoints[i] - CapacityPoints[i - 1]);
            }
            return CapacityPoints[CapacityPoints.Length - 1];
        }

        /// <summary>
        /// The share of its basal heat a starving body still makes: its active tissue lost, and the adapted fall per unit of what
        /// is left (<see cref="BasalAdaptation"/>), as far on as the fast's adaptation or the fat spent, whichever is further. A
        /// total fast brings the fall with its ketosis; the Minnesota men, underfed and never fasting, brought it with their fat,
        /// and on refeeding their adjusted basal rate stayed down "by a magnitude which is inversely proportional to the degree of
        /// fat recovery" (Dulloo, Jacquet and Girardier 1996). The hook for <see cref="Warmth.BasalHeatW"/> (the contract): the
        /// Minnesota men, "increased sensitivity to cold", whose "sole detriment that can be ascribed to the reduced metabolism
        /// itself" it was (Keys et al. 1950, p. 338).
        /// </summary>
        public double BasalShare01 =>
            (1.0 - BasalAdaptation * Math.Max(Adaptation01, FatSpent01))
            * Math.Max(0.0, 1.0 - ActiveTissueLostKg / (ActiveTissueShare * Warmth.MassKg));

        /// <summary>The share of the tissue's insulation left as the fat goes, the hook for <see cref="Warmth"/>'s tissue insulation.</summary>
        public double TissueInsulationShare01 =>
            1.0 - FatShellInsulationShare * SimMath.Clamp01((StartFatKg - FatKg) / StartFatKg);

        /// <summary>Whether the protein lost is short of what kills. The server acts on it (part two): death by starvation.</summary>
        public bool IsAlive => ProteinLostKg < LethalProteinShare * BodyProteinKg;

        /// <summary>Food eaten: its energy as the body will absorb it (<see cref="Food.EnergyJ(double, FoodPreparation)"/>), J.</summary>
        public void Eat(double joules)
        {
            if (joules > 0.0) GutJ += joules;
        }

        /// <summary>
        /// The body's seconds going by, spending <paramref name="spendW"/> (the balance's metabolic watts), of which
        /// <paramref name="restingW"/> is what it would spend at rest: the gut absorbing, the stores paying the deficit in their
        /// order or refilled by a surplus, and the fast adapting while the liver is low or undone while it is full. The clock is
        /// the caller's, as the water's is.
        /// </summary>
        public void Advance(double seconds, double spendW, double restingW)
        {
            if (!(seconds > 0.0)) return;
            double absorbed = GutJ * (1.0 - Math.Exp(-seconds / (GutHours * 3600.0)));
            GutJ -= absorbed;
            double net = absorbed - Math.Max(0.0, spendW) * seconds;
            if (net >= 0.0) Refill(net);
            else Draw(-net, Math.Max(0.0, spendW - restingW) * seconds, seconds);
            double liver = LiverKg / LiverGlycogenKg, days = seconds / 86400.0;
            if (liver < VeryHungryLiver) Adaptation01 = 1.0 - (1.0 - Adaptation01) * Math.Exp(-days / AdaptationDays);
            else if (liver >= HungryLiver) Adaptation01 *= Math.Exp(-days / RefedDays);
        }

        /// <summary>A deficit paid from the stores in their order: the liver's glycogen, the muscles' for work, then fat and protein.</summary>
        private void Draw(double deficitJ, double workJ, double seconds)
        {
            // The liver's glycogen, exponentially over about a day.
            double liver = Math.Min(LiverKg * (1.0 - Math.Exp(-seconds / (LiverDrawHours * 3600.0))), deficitJ / GlycogenJPerKg);
            LiverKg -= liver;
            deficitJ -= liver * GlycogenJPerKg;
            // The muscles' glycogen pays half of any work above rest, while it lasts.
            double muscle = Math.Min(MuscleKg, Math.Min(WorkGlycogenShare * workJ * (MuscleKg / MuscleGlycogenKg), deficitJ) / GlycogenJPerKg);
            MuscleKg -= muscle;
            deficitJ -= muscle * GlycogenJPerKg;
            // Fat and protein in the fasting body's shares; past the fat that can burn, protein pays all.
            double fatJ = Math.Min(deficitJ * (1.0 - ProteinShare01), Math.Max(0.0, FatKg - FatFloorKg) * FatJPerKg);
            FatKg -= fatJ / FatJPerKg;
            ProteinLostKg += (deficitJ - fatJ) / ProteinJPerKg;
        }

        /// <summary>
        /// A surplus refills the liver's glycogen, then the muscles', then gives back protein lost in <see cref="ProteinShare01"/>
        /// of what is left, and lays the rest down as fat. What it costs to lay tissue down is not charged.
        /// </summary>
        private void Refill(double joules)
        {
            double liver = Math.Min(LiverGlycogenKg - LiverKg, joules / GlycogenJPerKg);
            LiverKg += liver;
            joules -= liver * GlycogenJPerKg;
            double muscle = Math.Min(MuscleGlycogenKg - MuscleKg, joules / GlycogenJPerKg);
            MuscleKg += muscle;
            joules -= muscle * GlycogenJPerKg;
            double protein = Math.Min(ProteinLostKg, joules * ProteinShare01 / ProteinJPerKg);
            ProteinLostKg -= protein;
            joules -= protein * ProteinJPerKg;
            FatKg += joules / FatJPerKg;
        }

        /// <summary>A body put back as a save states it; each store clamped to what a body holds, NaN full.</summary>
        public void Restore(double liverKg, double muscleKg, double fatKg, double proteinLostKg, double adaptation01, double gutJ)
        {
            LiverKg = double.IsNaN(liverKg) ? LiverGlycogenKg : SimMath.Clamp(liverKg, 0.0, LiverGlycogenKg);
            MuscleKg = double.IsNaN(muscleKg) ? MuscleGlycogenKg : SimMath.Clamp(muscleKg, 0.0, MuscleGlycogenKg);
            FatKg = double.IsNaN(fatKg) ? StartFatKg : Math.Max(0.0, fatKg);
            ProteinLostKg = double.IsNaN(proteinLostKg) ? 0.0 : SimMath.Clamp(proteinLostKg, 0.0, BodyProteinKg);
            Adaptation01 = double.IsNaN(adaptation01) ? 0.0 : SimMath.Clamp01(adaptation01);
            GutJ = double.IsNaN(gutJ) ? 0.0 : Math.Max(0.0, gutJ);
        }
    }
}
