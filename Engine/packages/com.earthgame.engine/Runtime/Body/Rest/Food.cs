using System;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>How a food was made ready to eat (BF.7). Never renumbered: part two saves it with the food.</summary>
    public enum FoodPreparation : byte
    {
        /// <summary>As it was dug, picked or sucked from the flower.</summary>
        Raw = 0,
        /// <summary>Heated through in a fire's coals or ashes, its starch gelatinised.</summary>
        Roasted = 1,
    }

    /// <summary>
    /// A food (BF.7 part one): the edible part of a plant as its published composition per 100 g of the part as analysed,
    /// with its water, so what it gives a body falls out of what it is made of (GAME_DESIGN §8), never out of a label. The
    /// energy is the Australian Food Composition Database's own equation (<see cref="ProteinKJPerG"/> and the factors beside
    /// it) taken at what the gut actually digests: starch as its digestibility raw or cooked, protein likewise, fibre at the
    /// colon's yield unless the fibre is spat out, and every dry-matter term scaled by the food's water, so a rhizome dried
    /// in the shade is richer by the kilogram than one just dug.
    ///
    /// <para>The table's rows are the foods of the beta's chain G8. Their sources are thin: Brand Miller, James and
    /// Maggiore's <i>Tables of Composition of Australian Aboriginal Foods</i> (1993) could not be read, and each row says
    /// whose numbers it carries and which of them are estimates.</para>
    /// </summary>
    public sealed class Food
    {
        // ---- the energy factors: FSANZ, Australian Food Composition Database Release 3 (2025) ----

        /// <summary>
        /// Energy per gram of protein, kJ: 17. The AFCD's equation, "Energy = protein*17 + sugars*16 + other available
        /// carbohydrate (starch …)*17 + fat*37 + dietary fibre*8 …" (FSANZ, AFCD Release 3, 2025, Nutrient details,
        /// Proximates), NUTTAB 2010's before it; FAO's Atwater general factors are the same (FAO 2003, <i>Food energy: methods of
        /// analysis and conversion factors</i>, Food and Nutrition Paper 77).
        /// </summary>
        public const double ProteinKJPerG = 17.0;
        /// <summary>Energy per gram of fat, kJ: 37 (the same equation).</summary>
        public const double FatKJPerG = 37.0;
        /// <summary>Energy per gram of sugars, kJ: 16 (the same equation; FAO 2003's 16 kJ/g for carbohydrate as monosaccharide).</summary>
        public const double SugarsKJPerG = 16.0;
        /// <summary>Energy per gram of starch digested, kJ: 17 (the same equation's "other available carbohydrate").</summary>
        public const double StarchKJPerG = 17.0;
        /// <summary>
        /// Energy per gram of dietary fibre, and of any carbohydrate that reaches the colon, kJ: 8. "A caloric value of about
        /// 2 kcal/g (8 kJ/g) … would be a reasonable average figure for carbohydrate which reaches the colon" (FAO/WHO 1998,
        /// <i>Carbohydrates in human nutrition</i>, FAO Food and Nutrition Paper 66, ch. 1); the AFCD's fibre factor.
        /// </summary>
        public const double ColonKJPerG = 8.0;

        // ---- what the gut digests ----

        /// <summary>
        /// The share of a raw tuber's starch the small intestine digests: 0.50. Tubers and green bananas store "Type B" starch,
        /// "more resistant than Type A to pancreatic amylase"; its ileal digestibility raw was 47.3 and 49.4 per cent for green
        /// banana in people (Langkilde et al. 2002; Muir et al. 1995), 53.6 for plantain and 50.7 for potato in vitro (Englyst
        /// and Cummings 1986, 1987), as Carmody and Wrangham gather them (2009, "The energetic significance of cooking", Journal
        /// of Human Evolution 57:379–391, table 3). What escapes is fermented in the colon at <see cref="ColonKJPerG"/>.
        /// </summary>
        public const double StarchDigestedRaw = 0.50;
        /// <summary>
        /// The share of a cooked tuber's starch the small intestine digests: 0.97. Cooked potato 96.7 per cent, cooked green banana
        /// 96.9 and 98.8 (the same table): heat gelatinises the granules and the amylase reaches them. Cooking so gives a tuber
        /// about 30 per cent more energy, which is Carmody and Wrangham's own figure for potato (30.5).
        /// </summary>
        public const double StarchDigestedCooked = 0.97;
        /// <summary>
        /// The share of raw protein digested: 0.65, an estimate for these plants' little protein, from egg, the one food measured
        /// both ways in people: 51 per cent in ileostomy patients and 65 in healthy volunteers raw, 91 to 94 cooked (Evenepoel et
        /// al. 1998, Journal of Nutrition 128:1716–1722, as Carmody and Wrangham 2009 report them).
        /// </summary>
        public const double ProteinDigestedRaw = 0.65;
        /// <summary>The share of cooked protein digested: 0.91 (the same measurements: cooked egg protein 90.9 per cent in ileostomy patients).</summary>
        public const double ProteinDigestedCooked = 0.91;

        // ---- the row ----

        /// <summary>The row's key, stable for the wire and the save.</summary>
        public string Name { get; }
        /// <summary>The food as a person names it: "bracken rhizome".</summary>
        public string DisplayName { get; }
        /// <summary>The plant of the world's catalogue it comes from, or null for a plant the catalogue does not yet grow.</summary>
        public PlantSpecies Species { get; }
        /// <summary>The water in the part as analysed, as a share of its fresh mass.</summary>
        public double WaterFresh { get; }
        /// <summary>Grams per 100 g of the part as analysed: protein, fat, sugars, starch (or all available carbohydrate), fibre.</summary>
        public double ProteinG { get; }
        public double FatG { get; }
        public double SugarsG { get; }
        public double StarchG { get; }
        public double FibreG { get; }
        /// <summary>Whether its fibre is chewed and spat out rather than swallowed, as bracken's is.</summary>
        public bool FibreSpatOut { get; }
        /// <summary>Whose numbers these are, and which of them are estimates.</summary>
        public string Source { get; }

        private Food(string name, string displayName, PlantSpecies species, double waterFresh, double proteinG, double fatG,
                     double sugarsG, double starchG, double fibreG, bool fibreSpatOut, string source)
        {
            Name = name;
            DisplayName = displayName;
            Species = species;
            WaterFresh = waterFresh;
            ProteinG = proteinG;
            FatG = fatG;
            SugarsG = sugarsG;
            StarchG = starchG;
            FibreG = fibreG;
            FibreSpatOut = fibreSpatOut;
            Source = source;
        }

        /// <summary>
        /// The energy the AFCD's equation gives 100 g of it as analysed, kJ, every gram counted as available: the figure a food
        /// table prints, which is what the gut would get were it to digest everything it could.
        /// </summary>
        public double TableKJPer100g =>
            ProteinG * ProteinKJPerG + FatG * FatKJPerG + SugarsG * SugarsKJPerG + StarchG * StarchKJPerG + FibreG * ColonKJPerG;

        /// <summary>
        /// The energy a body gets from 100 g of it, kJ, prepared so and holding <paramref name="moisture"/> of its fresh mass as
        /// water: the table's equation at what the gut digests (the starch and the protein by <paramref name="preparation"/>, the
        /// starch that escapes and the fibre fermented in the colon, the fibre nothing when it is spat out), its dry matter
        /// scaled from the water it was analysed at to the water it has.
        /// </summary>
        public double KJPer100g(FoodPreparation preparation, double moisture)
        {
            bool cooked = preparation == FoodPreparation.Roasted;
            double starch = cooked ? StarchDigestedCooked : StarchDigestedRaw;
            double protein = cooked ? ProteinDigestedCooked : ProteinDigestedRaw;
            double kj = ProteinG * ProteinKJPerG * protein
                        + FatG * FatKJPerG
                        + SugarsG * SugarsKJPerG
                        + StarchG * (StarchKJPerG * starch + ColonKJPerG * (1.0 - starch))
                        + (FibreSpatOut ? 0.0 : FibreG * ColonKJPerG);
            return kj * DryMatterScale(moisture);
        }

        /// <summary>The same at the water it was analysed at.</summary>
        public double KJPer100g(FoodPreparation preparation) => KJPer100g(preparation, WaterFresh);

        /// <summary>The energy a body gets from <paramref name="kg"/> of it, J.</summary>
        public double EnergyJ(double kg, FoodPreparation preparation, double moisture) =>
            Math.Max(0.0, kg) * 10.0 * KJPer100g(preparation, moisture) * 1000.0;

        /// <summary>The same at the water it was analysed at.</summary>
        public double EnergyJ(double kg, FoodPreparation preparation) => EnergyJ(kg, preparation, WaterFresh);

        /// <summary>How many kilograms of it, prepared so and at the water it was analysed at, give <paramref name="joules"/>.</summary>
        public double KgFor(double joules, FoodPreparation preparation)
        {
            double perKg = EnergyJ(1.0, preparation);
            return perKg > 0.0 ? Math.Max(0.0, joules) / perKg : double.PositiveInfinity;
        }

        /// <summary>
        /// How the dry matter of 100 g scales from the water it was analysed at to <paramref name="moisture"/>: the same solids in
        /// less water weigh less, so a kilogram of them is richer.
        /// </summary>
        private double DryMatterScale(double moisture)
        {
            double m = SimMath.Clamp(moisture, 0.0, 0.99);
            double dry = 1.0 - WaterFresh;
            return dry > 0.0 ? (1.0 - m) / dry : 0.0;
        }

        /// <summary>A meal in a person's words: "0.5 kg of roasted bracken rhizome, about 430 kJ".</summary>
        public string Describe(double kg, FoodPreparation preparation)
        {
            string mass = ThingWords.MassWord(kg);
            string how = preparation == FoodPreparation.Roasted ? "roasted " : "raw ";
            double kj = EnergyJ(kg, preparation) / 1000.0;
            return mass + " of " + how + DisplayName + ", about " + Math.Round(kj).ToString("0", CultureInfo.InvariantCulture) + " kJ";
        }

        // ---- the foods of chain G8 ----

        /// <summary>
        /// Lomandra's white leaf bases, eaten raw ("Leaf bases edible", Flood 1980; tasting of "fresh green peas", Cribb and Cribb
        /// 1987; both as the Australian National Botanic Gardens' <i>Aboriginal Plant Use – Southern Tablelands and the ACT</i>,
        /// Nash 2003, gives them). No analysis of them was found: the composition is an estimate, the mean of the "miscellaneous
        /// vegetables (pith, stalks, buds)" the Aboriginal food tables analysed, wet weight: water 78, protein 3, fat 0.7,
        /// carbohydrate 15, fibre 8 g per 100 g (Brand-Miller and Holt 1998, "Australian Aboriginal plant foods: a consideration
        /// of their nutritional composition and health implications", Nutrition Research Reviews 11:5–23, table 1), whose
        /// authors warn that the carbohydrate "is in fact" largely non-starch polysaccharide, so it is counted here as starch
        /// digested raw, an upper bound.
        /// </summary>
        public static readonly Food LomandraLeafBase = new Food(
            "LomandraLeafBase", "lomandra leaf base", PlantSpecies.Lomandra, 0.78, 3.0, 0.7, 0.0, 15.0, 8.0, false,
            "Brand-Miller and Holt 1998, table 1: the class mean for pith, stalks and buds (an estimate; no analysis of the plant)");

        /// <summary>
        /// Bracken's rhizome, <i>Pteridium esculentum</i>, "roasted and beaten into a paste" (Zola and Gott 1992) or "eaten both
        /// raw and roasted" (Maiden 1889, <i>The Useful Native Plants of Australia</i>); in New Zealand heated, beaten "to
        /// separate the starch from the fibre", chewed, and the "fibre wad" spat out (McGlone, Wilmshurst and Leach 2005, New
        /// Zealand Journal of Ecology 29:165–184). Dry weight: protein 2.0, fat 1.0, carbohydrate 47.6 (by difference) and fibre
        /// 46.6 g per 100 g (Brand Miller et al. 1993, as Hodgson and Wahlqvist 1993 give it, Asia Pacific Journal of Clinical
        /// Nutrition 2:43–57, table 2), at the rhizome's "c. 90% water" (Smith 1986, as McGlone et al. give it). The carbohydrate
        /// is taken as starch; the starch measured in the rhizome is 10 to 30 per cent of its dry weight (Williams and Foley
        /// 1976; Al-Jaff et al. 1982; as McGlone et al. give them), so the table's by-difference figure is the upper bound.
        /// </summary>
        public static readonly Food BrackenRhizome = new Food(
            "BrackenRhizome", "bracken rhizome", PlantSpecies.Bracken, 0.90, 0.20, 0.10, 0.0, 4.76, 4.66, true,
            "Brand Miller et al. 1993 via Hodgson and Wahlqvist 1993 (dry weight), at McGlone et al. 2005's 90 % water");

        /// <summary>
        /// The long yam's tuber, <i>Dioscorea transversa</i>, "eaten after cooking, usually in ground ovens" (Low 1988), its
        /// small young tubers raw (Thozet, in Maiden 1889). Cooked: water 68, protein 3.2, fat 0.3 g per 100 g from wild tubers
        /// (Brand Miller et al. 1993), starch 20.4, sugars 0.5 and fibre 3.5 imputed from boiled potato: the AFCD's food F009604,
        /// "Yam, wild harvested, cooked", 448 kJ (FSANZ, AFCD Release 3). The one yam of New South Wales grows "north from
        /// Stanwell Tops" (PlantNET, NSW Flora Online): none on the South Coast, and none in the catalogue.
        /// </summary>
        public static readonly Food LongYamTuber = new Food(
            "LongYamTuber", "long yam tuber", null, 0.68, 3.2, 0.3, 0.5, 20.4, 3.5, false,
            "FSANZ AFCD Release 3, F009604 (Brand Miller et al. 1993's proximates; starch, sugars and fibre imputed from potato)");

        /// <summary>
        /// A greenhood orchid's paired tubers, <i>Pterostylis nutans</i>, "eaten raw or cooked … often abundant – 439 plants/sq.m"
        /// (Gott 1995, as Nash 2003 gives it); the tubers of the south-east's orchids were "roasted" (Flood 1980) and store
        /// "starch, high amylopectins" (Gott 2008, Telopea 12:215–226). Dry weight: protein 14.4, fat 1.3, carbohydrate 77.0 and
        /// fibre 5.3 g per 100 g (Brand Miller et al. 1993, as Hodgson and Wahlqvist 1993, table 2, give it; their bird orchid,
        /// <i>Chiloglottis</i>, 10.0, 1.1, 56.7 and 26.7). Its water was not reported: 84 per cent, the mean Hodgson and Wahlqvist
        /// give for the roots analysed, is an estimate.
        /// </summary>
        public static readonly Food GreenhoodTuber = new Food(
            "GreenhoodTuber", "greenhood orchid tuber", null, 0.84, 2.30, 0.21, 0.0, 12.32, 0.85, false,
            "Brand Miller et al. 1993 via Hodgson and Wahlqvist 1993 (dry weight), at an estimated 84 % water");

        /// <summary>
        /// Pigface fruit, <i>Carpobrotus</i>: "The fleshy fruit is eaten raw by the aborigines" (Maiden 1889), in "the 'Karkalla'
        /// season, which lasts from January to the end of summer" (Wilhelmi 1860, in Maiden). The one Australian analysis is of
        /// <i>C. rossii</i>: water 84.4 per cent, and per 100 g dry weight protein 3.9, fat 1.0, fibre 11 and ash 2.6 (Njume 2020,
        /// PhD thesis, Victoria University, table 3a.1); the carbohydrate is the rest by difference and is taken as sugars, a
        /// fleshy fruit's, an estimate (its sugars were not measured). <i>C. glaucescens</i>, this coast's, has no analysis, and
        /// no pigface is yet in the catalogue.
        /// </summary>
        public static readonly Food PigfaceFruit = new Food(
            "PigfaceFruit", "pigface fruit", null, 0.844, 0.61, 0.16, 12.71, 0.0, 1.72, false,
            "Njume 2020 (C. rossii, dry weight, at 84.4 % water); carbohydrate by difference, taken as sugars (an estimate)");

        /// <summary>
        /// Banksia nectar, "eagerly sucked out by the aborigines" (Maiden 1889), or the heath banksia's cobs broken off and swirled
        /// "in water to make a real sweet honey drink" (Wreck Bay Community and Renwick 2000, as Nash 2003 gives it). Its sugar:
        /// "35° Brix sucrose equivalent", in a Western Australian banksia's nectar (Wawrzyczek et al. 2024, Botanical Journal of
        /// the Linnean Society 206:257–273); none was found measured for this coast's banksias, so the figure is an estimate for
        /// them, and so is <see cref="NectarPerSpikeKg"/>.
        /// </summary>
        public static readonly Food BanksiaNectar = new Food(
            "BanksiaNectar", "banksia nectar", PlantSpecies.HeathBanksia, 0.65, 0.0, 0.0, 35.0, 0.0, 0.0, false,
            "Wawrzyczek et al. 2024 (35 Brix, a Western Australian banksia; an estimate for this coast's)");

        /// <summary>
        /// The nectar standing in one flowering spike of a morning, kg: 1.9 g, the "up to 1.9 mL standing nectar crop per
        /// inflorescence" of <i>Banksia catoglypta</i> (Wawrzyczek et al. 2024) taken at a gram a millilitre, so its sugar, 0.67 g,
        /// is a lower bound. An estimate for this coast's banksias: a spike is a mouthful of sweetness, some ten kilojoules, and
        /// a day's food would be a thousand of them.
        /// </summary>
        public const double NectarPerSpikeKg = 0.0019;

        /// <summary>Every food of the table, for the tests and the tablet.</summary>
        public static readonly Food[] All = { LomandraLeafBase, BrackenRhizome, LongYamTuber, GreenhoodTuber, PigfaceFruit, BanksiaNectar };

        /// <summary>The row by its key, or null.</summary>
        public static Food Named(string name)
        {
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i].Name, name, StringComparison.Ordinal)) return All[i];
            return null;
        }
    }
}
