using System;

namespace EarthGame.Engine
{
    /// <summary>What the founder is doing, as the body prices it (v1's Exertion): at rest, walking, or running.</summary>
    public enum Exertion : byte
    {
        Resting = 0,
        Walking = 1,
        Running = 2,
    }

    /// <summary>How cold the founder is, in the terms they would use (v1's BodyCondition, less its Dead).</summary>
    public enum ColdLevel : byte
    {
        Well = 0,
        Chilly = 1,
        Cold = 2,
        Hypothermic = 3,
        SeverelyHypothermic = 4,
    }

    /// <summary>
    /// The air, sky and sun at a founder's body, gathered once (FP.2): the body and any forecast read the same struct,
    /// so they cannot come to disagree about what the night is made of, which is the seam v1's contract B1 closed.
    /// </summary>
    public readonly struct Surroundings
    {
        /// <summary>The air's temperature at the body, °C.</summary>
        public readonly double AirC;
        /// <summary>The wind at the skin, m/s: the world's wind where the founder stands, nothing built having touched it yet.</summary>
        public readonly double WindAtBodyMs;
        /// <summary>How much of the sky is covered, 0 clear to 1 overcast: it closes the sky's cold and kills the sun's beam.</summary>
        public readonly double CloudCover01;
        /// <summary>The air's relative humidity, 0 to 1, for the breath's latent half.</summary>
        public readonly double RelativeHumidity01;
        /// <summary>The sun's elevation, degrees above the horizon; at or below it, no sun.</summary>
        public readonly double SolarElevationDeg;
        /// <summary>How much of the sky the body sees, 0 to 1: half for a body standing in the open (v1's reading).</summary>
        public readonly double SkyView01;

        public Surroundings(double airC, double windAtBodyMs, double cloudCover01, double relativeHumidity01, double solarElevationDeg, double skyView01)
        {
            AirC = airC;
            WindAtBodyMs = Math.Max(0.0, windAtBodyMs);
            CloudCover01 = SimMath.Clamp01(cloudCover01);
            RelativeHumidity01 = SimMath.Clamp01(relativeHumidity01);
            SolarElevationDeg = solarElevationDeg;
            SkyView01 = SimMath.Clamp01(skyView01);
        }

        /// <summary>The surroundings a reading of the sky gives a body standing in the open under the sun's elevation.</summary>
        public static Surroundings Of(in Weather weather, double solarElevationDeg) =>
            new Surroundings(weather.AirC, weather.WindMs, weather.CloudCover01, weather.RelativeHumidity01, solarElevationDeg, Warmth.StandingSkyView01);
    }

    /// <summary>
    /// The warmth of a founder's body (FP.2, the Founder's Path's second beat): its core temperature and the heat balance
    /// that moves it, ported term by term from v1's <c>BodyState</c> (Assets/EarthGame/Sim/Body/BodyState.cs), whose
    /// numbers are published human physiology restated in code and whose loss terms were measured against their
    /// referents (v1's CALIBRATION_B1_DAY_ONE.md). Heat in: the basal rate, the work of walking or running, shivering
    /// while the stamina for it lasts, and the sun on the body's projected area. Heat out: conduction and convection
    /// through the tissue's insulation and the still-air layer the wind thins, the clear sky's cold above the air,
    /// breathing. Sweat sheds a surplus and costs water in the thirst's account. Shelter, fire, clothing, bedding, the
    /// ground and sleep are later beats' terms and add to this balance without rewriting it: the founder here is naked,
    /// standing or walking in the open, awake.
    /// </summary>
    public sealed class Warmth
    {
        // ---- constants, all from published human physiology, as v1 restated them ----

        /// <summary>Normal core temperature, °C.</summary>
        public const double NormalCoreC = 37.0;
        /// <summary>Below this is hypothermia by the clinical definition.</summary>
        public const double HypothermiaC = 35.0;
        /// <summary>Below this, consciousness and coordination fail.</summary>
        public const double SevereHypothermiaC = 32.0;
        /// <summary>Cardiac arrest becomes near-certain around here: death.</summary>
        public const double LethalCoreC = 28.0;
        /// <summary>The words' upper thresholds below normal: chilly under 36.7, cold under 36.</summary>
        public const double ChillyC = 36.7, ColdC = 36.0;
        /// <summary>Body surface area of an average adult, m².</summary>
        public const double SkinAreaM2 = 1.8;
        /// <summary>Mass, kg.</summary>
        public const double MassKg = 70.0;
        /// <summary>Specific heat of human tissue, J/(kg·K).</summary>
        public const double SpecificHeat = 3500.0;
        /// <summary>Basal metabolic heat, W: about 1,700 kcal a day.</summary>
        public const double BasalHeatW = 80.0;
        /// <summary>Extra heat from walking and from running, W.</summary>
        public const double WalkingHeatW = 180.0, RunningHeatW = 420.0;
        /// <summary>Most vigorous shivering roughly quintuples basal heat production.</summary>
        public const double MaxShiverW = 350.0;
        /// <summary>Hours of maximal shivering before it is exhausted; spent fast or slow, the integral is this.</summary>
        public const double ShiverEnduranceHours = 3.0;
        /// <summary>Still-air boundary layer on bare skin, in clo.</summary>
        public const double BareSkinClo = 0.7;
        /// <summary>
        /// Insulation of the body's own tissue, in clo: slight when warm and well perfused, roughly tripled by
        /// vasoconstriction in the cold. v1 found that leaving this out made a naked body at 2 °C shed 970 W and reach
        /// hypothermia in eight minutes, about ten times too fast.
        /// </summary>
        public const double TissueCloWarm = 0.30, TissueCloConstricted = 0.90;
        /// <summary>One clo in SI units, m²·K/W.</summary>
        public const double CloToSI = 0.155;
        /// <summary>Stefan-Boltzmann constant, W/(m²·K⁴).</summary>
        public const double StefanBoltzmann = 5.670374419e-8;
        /// <summary>Emissivity of skin in the infrared; skin is very nearly a black body.</summary>
        public const double SkinEmissivity = 0.98;
        /// <summary>The share of the skin that radiates outward rather than at other parts of the body.</summary>
        public const double RadiatingAreaFraction = 0.70;
        /// <summary>How much of the sky a body standing in the open sees, 0 to 1.</summary>
        public const double StandingSkyView01 = 0.5;
        /// <summary>How much of the sun a naked body absorbs: skin reflects about a third, and parts shade parts.</summary>
        public const double SolarAbsorptance = 0.55;
        /// <summary>Most water an acclimatised adult can sweat in an hour, litres: about a kilowatt of cooling.</summary>
        public const double MaxSweatLPerHour = 1.5;
        /// <summary>Heat carried off by evaporating a litre of sweat at skin temperature, J.</summary>
        public const double LatentHeatOfSweatJPerL = 2430000.0;
        /// <summary>A body at rest moves slower than this, m/s; a running one faster than <see cref="RunningFromMs"/> (the mover's gaits, M1.5c).</summary>
        public const double RestingBelowMs = 0.2, RunningFromMs = 2.8;

        /// <summary>Core temperature, °C.</summary>
        public double CoreC { get; private set; } = NormalCoreC;

        /// <summary>Whether the body is shivering, and how hard, 0 to 1: involuntary, decided here.</summary>
        public double Shivering01 { get; private set; }

        /// <summary>What is left of the muscles' shivering, 1 fresh to 0 spent; recovers when warm, four times slower than it is spent.</summary>
        public double ShiverStamina01 { get; private set; } = 1.0;

        /// <summary>Insulation worn, in clo. Naked is zero, and that is where the founder starts.</summary>
        public double ClothingClo { get; set; }

        /// <summary>The last tick's terms, W, for the screen, the log and the forecast to come: negative net means cooling.</summary>
        public double NetHeatW { get; private set; }
        public double ProductionW { get; private set; } = BasalHeatW;
        public double SensibleLossW { get; private set; }
        public double SkyLossW { get; private set; }
        public double RespiratoryLossW { get; private set; }

        /// <summary>The water leaving in the breath in the last tick, litres an hour (2026-09-16): the water's account charges what of it exceeds the resting breath.</summary>
        public double BreathWaterLPerHour { get; private set; }
        public double SolarGainW { get; private set; }
        public double EvaporativeW { get; private set; }

        /// <summary>How hard the founder sweated in the last tick, litres per hour: the thirst's account is charged this.</summary>
        public double SweatRateLPerHour { get; private set; }

        /// <summary>The word for it now.</summary>
        public ColdLevel Cold => LevelOf(CoreC);

        /// <summary>Whether the core is above the lethal temperature. The server acts on it (FP.2): death.</summary>
        public bool IsAlive => CoreC > LethalCoreC;

        /// <summary>Hours until hypothermia at the last tick's net loss; infinite while the body is not cooling; none once it is there.</summary>
        public double HoursToHypothermia
        {
            get
            {
                if (NetHeatW >= 0.0) return double.PositiveInfinity;
                double margin = CoreC - HypothermiaC;
                if (margin <= 0.0) return 0.0;
                return margin / (-NetHeatW / (MassKg * SpecificHeat)) / 3600.0;
            }
        }

        /// <summary>The word for a body with this core, the one owner of the thresholds for every reader of the wire.</summary>
        public static ColdLevel LevelOf(double coreC)
        {
            if (coreC < SevereHypothermiaC) return ColdLevel.SeverelyHypothermic;
            if (coreC < HypothermiaC) return ColdLevel.Hypothermic;
            if (coreC < ColdC) return ColdLevel.Cold;
            if (coreC < ChillyC) return ColdLevel.Chilly;
            return ColdLevel.Well;
        }

        /// <summary>The word the founder would use, and the screen shows under the clock; nothing while well.</summary>
        public static string WordFor(ColdLevel level)
        {
            switch (level)
            {
                case ColdLevel.Chilly: return "chilly";
                case ColdLevel.Cold: return "cold";
                case ColdLevel.Hypothermic: return "hypothermic";
                case ColdLevel.SeverelyHypothermic: return "severely hypothermic";
                default: return string.Empty;
            }
        }

        /// <summary>What a founder moving at this speed is doing, by the mover's gaits.</summary>
        public static Exertion ExertionOf(double horizontalSpeedMs)
        {
            if (!(horizontalSpeedMs >= RestingBelowMs)) return Exertion.Resting;
            return horizontalSpeedMs >= RunningFromMs ? Exertion.Running : Exertion.Walking;
        }

        /// <summary>The work of moving, W above basal.</summary>
        public static double ActivityHeatW(Exertion exertion) =>
            exertion == Exertion.Running ? RunningHeatW : exertion == Exertion.Walking ? WalkingHeatW : 0.0;


        /// <summary>
        /// Total insulation between core and air, in clo: what is worn, plus the still-air layer on the skin, which
        /// wind strips away. Losing that layer is why a breeze at 5 °C is more dangerous than still air at 0 °C.
        /// The wind is the wind at the skin; nothing here reduces it twice (v1's R1).
        /// </summary>
        public double TotalInsulationClo(double windAtBodyMs)
        {
            double wind = Math.Max(0.0, windAtBodyMs);
            // The boundary layer thins with the square root of wind speed, and never quite vanishes.
            double boundary = Math.Max(0.12, BareSkinClo / (1.0 + 0.55 * Math.Sqrt(wind)));
            // Clothing loses some of its value in wind too, unless it is windproof.
            double clothing = ClothingClo / (1.0 + 0.08 * wind);
            return clothing + boundary;
        }

        /// <summary>
        /// Heat leaving by conduction and convection, W: everything the clo boundary layer carries. Vasoconstriction is
        /// the body's first defence, shutting blood away from the skin as the core's deficit grows, trading a cold
        /// surface for a warm core. One function for the body and any forecast, so the two cannot disagree.
        /// </summary>
        public double SensibleLossAt(in Surroundings s, double coreDeficitC)
        {
            double constriction = SimMath.Clamp01((s.AirC < 28.0 ? 1.0 : 0.0) * (coreDeficitC + 0.6));
            double tissue = TissueCloWarm + (TissueCloConstricted - TissueCloWarm) * constriction;
            double insulation = (tissue + TotalInsulationClo(s.WindAtBodyMs)) * CloToSI;
            double gradient = CoreC - s.AirC;
            return SkinAreaM2 * gradient / Math.Max(0.02, insulation);
        }

        /// <summary>
        /// Extra radiative loss because the sky is colder than the air, W: a clear night sky radiates as if it were
        /// <see cref="Climate.ClearSkyDepressionK"/> below the air, cloud closes that gap, and a body facing it loses the
        /// difference on top of what the boundary layer already carries. About 39 W for someone standing in the open
        /// at freezing under a clear sky, half their basal rate; nothing under a roof. That is why shelters have roofs.
        /// </summary>
        public double SkyExcessLossAt(in Surroundings s)
        {
            if (s.SkyView01 <= 0.0) return 0.0;
            double skyC = s.AirC - Climate.ClearSkyDepressionK * (1.0 - s.CloudCover01);
            double air = s.AirC + 273.15, sky = skyC + 273.15;
            if (sky >= air) return 0.0;
            double area = SkinAreaM2 * RadiatingAreaFraction * s.SkyView01;
            return SkinEmissivity * StefanBoltzmann * area * (air * air * air * air - sky * sky * sky * sky);
        }

        /// <summary>
        /// Heat gained from the sun, W: the direct beam on the body's projected area (a quarter of a standing body to a
        /// sun on the horizon, a twelfth to one overhead) and the diffuse sky on what faces upward, less what the skin
        /// reflects. The largest term v1's model was missing, and the one that decides whether a naked founder gets
        /// through a winter day.
        /// </summary>
        public double SolarGainAt(in Surroundings s)
        {
            if (s.SolarElevationDeg <= 0.0) return 0.0;
            double elevation = Math.Min(90.0, s.SolarElevationDeg);
            double projected = 0.25 - 0.17 * (elevation / 90.0);
            double direct = Climate.DirectSolarWm2(elevation, s.CloudCover01) * projected;
            double diffuse = Climate.DiffuseSolarWm2(elevation, s.CloudCover01) * RadiatingAreaFraction * 0.5;
            return (direct + diffuse) * SkinAreaM2 * SolarAbsorptance;
        }

        /// <summary>
        /// Heat carried out by breathing, W: warming the air on the way in (sensible) and saturating it (latent,
        /// <see cref="RespiratoryLatentAt"/>), Fanger's form as ISO 7933 uses it, a straight share of what the founder is
        /// producing, so it costs more the harder they work.
        /// </summary>
        public double RespiratoryAt(double metabolicW, in Surroundings s)
        {
            double m = Math.Max(0.0, metabolicW);
            double sensible = 0.0014 * m * (34.0 - s.AirC);
            return Math.Max(0.0, sensible + RespiratoryLatentAt(metabolicW, s));
        }

        /// <summary>
        /// The latent part of the breath's loss, W: the water that leaves as vapour, more in dry air and the harder the
        /// founder works. It is the water's account's breath (<see cref="BreathWaterLPerHour"/>): one owner for the heat
        /// it carries and the litres it costs (2026-09-16).
        /// </summary>
        public double RespiratoryLatentAt(double metabolicW, in Surroundings s)
        {
            double m = Math.Max(0.0, metabolicW);
            double vapour = s.RelativeHumidity01 * Climate.SaturationVapourKPa(s.AirC);
            return Math.Max(0.0, 0.0173 * m * (5.87 - vapour));
        }

        /// <summary>
        /// The body's seconds going by in these surroundings, doing this. <paramref name="workCapacity01"/> is what thirst
        /// leaves of the muscles (shivering is muscular work); <paramref name="sweatCapable01"/> what it leaves of the
        /// sweat (dehydration blunts it long before it is dangerous on its own). A dead body is left as it is.
        /// </summary>
        public void Tick(double seconds, in Surroundings s, Exertion exertion, double workCapacity01, double sweatCapable01)
        {
            if (!(seconds > 0.0) || !IsAlive) return;
            double capacity = SimMath.Clamp01(workCapacity01), sweatCapable = SimMath.Clamp01(sweatCapable01);

            // Shivering ramps in as the core falls and is maximal a degree down, limited by what the muscles have left.
            double deficit = NormalCoreC - CoreC;
            double demand = SimMath.Clamp01((deficit - 0.15) / 1.2);
            Shivering01 = demand * ShiverStamina01;
            if (demand > 0.01)
                // Billed for the shivering delivered, not demanded: the integral of delivered shivering over the reserve's
                // life is exactly the endurance, at any intensity (v1's ruling of 2026-09-01).
                ShiverStamina01 = Math.Max(0.0, ShiverStamina01 - Shivering01 * seconds / (ShiverEnduranceHours * 3600.0));
            else
                // Recovers when warm, four times slower than it is spent; gated on demand, so a spent body that still wants
                // to shiver is not recovering while it freezes.
                ShiverStamina01 = Math.Min(1.0, ShiverStamina01 + seconds / (ShiverEnduranceHours * 3600.0 * 4.0));

            ProductionW = BasalHeatW + ActivityHeatW(exertion) + Shivering01 * MaxShiverW * capacity;
            SensibleLossW = SensibleLossAt(s, deficit);
            SolarGainW = SolarGainAt(s);

            // Sweat sheds a surplus, driven by where the core is heading rather than where it is, never below the
            // setpoint within one step, and blunted by thirst. The water it costs is the thirst's account's.
            double heatCapacity = MassKg * SpecificHeat;
            double surplus = ProductionW + SolarGainW - SensibleLossW;
            EvaporativeW = 0.0;
            if (surplus > 0.0)
            {
                double projected = CoreC + surplus * seconds / heatCapacity;
                double drive = SimMath.Clamp01((projected - NormalCoreC) / 0.4);
                double most = MaxSweatLPerHour * LatentHeatOfSweatJPerL / 3600.0;
                double headroom = surplus + Math.Max(0.0, CoreC - NormalCoreC) * heatCapacity / seconds;
                EvaporativeW = Math.Min(drive * most * sweatCapable, headroom);
            }
            SweatRateLPerHour = EvaporativeW * 3600.0 / LatentHeatOfSweatJPerL;

            SkyLossW = SkyExcessLossAt(s);
            RespiratoryLossW = RespiratoryAt(ProductionW, s);
            // The breath's water: the latent heat it carries, in litres by the heat of vaporisation. About a quarter of a
            // litre a day at rest, near a litre walking, a litre and a half running in cold dry air.
            BreathWaterLPerHour = RespiratoryLatentAt(ProductionW, s) * 3600.0 / LatentHeatOfSweatJPerL;
            NetHeatW = surplus - EvaporativeW - SkyLossW - RespiratoryLossW;
            CoreC += NetHeatW * seconds / heatCapacity;
            // A core this high means sweating has already failed; heat illness is not modelled beyond the water it costs.
            if (CoreC > 41.0) CoreC = 41.0;
        }

        /// <summary>A body put back as a save or a developer's setting states it; anything outside the possible is clamped, NaN is normal.</summary>
        public void Restore(double coreC)
        {
            CoreC = double.IsNaN(coreC) ? NormalCoreC : Math.Min(41.0, Math.Max(20.0, coreC));
        }

        /// <summary>A new founder's body: normal, fresh.</summary>
        public void Reset()
        {
            CoreC = NormalCoreC;
            Shivering01 = 0.0;
            ShiverStamina01 = 1.0;
            NetHeatW = 0.0;
            ProductionW = BasalHeatW;
            SensibleLossW = SkyLossW = RespiratoryLossW = SolarGainW = EvaporativeW = 0.0;
            SweatRateLPerHour = 0.0;
            BreathWaterLPerHour = 0.0;
        }
    }
}
