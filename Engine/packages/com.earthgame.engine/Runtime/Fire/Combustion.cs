using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// How wood burns, as the fire-science literature measures it (BF.5, 2026-09-25): the heat a kilogram gives and what its
    /// water costs, the char it leaves, how fast a piece burns by its thickness and its water, how long a piece takes to catch
    /// under a given heat, and how much gas a surface must give off to hold a flame. The one owner of these numbers:
    /// <see cref="Fire"/> steps pieces by them, <see cref="FrictionFire"/> and <see cref="Tinder"/> read the water's and the
    /// wood's heat from here, and <see cref="FireFuel"/> prices a night by them.
    ///
    /// <para>Every constant names its source. Where a number is this model's own choice rather than a measurement it says
    /// "an estimate" and what it was placed against.</para>
    /// </summary>
    public static class Combustion
    {
        // ---- water ----

        /// <summary>
        /// The enthalpy of vaporisation of water at 25 °C, J/kg: 2.443 MJ/kg, the constant the solid-biofuel standards charge a
        /// fuel's water at when they turn a gross calorific value into a net one, "24,43 J/g per weight percent moisture" (ISO
        /// 16993:2016, 5.2.3, formula 4; ISO 18125:2017 sets the reference state at 25 °C). The CODATA enthalpies of formation give
        /// 2.4426 MJ/kg for liquid to vapour at 25 °C, and IAPWS-95's saturated table 2.4417 (NIST Chemistry WebBook).
        /// </summary>
        public const double WaterLatentJPerKg = 2.443e6;

        /// <summary>
        /// The enthalpy of vaporisation of water at 100 °C, J/kg: 2256.4 kJ/kg, saturated vapour less saturated liquid in the NIST
        /// Chemistry WebBook's IAPWS-95 table (Wagner and Pruss 2002: 2675.57 − 419.17 kJ/kg).
        /// </summary>
        public const double WaterBoilingLatentJPerKg = 2.2564e6;

        /// <summary>
        /// The heat that drives a kilogram of water out of wood being heated from the air, J/kg: its sensible heat from 20 °C to
        /// boiling at 4.186 kJ/(kg·K) and <see cref="WaterBoilingLatentJPerKg"/>. About 2.59 MJ/kg: what a damp piece must be
        /// given before it can heat past 100 °C.
        /// </summary>
        public const double WaterDriveOffJPerKg = 4186.0 * 80.0 + WaterBoilingLatentJPerKg;

        // ---- the heat of the wood ----

        /// <summary>
        /// Hydrogen in dry wood, per cent by mass: 6. "Dry wood has an elemental composition of about 50% carbon, 6% hydrogen, 44%
        /// oxygen" (Rowell, Pettersen and Tshabalala 2013, "Cell wall chemistry", Handbook of Wood Chemistry and Wood Composites,
        /// 2nd ed., CRC; eucalyptus wood in the Phyllis2 database, 5.75 per cent). The water this hydrogen makes when it burns
        /// leaves as vapour from an open fire and takes its latent heat with it.
        /// </summary>
        public const double HydrogenPercentDry = 6.0;

        /// <summary>Oxygen and nitrogen in dry wood, per cent by mass: 44, the oxygen of the same composition, nitrogen a trace (0.14 in Phyllis2's eucalyptus).</summary>
        public const double OxygenNitrogenPercentDry = 44.0;

        /// <summary>The gross heat of dry wood, MJ/kg: the table's (<see cref="Wood.HeatMJPerKgDry"/>), or wood's one figure for fibre of no row.</summary>
        public static double GrossHeatMJPerKgDry(Wood wood) => wood != null ? wood.HeatMJPerKgDry : Wood.HeatOfWoodMJPerKgDry;

        /// <summary>
        /// The net heat of dry wood, MJ/kg: the gross less the latent heat of the water its own hydrogen makes, by the standards'
        /// q_p,net,d = q_V,gr,d − 212.2 w(H) − 0.8 [w(O) + w(N)], in J/g with w in per cent (ISO 1928:2009's equation, the form ISO
        /// 18125 takes for solid biofuels; the 212.2 carries the change from constant volume to constant pressure, Phyllis2's
        /// simpler 2.443 × 8.936 H giving 218.3 and a net value 0.04 MJ/kg lower). About 17.7 MJ/kg from the table's 19.0; eucalyptus
        /// wood measures 19.22 gross and 17.96 net (Phyllis2, from NREL 1998), and Wong et al.'s eucalypt fuels heat yield,
        /// GCV − 24.42 (9 H + FMC), is the same arithmetic (2025, International Journal of Wildland Fire 34, WF24227).
        /// </summary>
        public static double NetHeatMJPerKgDry(Wood wood) =>
            GrossHeatMJPerKgDry(wood) - (212.2 * HydrogenPercentDry + 0.8 * OxygenNitrogenPercentDry) / 1000.0;

        /// <summary>
        /// The heat a kilogram of dry wood gives when it burns at <paramref name="moisture"/> (water as a share of the dry
        /// mass), MJ: the net heat less the latent heat of its water, ISO 16993's net value as received put per kilogram of dry
        /// wood (its q_net,ar = q_net,d (100 − M)/100 − 24.43 M, for M per cent of the wet mass, is this times the dry share).
        /// </summary>
        public static double HeatMJPerKgDry(Wood wood, double moisture) => NetHeatMJPerKgDry(wood) - WaterLatentJPerKg / 1e6 * Math.Max(0.0, moisture);

        /// <summary>The same heat per kilogram of the wood as it is, water and all, MJ/kg: what a person's armful gives.</summary>
        public static double HeatMJPerKgAsItIs(Wood wood, double moisture) => HeatMJPerKgDry(wood, moisture) / (1.0 + Math.Max(0.0, moisture));

        // ---- char and gas ----

        /// <summary>
        /// The share of dry wood the flame leaves as char, 0.2: "for most wood samples, the residual char is about 20% of the
        /// original mass" (Tran 1992, "Experimental data on wood materials", in Babrauskas and Grayson, eds., <i>Heat Release in
        /// Fires</i>, ch. 11b); 0.16 to 0.23 in NIST's microscale calorimetry of woods (Leventon, De Lannoye and Greene 2025,
        /// Interflam, NIST publication 960103). The char is the fire's embers.
        /// </summary>
        public const double CharYield = 0.20;

        /// <summary>
        /// The space a stick's char takes, as a share of the wood it came from: a half, an estimate. Char keeps a stick's shape
        /// as it forms and cracks and shrinks as it goes, so a charred stick is thinner than the stick was; with a fifth of the
        /// mass in half the space, its char is two-fifths the wood's density (Bartlett, Hadden and Bisby 2019 give a char layer's
        /// density as about a fifth of the wood's, before it shrinks). It gives a glowing stick the surface its embers burn on.
        /// </summary>
        public const double CharVolumeShare = 0.5;

        /// <summary>
        /// The heat of combustion of wood char, MJ/kg: 32.6, Douglas fir's char in NIST's microscale calorimetry (32.6 ± 4.1 kJ/g;
        /// western red cedar's 32.8; pure carbon's 32.8 the ceiling: Leventon, De Lannoye and Greene 2025, table 2).
        /// </summary>
        public const double CharHeatMJPerKg = 32.6;

        /// <summary>
        /// The net heat of the gases, MJ per kilogram of gas: whatever of the wood's net heat its char does not carry, so a piece
        /// burnt to ash has given exactly the wood's net heat. About 14.0 MJ/kg for the table's wood; NIST measured Douglas fir's
        /// gas at 12.8 ± 0.9 kJ/g and western red cedar's at 13.9 (Leventon et al. 2025).
        /// </summary>
        public static double GasHeatMJPerKg(Wood wood) => (NetHeatMJPerKgDry(wood) - CharYield * CharHeatMJPerKg) / (1.0 - CharYield);

        /// <summary>
        /// How much of the gas an open wood fire's flame burns, 0.95: the rest leaves as smoke, soot and carbon monoxide. An
        /// estimate, placed so the heat a kilogram of wood lost in flame gives, 13.3 MJ, sits in the cone calorimeter's effective
        /// heat of combustion for wood, 13.0 to 14.7 MJ/kg on a dry basis at 20 to 50 kW/m² (Tran 1992: 0.057 q + 11.88) and the
        /// Wood Handbook's "about 65%" of the bomb's 20 MJ/kg (White and Dietenberger 2010, ch. 18).
        /// </summary>
        public const double FlameEfficiency = 0.95;

        // ---- how fast a piece burns ----

        /// <summary>
        /// How fast a burning stick's surface gives off its wood in a going fire, kg/(m²·s), by its thickness when laid: the
        /// burning rate of wood cribs as their sticks allow, R / A_s = C b^−0.5 with C = 1.08 × 10⁻³ g/(s·cm^1.5), b the stick's
        /// thickness in centimetres (McAllister and Finney 2015, "Burning rates of wood cribs with implications for wildland
        /// fires", Fire Technology, Heskestad's form of Block's law; robust from 3.2 mm up). A thicker stick burns slower on each
        /// square metre, a denser one slower inward. The second source, Babrauskas's crib rate as the surface's speed,
        /// v_p = 1.7 × 10⁻⁶ D^−0.6 m/s (1979, COMPF2, NBS Technical Note 991, equation 17, from Nilsson's and Yamashika's cribs),
        /// agrees within a fifth for woods of 450 kg/m³ from 4 mm to 10 cm.
        /// </summary>
        public static double CribBurnKgM2s(double thicknessM) => 1.08e-3 / Math.Sqrt(Math.Max(1e-4, thicknessM));

        /// <summary>How fast the burning surface of a stick of this density moves inward in a going fire, m/s: the crib rate over the density.</summary>
        public static double CribRegressionMs(double thicknessM, double densityKgM3) => CribBurnKgM2s(thicknessM) / Math.Max(1.0, densityKgM3);

        /// <summary>
        /// The water in the air-dry wood the burning rates are measured on, as a share of dry mass: 0.08. Laboratory wood
        /// conditioned at 24 °C and 55 per cent humidity comes to 8 per cent (McAllister, Finney and Cohen 2011), and the charring
        /// rate's moisture in <see cref="MoistureBurnFactor"/> is reckoned from 8 per cent. The moisture at which that factor is one.
        /// </summary>
        public const double CribMoisture = 0.08;

        /// <summary>
        /// The heat a burning surface spends on each kilogram of its dry wood, J/kg: 3.2 MJ/kg, fitted to the one published point
        /// found for moisture's effect on a burning rate: wood chars 8.3 per cent slower at 20 per cent moisture than at 8
        /// (Babrauskas 2005, as Bartlett, Hadden and Bisby 2019 report it, Fire Technology 55:1–49); no heat of gasification
        /// for wood could be read at its source for this.
        /// </summary>
        public const double DryBurnHeatJPerKg = 3.2e6;

        /// <summary>
        /// How a stick's water slows its burning against air-dry wood: the heat reaching its burning surface must drive its water
        /// off as well as burn its wood, so the rate goes as L / (L + u × 2.59 MJ/kg), L <see cref="DryBurnHeatJPerKg"/>, against
        /// the same at <see cref="CribMoisture"/>. Green wood at 0.6 burns at about 0.7 of an air-dry stick's pace.
        /// </summary>
        public static double MoistureBurnFactor(double moisture)
        {
            double u = Math.Max(0.0, moisture);
            return (DryBurnHeatJPerKg + CribMoisture * WaterDriveOffJPerKg) / (DryBurnHeatJPerKg + u * WaterDriveOffJPerKg);
        }

        /// <summary>
        /// How a wind speeds a flaming stick's burning, as a factor: the root of one plus the wind over 1 m/s, the form of forced
        /// convection's heat to a cylinder (Hilpert's Nu ∝ Re^0.5). 1.3 at 0.7 m/s, inside the 6 to 62 per cent McAllister and
        /// Finney measured for cribs of half-inch sticks at that wind (2016, Fire Technology 52:1035–1050; up to 70 per cent in
        /// McAllister 2019, Frontiers in Mechanical Engineering 5:11), where cribs of quarter-inch sticks burnt slower instead: an
        /// estimate beyond their 0.7 m/s.
        /// </summary>
        public static double FlameWindFactor(double windMs) => Math.Sqrt(1.0 + Math.Max(0.0, windMs) / 1.0);

        // ---- catching ----

        /// <summary>
        /// The least heat that brings wood to a flame at all, W/m²: 11 kW/m², the critical flux of Babrauskas's fit to piloted
        /// ignition of woods (2001, "Ignition of wood: a review of the state of the art", Interflam 2001, 71–88; 9.0 to 12.2 by
        /// orientation and conditioning); the Wood Handbook gives 10 to 13 and Bartlett et al. 12 ± 2. Below it a surface loses
        /// what it is given as fast as it gets it and never reaches the ignition temperature; the same figure is the loss a
        /// burning surface fights, and a piece whose heat falls to it goes out.
        /// </summary>
        public const double CriticalFluxWm2 = 11000.0;

        /// <summary>
        /// The piloted ignition temperature of wood's surface, °C: 350. "Piloted ignition … normally occurs at surface temperatures
        /// of 300–365 ºC" (Babrauskas 2001; 300 to 310 for hardwoods and 350 to 365 for softwoods in his table 5); the Wood
        /// Handbook gives 300 to 400.
        /// </summary>
        public const double IgnitionC = 350.0;

        /// <summary>The air a piece starts from, °C.</summary>
        public const double AmbientC = 20.0;

        /// <summary>
        /// The heat to take a kilogram of dry wood from the air's temperature to its ignition temperature, J/kg: the integral of
        /// the Wood Handbook's specific heat of dry wood, c = 0.1031 + 0.003867 T kJ/(kg·K) with T in kelvin (Glass and Zelinka
        /// 2010, Wood Handbook, FPL-GTR-190, ch. 4, eq. 4-16a), from 20 °C to 350 °C: about 0.62 MJ/kg.
        /// </summary>
        public static readonly double HeatToIgniteJPerKg = HeatToRaiseJPerKg(AmbientC, IgnitionC);

        /// <summary>The Wood Handbook's dry wood heated from one temperature to another, J/kg.</summary>
        public static double HeatToRaiseJPerKg(double fromC, double toC)
        {
            double a = fromC + 273.15, b = toC + 273.15;
            return 1000.0 * (0.1031 * (b - a) + 0.003867 / 2.0 * (b * b - a * a));
        }

        /// <summary>
        /// How long a piece takes to catch under a heat of <paramref name="fluxWm2"/>, s, by whichever of the two limits comes first;
        /// infinite at or below the critical flux.
        /// <list type="bullet">
        /// <item>Thin (the whole piece heats through, as a twig or a fibre does): its dry wood raised to the ignition temperature
        /// and its water driven off, per square metre of surface a cylinder's d/4 of it, by the heat it gains over what the
        /// critical flux says it loses: t = ρ (d/4) (h_ig + u h_w) / (q − q_cr), the thermally thin form
        /// t = ρ c l ΔT / q (Drysdale; as Bartlett et al. 2019, eq. 2, give it) with the loss and the water added.</item>
        /// <item>Thick (only its surface need reach ignition): Babrauskas's fit for wood, t = 130 ρ^0.73 / (q − 11)^1.82 with
        /// q in kW/m² and ρ in kg/m³ (Babrauskas 2001; densities 170 to 850, specimens 12 to 25 mm, a root-mean-square error of
        /// 64 per cent), slowed by its water as McAllister, Finney and Cohen measured it for poplar at 20 to 50 kW/m²: 1.47 times
        /// as long at 18.5 per cent as dry, fitted as 1 + 2.5u (2011, "Critical mass flux for flaming ignition of wood as a
        /// function of external radiant heat flux and moisture content", 7th US National Technical Meeting of the Combustion
        /// Institute, table 1). Others find water slows it more: Mikkola's (1 + 4w)² (1992) and Moghtaderi et al.'s radiata pine,
        /// three times as long at 30 per cent (1997), both as Bartlett et al. report them.</item>
        /// </list>
        /// </summary>
        public static double IgnitionSeconds(double densityKgM3, double thicknessM, double moisture, double fluxWm2)
        {
            if (!(fluxWm2 > CriticalFluxWm2)) return double.PositiveInfinity;
            double u = Math.Max(0.0, moisture);
            double thin = densityKgM3 * thicknessM / 4.0 * (HeatToIgniteJPerKg + u * WaterDriveOffJPerKg) / (fluxWm2 - CriticalFluxWm2);
            double qKw = fluxWm2 / 1000.0;
            double thick = 130.0 * Math.Pow(densityKgM3, 0.73) / Math.Pow(qKw - CriticalFluxWm2 / 1000.0, 1.82) * (1.0 + 2.5 * u);
            return Math.Min(thin, thick);
        }

        // ---- holding a flame ----

        /// <summary>
        /// The least gas a burning surface must give off to hold a flame, kg/(m²·s): the critical mass flux. McAllister, Finney
        /// and Cohen measured it for poplar in a 1 m/s air stream (2011, table 1): 1.3 to 1.9 g/(m²·s) dry across 20 to 50 kW/m²,
        /// a third more at 8 per cent moisture and 56 per cent more at 18.5, because the water coming off with the gas dilutes and
        /// cools the flame. Here 1.6 g/(m²·s) dry, rising as 1 + 3u (through their 18.5 per cent point; carried on past it to
        /// green wood as an estimate, their own trend, so green wood smoulders on a small fire and flames only in a hot one), and
        /// with the wind as its root past their 1 m/s (an estimate: a stronger stream strips a weak flame; the critical flux of
        /// solids rises with the air's speed in the tunnels their paper cites, Rasbash et al. 1986 and Rich et al. 2007).
        /// </summary>
        public static double CriticalMassFluxKgM2s(double moisture, double windMs)
        {
            double u = Math.Max(0.0, moisture);
            double wind = Math.Sqrt(Math.Max(1.0, windMs) / 1.0);
            return 1.6e-3 * (1.0 + 3.0 * u) * wind;
        }

        // ---- a fire's heat on its own pieces ----

        /// <summary>
        /// The heat on a piece bathed in a fire's flames and coals, W/m²: 60 kW/m², the low end of what pieces inside a burning crib
        /// receive: 60 to 80 kW/m² for open cribs (Thomas 1967, as Gupta, Torero and Hidalgo 2021 give it, Combustion and Flame
        /// 228:42–56) and 71 to 84 kW/m² from a crib's burning zone (Thomas, Simms and Wraight 1965, Fire Research Note 599). A
        /// campfire is laid looser than a crib, so the low end. A piece sees it in the share of its view that is fire (<see cref="Fire"/>).
        /// </summary>
        public const double FireFluxWm2 = 60000.0;

        /// <summary>
        /// The heat a burning stick's own flame gives back to its surface, W/m², by its thickness: 25 kW/m² at 6 mm, going as the
        /// inverse root of the thickness as the heat a flow gives a cylinder does (Hilpert's Nu ∝ Re^0.5). An estimate, placed so
        /// that a lone air-dry stick thinner than a pencil keeps its own flame and a thicker one does not, which is what every
        /// fire-lighting manual teaches when it says to lay kindling close.
        /// </summary>
        public static double OwnFlameFluxWm2(double thicknessM) => 25000.0 * Math.Sqrt(0.006 / Math.Max(1e-4, thicknessM));

        /// <summary>
        /// How fast glowing char burns on its surface, kg/(m²·s): 1.5 g/(m²·s) in still air, rising with the root of one plus the
        /// air's speed over 0.5 m/s, since char's glow is fed by the oxygen reaching it and mass transfer to a body goes as the
        /// root of the Reynolds number. An estimate: still air's natural draught brings a hot surface oxygen for about 1 to 2 g of
        /// carbon a square metre a second, and it gives a 0.2 g glowing brand in a 1.5 m/s breeze the two and a half minutes of
        /// glow Ellis measured for eucalypt bark brands in wind (2015, International Journal of Wildland Fire 24:225–235).
        /// </summary>
        public static double CharGlowKgM2s(double windMs) => 1.5e-3 * Math.Sqrt(1.0 + Math.Max(0.0, windMs) / 0.5);

        // ---- a bundle of tinder ----

        /// <summary>
        /// The fibre's moisture above which a flame will not spread through a bed of fine fuel in still air, 0.236 of its dry
        /// mass: Rothermel and Anderson's burns of ponderosa pine needle beds, whose spread fell linearly with moisture to
        /// nothing at 23.6 per cent (white pine's at 22; 1966, "Fire spread characteristics determined in the laboratory", USDA
        /// Forest Service Research Paper INT-30, equations 1 to 4). Damp tinder smokes and will not flame.
        /// </summary>
        public const double BundleExtinctionMoisture = 1.04 / 0.044 / 100.0;

        /// <summary>
        /// How fast a flame crosses a bundle of fine fuel, m/s, by the fibre's water and the air's speed through it: Rothermel and
        /// Anderson's R = (1.04 − 0.044 M) e^(0.0038 U) feet a minute for M per cent and U feet a minute (INT-30, equation 6),
        /// measured to 700 feet a minute (3.6 m/s) and held there beyond, where their fires began to be cooled by the wind. Half a
        /// centimetre a second dry in still air; a breath of 2 m/s through it, four and a half times that.
        /// </summary>
        public static double BundleSpreadMs(double moisture, double windMs)
        {
            double m = Math.Max(0.0, moisture) * 100.0;
            double still = Math.Max(0.0, 1.04 - 0.044 * m);
            double uFtMin = Math.Min(Math.Max(0.0, windMs), 3.6) * 196.8504;
            return still * Math.Exp(0.0038 * uFtMin) * 0.3048 / 60.0;
        }
    }
}
