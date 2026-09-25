using System;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// How deep the air's haze is (M1.6h, 2026-09-25): its extinction at the sea from the weather's humidity, which the shaders'
    /// haze (Haze.hlsl) thins with height and fades the land by. Neither station's record gives a visibility, so its depth is
    /// stated at the coast's usual humidity and grows with the humidity as the aerosol's scattering does when its particles
    /// take up water: hazier on a damp morning, clearer on a dry westerly afternoon.
    /// </summary>
    public static class Haze
    {
        /// <summary>
        /// Koschmieder's: the distance at which a dark thing's contrast with the horizon falls to 2 % is ln 50 over the
        /// extinction, so the extinction is 3.912 over the visibility.
        /// </summary>
        public const double Koschmieder = 3.912;

        /// <summary>
        /// The visibility at the coast's usual humidity, m: 40 km, the WMO scale's very clear air (20 to 50 km), stated, since
        /// neither the lighthouse's record nor Nowra's gives one.
        /// </summary>
        public const double VisibilityAtUsualM = 40000.0;

        /// <summary>The coast's usual humidity: the lighthouse's 9am and 3pm means over its record, 74 % and 67 %.</summary>
        public const double UsualHumidity01 = 0.70;

        /// <summary>
        /// How strongly the aerosol's scattering grows with the humidity: the γ of f(RH) = a (1 − RH)^−γ, 0.6, the moderately
        /// hygroscopic aerosol of Titos et al. (2021, Atmos. Chem. Phys. 21, 13031), whose dry reference is 0 to 40 %.
        /// </summary>
        public const double Gamma = 0.6;

        /// <summary>Below this humidity the aerosol is dry and its haze no thinner: the top of that dry reference.</summary>
        public const double DryHumidity01 = 0.40;

        /// <summary>Above this the air is at mist and cloud, which the law is not: the haze is held at its depth here, 10 km.</summary>
        public const double WettestHumidity01 = 0.97;

        /// <summary>
        /// How high the haze thins by e, m: the aerosols' scale height, 1.2 km, as Bruneton's reference implementation of
        /// precomputed atmospheric scattering has it (the air's own molecules thin over 8 km).
        /// </summary>
        public const double ScaleHeightM = 1200.0;

        /// <summary>The haze's extinction at the sea, per metre, at a relative humidity (0 to 1); an unknown humidity is the usual one.</summary>
        public static double PerMetre(double humidity01)
        {
            double rh = double.IsNaN(humidity01) ? UsualHumidity01 : Math.Min(Math.Max(humidity01, DryHumidity01), WettestHumidity01);
            double growth = Math.Pow((1.0 - rh) / (1.0 - UsualHumidity01), -Gamma);
            return Koschmieder / VisibilityAtUsualM * growth;
        }

        /// <summary>How far one sees along the sea at a relative humidity, m.</summary>
        public static double VisibilityM(double humidity01) => Koschmieder / PerMetre(humidity01);
    }
}
