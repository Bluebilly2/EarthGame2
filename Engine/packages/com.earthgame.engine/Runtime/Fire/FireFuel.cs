using System;

namespace EarthGame.Engine
{
    /// <summary>The fires a person keeps, by what they are for (BF.5). Never renumbered.</summary>
    public enum FireSize : byte
    {
        /// <summary>A fire kept low, for light, a little warmth, a pot simmering.</summary>
        Small = 0,
        /// <summary>A fire under a pot brought to the boil, or a roasting stone.</summary>
        Cooking = 1,
        /// <summary>A fire a person sits by through a cold night.</summary>
        Warming = 2,
    }

    /// <summary>
    /// The night's arithmetic (BF.5 part one, promise 9): how many kilograms of wood an hour a fire of a size eats, the heat it
    /// gives for them, and a night's pile. The sizes are published burn rates; the heat is this model's own for them, from
    /// <see cref="EffectiveHeatMJPerKgDry"/>, which is exactly what <see cref="Fire"/> releases from a kilogram burnt to ash in
    /// flame (its gas at the flame's efficiency, its char in full, less its water). One owner: a fire of so many watts eats
    /// what this says.
    /// </summary>
    public static class FireFuel
    {
        /// <summary>
        /// Dry wood a small fire eats, kg/h: 0.57, the three-stone fire at simmer in the Water Boiling Test, 9.49 g a minute of
        /// Douglas fir at 11 per cent (Aprovecho Research Center, Shell Foundation, US EPA and PCIA 2011, <i>Test Results of Cook
        /// Stove Performance</i>, appendix C, the three-stone fire).
        /// </summary>
        public const double SmallDryKgPerHour = 9.49 * 60.0 / 1000.0;

        /// <summary>
        /// Dry wood a cooking fire eats, kg/h: 1.5, the same three-stone fire at high power, 24.08 g a minute bringing 5 litres to
        /// the boil from cold and 25.61 from hot (the same report); their firepower 7.8 and 8.2 kW at the fir's 19.26 MJ/kg.
        /// </summary>
        public const double CookingDryKgPerHour = 1.5;

        /// <summary>
        /// Dry wood a warming fire eats, kg/h: 3, "the typical burn rate of a fireplace" (Houck and Tiegs 1998, <i>Residential
        /// Wood Combustion Technology Review</i>, US EPA-600/R-98-174a). A campground's recreational fire fed a log every ten
        /// minutes burnt 16 to 22 kg an hour (Urbanski 2020, US Forest Service report to the Minnesota Pollution Control Agency):
        /// a warming fire is a fireplace's, not a bonfire's.
        /// </summary>
        public const double WarmingDryKgPerHour = 3.0;

        /// <summary>Dry wood a fire of a size eats, kg/h.</summary>
        public static double DryKgPerHour(FireSize size)
        {
            switch (size)
            {
                case FireSize.Small: return SmallDryKgPerHour;
                case FireSize.Cooking: return CookingDryKgPerHour;
                default: return WarmingDryKgPerHour;
            }
        }

        /// <summary>
        /// The heat a fire gets from a kilogram of dry wood of a moisture, burnt to ash in flame, MJ: its gas's heat at the
        /// flame's efficiency, its char's in full, less its water's latent heat (<see cref="Combustion"/>).
        /// </summary>
        public static double EffectiveHeatMJPerKgDry(Wood wood, double moisture) =>
            (1.0 - Combustion.CharYield) * Combustion.GasHeatMJPerKg(wood) * Combustion.FlameEfficiency
            + Combustion.CharYield * Combustion.CharHeatMJPerKg
            - Combustion.WaterLatentJPerKg / 1e6 * Math.Max(0.0, moisture);

        /// <summary>The heat a fire of a size gives, W, burning this wood at this moisture.</summary>
        public static double HeatReleaseW(FireSize size, Wood wood, double moisture) => DryKgPerHour(size) / 3600.0 * EffectiveHeatMJPerKgDry(wood, moisture) * 1e6;

        /// <summary>Kilograms of dry wood an hour a fire of <paramref name="heatW"/> eats.</summary>
        public static double DryKgPerHour(double heatW, Wood wood, double moisture) =>
            Math.Max(0.0, heatW) * 3600.0 / (EffectiveHeatMJPerKgDry(wood, moisture) * 1e6);

        /// <summary>Kilograms of the wood as it is, water and all, an hour for a fire of <paramref name="heatW"/>: what a person carries to the fire.</summary>
        public static double KgPerHour(double heatW, Wood wood, double moisture) =>
            DryKgPerHour(heatW, wood, moisture) * (1.0 + Math.Max(0.0, moisture));

        /// <summary>Kilograms of the wood as it is an hour for a fire of a size.</summary>
        public static double KgPerHour(FireSize size, double moisture) => DryKgPerHour(size) * (1.0 + Math.Max(0.0, moisture));

        /// <summary>Kilograms of wood as it is for so many hours of a fire of a size: the night's pile.</summary>
        public static double KgFor(FireSize size, double hours, double moisture) => KgPerHour(size, moisture) * Math.Max(0.0, hours);
    }
}
