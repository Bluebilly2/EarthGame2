using System;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>How a body presents itself to a fire (BF.5). The body has no posture yet (<see cref="Warmth"/> stands); never renumbered.</summary>
    public enum Posture : byte
    {
        Standing = 0,
        Sitting = 1,
    }

    /// <summary>A fire's warmth on a body, worked out once (BF.5 part one, promise 8): what reaches the skin and what the body takes of it.</summary>
    public readonly struct FireOnBody
    {
        /// <summary>The fire's radiant power, W.</summary>
        public readonly double RadiantW;
        /// <summary>From the fire's heart to the body's, m.</summary>
        public readonly double DistanceM;
        /// <summary>The fire's angle below the body's centre, degrees.</summary>
        public readonly double BelowDeg;
        /// <summary>The radiant heat arriving at the body, W/m², across the line to the fire.</summary>
        public readonly double FluxWm2;
        /// <summary>The body's area square to the fire, m².</summary>
        public readonly double ProjectedM2;
        /// <summary>The heat the body takes, W: the term the body's balance would add (the hook proposed in the contract).</summary>
        public readonly double AbsorbedW;
        /// <summary>Whether the fire is nearer than the point source is good for: nearer than 2.5 of its widths, where it overstates (<see cref="FireWarmth.PointSourceFromWidths"/>).</summary>
        public readonly bool Near;
        public readonly string Words;

        public FireOnBody(double radiantW, double distanceM, double belowDeg, double fluxWm2, double projectedM2, double absorbedW, bool near, string words)
        {
            RadiantW = radiantW;
            DistanceM = distanceM;
            BelowDeg = belowDeg;
            FluxWm2 = fluxWm2;
            ProjectedM2 = projectedM2;
            AbsorbedW = absorbedW;
            Near = near;
            Words = words;
        }

        /// <summary>Whether the heat at the skin is past what bare skin tolerates for more than moments.</summary>
        public bool TooHot => FluxWm2 > FireWarmth.TooHotWm2;
    }

    /// <summary>
    /// A fire's warmth on a body at a distance (BF.5 part one, promise 8): the fire as a point source of radiant heat, the
    /// share of it a standing or a sitting body intercepts, and the heat the skin takes, as the term the body's heat balance
    /// would add. <see cref="Warmth"/> is not changed: the hook (a term beside its sun, <c>SolarGainW</c>) is proposed in
    /// the contract for main.
    ///
    /// <para><b>The point source.</b> A fire radiating a share χ of its heat release Q puts q = χQ / (4πR²) on a surface square to
    /// it at R (Modak 1977, "Thermal radiation from pool fires", Combustion and Flame 29:177–192; as the SFPE Handbook and the
    /// US NRC's NUREG-1805 carry it). NUREG-1805 gives it within 5 per cent beyond 2.5 fire diameters and says it overstates
    /// the heat closer in; Madrzykowski and Fleischmann (NIST, 2010) found it understating at about a flame length for two
    /// fuels of three. A person sits a metre from a campfire half a metre wide, inside that range; the words say so.</para>
    ///
    /// <para><b>The body's share.</b> The area a body shows a distant source is Fanger's: A_p = f_p f_eff A_D, with f_eff the
    /// share of the body's surface that radiates to its surroundings (0.725 standing, 0.696 seated) and f_p the projected area
    /// factor by the source's altitude and its bearing from the body's front (Fanger 1970, <i>Thermal Comfort</i>; the tables
    /// as ASHRAE 55-2020 Addendum d, appendix C, and the pythermalcomfort library's <c>solar_gain</c> carry them, the two
    /// identical). A fire on the ground is below a body's centre; the tables stop at the horizon, and the fire's angle below it
    /// is read as the same angle above, a standing body being near enough the same from below as from above (an estimate; a
    /// seated body less so). A_D is the body's own surface, <see cref="Warmth.SkinAreaM2"/>.</para>
    /// </summary>
    public static class FireWarmth
    {
        /// <summary>
        /// The share of a flaming wood fire's heat that leaves as radiation, χ: 0.30. Measured shares run from 0.21 for the flames
        /// above a crib (McCarter and Broido 1965, Pyrodynamics 2:65–85, as Gupta, Torero and Hidalgo 2021 read them) to a third
        /// for cribs (Sunahara et al. 2011, Fire Science and Technology 30:1–25) and 0.29 for Douglas fir burnt whole at NIST
        /// (Sung et al. 2025, NIST Technical Note 2314, which found it falling with the wood's water, 0.41 at 7 per cent to 0.15
        /// at 85); 0.30 for dry trees burnt whole (Johnsson et al. 2025, NIST TN 2327r1).
        /// </summary>
        public const double FlameRadiantShare = 0.30;

        /// <summary>
        /// The share of glowing char's heat that leaves as radiation, χ: 0.5, an estimate. NIST's burns found the radiant share
        /// rising as a fire passed from flame to smoulder, the hot char carrying it (Sung et al. 2025, TN 2314); a surface glowing
        /// at 800 °C sheds most of its heat by radiation, about half of it upward and outward from a bed of coals.
        /// </summary>
        public const double GlowRadiantShare = 0.5;

        /// <summary>How many of its widths off a fire the point source holds to 5 per cent: 2.5 (Modak 1977, as NUREG-1805 states it).</summary>
        public const double PointSourceFromWidths = 2.5;

        /// <summary>
        /// How much of a fire's radiation the skin takes, α: 0.95, the long-wave absorptance ASHRAE 55 and the SolarCal model take
        /// for the body (Arens et al. 2015, Building and Environment 88:3–9). Skin is near black past 3 µm (its emissivity 0.97 to
        /// 0.98, the <see cref="Warmth.SkinEmissivity"/> the body radiates by); a wood fire's soot radiates from about 1 to 5 µm,
        /// where skin reflects some of the shorter part (Hardy, Hammel and Murgatroyd 1956), so this is the top of the range.
        /// </summary>
        public const double SkinAbsorptance = 0.95;

        /// <summary>
        /// The heat on bare skin past which it is tolerated only moments, W/m²: 2.5 kW/m², the tolerance limit Purser gives after
        /// Babrauskas (SFPE Handbook, 3rd ed., 2002, ch. 2-6: more than five minutes below it, thirty seconds at it). Strong
        /// tropical sun is about 1.1 kW/m².
        /// </summary>
        public const double TooHotWm2 = 2500.0;

        /// <summary>The height of a standing body's centre, m: 1.0, an estimate, about the middle of a person's height.</summary>
        public const double StandingCentreM = 1.0;
        /// <summary>The height of a body sitting on the ground's centre, m: 0.45, an estimate (the torso's middle, sitting cross-legged).</summary>
        public const double SittingCentreM = 0.45;

        /// <summary>The fraction of the body's surface that radiates to its surroundings: Fanger's 0.725 standing, 0.696 seated (ASHRAE 55-2020 Addendum d, C1).</summary>
        public static double EffectiveAreaFactor(Posture posture) => posture == Posture.Sitting ? 0.696 : 0.725;

        public static double CentreM(Posture posture) => posture == Posture.Sitting ? SittingCentreM : StandingCentreM;

        // Fanger's projected area factors, rows the source's bearing from the body's front (0, 15, ... 180 degrees), columns its
        // altitude (0, 15, ... 90 degrees): Fanger 1970 as ASHRAE 55-2020 Addendum d and pythermalcomfort's solar_gain carry them.
        private static readonly double[,] StandingFp =
        {
            { 0.350, 0.350, 0.314, 0.258, 0.206, 0.144, 0.082 },
            { 0.342, 0.342, 0.310, 0.252, 0.200, 0.140, 0.082 },
            { 0.330, 0.330, 0.300, 0.244, 0.190, 0.132, 0.082 },
            { 0.310, 0.310, 0.275, 0.228, 0.175, 0.124, 0.082 },
            { 0.283, 0.283, 0.251, 0.208, 0.160, 0.114, 0.082 },
            { 0.252, 0.252, 0.228, 0.188, 0.150, 0.108, 0.082 },
            { 0.230, 0.230, 0.214, 0.180, 0.148, 0.108, 0.082 },
            { 0.242, 0.242, 0.222, 0.180, 0.153, 0.112, 0.082 },
            { 0.274, 0.274, 0.245, 0.203, 0.165, 0.116, 0.082 },
            { 0.304, 0.304, 0.270, 0.220, 0.174, 0.121, 0.082 },
            { 0.328, 0.328, 0.290, 0.234, 0.183, 0.125, 0.082 },
            { 0.344, 0.344, 0.304, 0.244, 0.190, 0.128, 0.082 },
            { 0.347, 0.347, 0.308, 0.246, 0.191, 0.128, 0.082 },
        };

        private static readonly double[,] SittingFp =
        {
            { 0.290, 0.324, 0.305, 0.303, 0.262, 0.224, 0.177 },
            { 0.292, 0.328, 0.294, 0.288, 0.268, 0.227, 0.177 },
            { 0.288, 0.332, 0.298, 0.290, 0.264, 0.222, 0.177 },
            { 0.274, 0.326, 0.294, 0.289, 0.252, 0.214, 0.177 },
            { 0.254, 0.308, 0.280, 0.276, 0.241, 0.202, 0.177 },
            { 0.230, 0.282, 0.262, 0.260, 0.233, 0.193, 0.177 },
            { 0.216, 0.260, 0.248, 0.244, 0.220, 0.186, 0.177 },
            { 0.234, 0.258, 0.236, 0.227, 0.208, 0.180, 0.177 },
            { 0.262, 0.260, 0.224, 0.208, 0.196, 0.176, 0.177 },
            { 0.280, 0.260, 0.210, 0.192, 0.184, 0.170, 0.177 },
            { 0.298, 0.256, 0.194, 0.174, 0.168, 0.168, 0.177 },
            { 0.306, 0.250, 0.180, 0.156, 0.156, 0.166, 0.177 },
            { 0.300, 0.240, 0.168, 0.152, 0.152, 0.164, 0.177 },
        };

        /// <summary>
        /// Fanger's projected area factor for a source at <paramref name="altitudeDeg"/> above (or, read the same, below) the
        /// body's centre and <paramref name="bearingDeg"/> from its front (0 facing, 90 side-on, 180 its back), interpolated
        /// between the tables' 15° steps as the ASHRAE and pythermalcomfort codes do.
        /// </summary>
        public static double ProjectedAreaFactor(Posture posture, double altitudeDeg, double bearingDeg)
        {
            double[,] t = posture == Posture.Sitting ? SittingFp : StandingFp;
            double alt = SimMath.Clamp(Math.Abs(altitudeDeg), 0.0, 90.0);
            double b = Math.Abs(bearingDeg) % 360.0;
            if (b > 180.0) b = 360.0 - b;
            int ai = Math.Min(5, (int)(alt / 15.0)), bi = Math.Min(11, (int)(b / 15.0));
            double fa = (alt - 15.0 * ai) / 15.0, fb = (b - 15.0 * bi) / 15.0;
            double top = t[bi, ai] * (1.0 - fa) + t[bi, ai + 1] * fa;
            double bottom = t[bi + 1, ai] * (1.0 - fa) + t[bi + 1, ai + 1] * fa;
            return top * (1.0 - fb) + bottom * fb;
        }

        /// <summary>The body's area square to a source, m²: A_p = f_p f_eff A_D.</summary>
        public static double ProjectedAreaM2(Posture posture, double altitudeDeg, double bearingDeg) =>
            ProjectedAreaFactor(posture, altitudeDeg, bearingDeg) * EffectiveAreaFactor(posture) * Warmth.SkinAreaM2;

        /// <summary>A fire's radiant power, W, from its flame's heat and its glow's (<see cref="Fire.FlameHeatW"/>, <see cref="Fire.GlowHeatW"/>).</summary>
        public static double RadiantW(double flameHeatW, double glowHeatW) => FlameRadiantShare * Math.Max(0.0, flameHeatW) + GlowRadiantShare * Math.Max(0.0, glowHeatW);

        public static double RadiantW(Fire fire) => fire == null ? 0.0 : RadiantW(fire.FlameHeatW, fire.GlowHeatW);

        /// <summary>The point source's heat on a surface square to it at <paramref name="distanceM"/>, W/m²: χQ / (4πR²).</summary>
        public static double FluxAtWm2(double radiantW, double distanceM) => Math.Max(0.0, radiantW) / (4.0 * Math.PI * Math.Max(0.05, distanceM) * Math.Max(0.05, distanceM));

        /// <summary>
        /// The height of a fire's radiant heart above the ground, m: the middle of its flame, Heskestad's height
        /// L = 0.235 Q^(2/5) − 1.02 D (kW, m; SFPE Handbook, as Madrzykowski and Fleischmann 2010 carry it), over a bed a tenth of
        /// a metre deep; the bed itself when the flame is no taller than it (an estimate of a campfire's lay).
        /// </summary>
        public static double HeartM(double flameHeatW, double widthM)
        {
            double flame = 0.235 * Math.Pow(Math.Max(0.0, flameHeatW) / 1000.0, 0.4) - 1.02 * Math.Max(0.0, widthM);
            return 0.1 + Math.Max(0.0, flame) / 2.0;
        }

        /// <summary>
        /// A fire's warmth on a body at <paramref name="groundDistanceM"/> from its middle, standing or sitting, facing it or
        /// turned <paramref name="bearingDeg"/> from it: the radiant power, the flux at the body, its area square to the fire, the
        /// heat taken, and the words.
        /// </summary>
        public static FireOnBody On(double flameHeatW, double glowHeatW, double widthM, double groundDistanceM, Posture posture, double bearingDeg)
        {
            double radiant = RadiantW(flameHeatW, glowHeatW);
            double rise = CentreM(posture) - HeartM(flameHeatW, widthM);
            double ground = Math.Max(0.05, groundDistanceM);
            double distance = Math.Sqrt(ground * ground + rise * rise);
            double below = Math.Atan2(rise, ground) * 180.0 / Math.PI;
            double flux = FluxAtWm2(radiant, distance);
            double area = ProjectedAreaM2(posture, below, bearingDeg);
            double absorbed = SkinAbsorptance * flux * area;
            bool near = distance < PointSourceFromWidths * Math.Max(0.1, widthM);
            string words;
            if (!(radiant > 0.0)) words = "the fire gives no warmth";
            else if (flux > TooHotWm2) words = "too hot to bear this close: " + Kw(flux) + " on the skin";
            else
                words = "the fire warms you by " + Math.Round(absorbed).ToString("0", CultureInfo.InvariantCulture) + " W, "
                    + (flux >= 1000.0 ? "hot on the skin" : flux >= 300.0 ? "warm on the skin" : "a little warmth")
                    + (bearingDeg > 90.0 ? ", on your back" : "") + (near ? " (near a fire this size the reckoning overstates)" : "");
            return new FireOnBody(radiant, distance, below, flux, area, absorbed, near, words);
        }

        /// <summary>The same for a fire as it burnt over its last advance.</summary>
        public static FireOnBody On(Fire fire, double widthM, double groundDistanceM, Posture posture, double bearingDeg) =>
            On(fire != null ? fire.FlameHeatW : 0.0, fire != null ? fire.GlowHeatW : 0.0, widthM, groundDistanceM, posture, bearingDeg);

        private static string Kw(double wm2) => (wm2 / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " kW/m²";
    }
}
