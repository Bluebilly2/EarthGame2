using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>
    /// Wood as a material (BF.1, 2026-09-22): what a stick, a log, a hearth board or a drill of a species is made of, by
    /// the numbers a person who knows timber would dispute. Following GAME_DESIGN §8 and <see cref="StoneType"/>'s pattern, a
    /// wood is not a label with a use attached: what a drill and a hearth want of it, and what a fire gets from it, fall out
    /// of its density, its hardness and its water, and nothing here says "the fire-drill wood".
    ///
    /// <para>The numbers: air-dry density and hardness from Bootle, <i>Wood in Australia</i> (2005), for the commercial
    /// eucalypts and the paperbark, and from Ilic, Boland, McDonald, Downes and Blakemore, <i>Woody density of Australian
    /// plants</i> (CSIRO, 2000), for the banksias; the heat of dry wood one figure for all wood, 19.0 MJ/kg, inside the
    /// Wood Handbook's 18–21; green wood's water as the Wood Handbook's 50–100 % of dry mass by the wood's kind. Each row
    /// names its source. They were written from memory of those tables and are owed a re-reading against the tables
    /// themselves before a fire's physics leans on them (DEBTS 2026-09-22); the grass tree's stalk carries an estimate and
    /// says so.</para>
    /// </summary>
    public sealed class Wood
    {
        /// <summary>The heat of combustion of oven-dry wood, MJ/kg: one figure for wood, inside the Wood Handbook's 18–21 across species.</summary>
        public const double HeatOfWoodMJPerKgDry = 19.0;

        public PlantSpecies Species { get; }

        /// <summary>Air-dry density (about 12 % moisture), kg/m³.</summary>
        public double DensityDryKgM3 { get; }

        /// <summary>Water in green wood as a share of its dry mass (0.6 is 60 %); wood dries toward the air's 0.12.</summary>
        public double GreenMoisture { get; }

        /// <summary>Janka hardness, kN, dry: the force to press a half-inch ball halfway into the wood.</summary>
        public double JankaKN { get; }

        /// <summary>Modulus of rupture, MPa, dry: the bending stress at which a beam of it breaks.</summary>
        public double RuptureMPa { get; }

        /// <summary>The heat of combustion of the dry wood, MJ/kg.</summary>
        public double HeatMJPerKgDry { get; }

        /// <summary>What it is, in a sentence, for the tablet to report.</summary>
        public string Description { get; }

        /// <summary>Where the numbers come from, and what in them is an estimate.</summary>
        public string Source { get; }

        private Wood(PlantSpecies species, double densityDryKgM3, double greenMoisture, double jankaKN, double ruptureMPa, string description, string source)
        {
            Species = species;
            DensityDryKgM3 = densityDryKgM3;
            GreenMoisture = greenMoisture;
            JankaKN = jankaKN;
            RuptureMPa = ruptureMPa;
            HeatMJPerKgDry = HeatOfWoodMJPerKgDry;
            Description = description;
            Source = source;
        }

        /// <summary>
        /// How soft the wood is, 0 to 1, from its hardness alone: a kilonewton of Janka is as soft as wood gets, ten is a
        /// blackbutt's floor. The axis a drill and a hearth are chosen on, and the one a carving edge feels.
        /// </summary>
        public double Softness01 => SimMath.Clamp01(1.0 - (JankaKN - 1.0) / 9.0);

        /// <summary>
        /// How readily this wood takes a coal by friction, 0 to 1: the band of density friction fire works in. Falls out of
        /// the density (<see cref="FrictionFireOf"/>); a banksia or a paperbark is in the band, a blackbutt is not.
        /// </summary>
        public double FrictionFire01 => FrictionFireOf(DensityDryKgM3);

        /// <summary>
        /// The friction-fire band by density, kg/m³: nothing below 200 (punk, which crumbles), rising to everything at 300, everything
        /// to 550, falling to nothing at 850 (a wood that dense polishes instead of powdering). The ethnographic hearth and drill
        /// woods sit in the 300–650 band: willow, cedar, sotol, the banksias and the paperbarks here.
        /// </summary>
        public static double FrictionFireOf(double densityDryKgM3)
        {
            if (densityDryKgM3 <= 200.0 || densityDryKgM3 >= 850.0) return 0.0;
            if (densityDryKgM3 < 300.0) return (densityDryKgM3 - 200.0) / 100.0;
            if (densityDryKgM3 <= 550.0) return 1.0;
            return (850.0 - densityDryKgM3) / 300.0;
        }

        public override string ToString() => Species.Name + " wood";

        // ---- the table: one row per woody plant, its source beside it ----

        private const string GreenWater = "green wood's water from the Wood Handbook's 50–100 % of dry mass by the wood's kind";
        private const string FromMemory = "read from memory of the table and owed a re-reading against it (DEBTS 2026-09-22)";

        public static readonly Wood Blackbutt = new Wood(PlantSpecies.Blackbutt, 900.0, 0.60, 9.1, 116.0,
            "A dense, hard, pale hardwood; a poor drill and hearth, a long-burning fuel.",
            "Bootle 2005 (Wood in Australia): blackbutt, Eucalyptus pilularis, air-dry density 900 kg/m3, Janka 9.1 kN dry, modulus of rupture 116 MPa dry; " + GreenWater + "; " + FromMemory);

        public static readonly Wood Bangalay = new Wood(PlantSpecies.Bangalay, 900.0, 0.60, 8.5, 100.0,
            "A dense red hardwood like the mahoganies; fibrous bark to the branches.",
            "Bootle 2005: bangalay (southern mahogany), Eucalyptus botryoides, air-dry density about 900 kg/m3, Janka about 8.5 kN; " + GreenWater + "; " + FromMemory);

        public static readonly Wood OldManBanksia = new Wood(PlantSpecies.OldManBanksia, 700.0, 0.90, 4.6, 70.0,
            "A medium-density, soft, pinkish wood with a coarse open grain; takes a coal by friction.",
            "Ilic et al. 2000 (Woody density of Australian plants, CSIRO): Banksia serrata basic density about 0.55-0.60, air-dry about 700 kg/m3; hardness and strength from the genus' range; " + GreenWater + "; " + FromMemory);

        public static readonly Wood CoastBanksia = new Wood(PlantSpecies.CoastBanksia, 640.0, 0.90, 3.8, 60.0,
            "A light, soft banksia wood; the hearth board of this coast.",
            "Ilic et al. 2000: Banksia integrifolia, air-dry about 640 kg/m3; hardness and strength from the genus' range; " + GreenWater + "; " + FromMemory);

        public static readonly Wood SwampPaperbark = new Wood(PlantSpecies.SwampPaperbark, 700.0, 0.80, 5.4, 80.0,
            "A medium-density wood under sheets of papery bark; the bark is tinder, the wood a hearth.",
            "Bootle 2005: paperbark, Melaleuca quinquenervia, air-dry density about 700 kg/m3, Janka about 5.4 kN; " + GreenWater + "; " + FromMemory);

        /// <summary>The grass tree's flower stalk, not its trunk: the fire drill of the New South Wales coast in the ethnographic record.</summary>
        public static readonly Wood GrassTree = new Wood(PlantSpecies.GrassTree, 300.0, 0.50, 1.0, 15.0,
            "The flower stalk of the grass tree: light, straight and pithy, the drill the coast's people spun.",
            "the scape of Xanthorrhoea as the fire drill of the NSW coast (the Australian Museum's account of Aboriginal fire-making); its density, hardness and strength are ESTIMATES of a light pithy stalk, not measured figures: the one estimated row in this table");

        public static readonly IReadOnlyList<Wood> All = new[] { Blackbutt, Bangalay, OldManBanksia, CoastBanksia, SwampPaperbark, GrassTree };

        private static readonly Dictionary<PlantSpecies, Wood> BySpecies = Build();

        private static Dictionary<PlantSpecies, Wood> Build()
        {
            Dictionary<PlantSpecies, Wood> map = new Dictionary<PlantSpecies, Wood>();
            foreach (Wood w in All) map[w.Species] = w;
            return map;
        }

        /// <summary>The wood of a plant, or null for a plant that has none (a fern, a sedge, a grass) and for no plant.</summary>
        public static Wood Of(PlantSpecies species) => species != null && BySpecies.TryGetValue(species, out Wood w) ? w : null;
    }
}
