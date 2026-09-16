using System;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>What killed the founder (FP.2). Wire-visible and never renumbered.</summary>
    public enum CauseOfDeath : byte
    {
        None = 0,
        /// <summary>The core fell to <see cref="Warmth.LethalCoreC"/>.</summary>
        Cold = 1,
        /// <summary>The body lost <see cref="Hydration.LethalWaterLoss"/> of its water.</summary>
        Thirst = 2,
    }

    /// <summary>
    /// A death and its explanation (FP.2): the path asks that every kill condition be "fully explained on death", and
    /// the sentence is made in one place for the screen and the log alike, from the numbers the server records at the
    /// moment, so no reader can tell a different story. Under the Standard mode (the owner, 2026-09-01) a new founder
    /// wakes at the wake, the world persists, and what was carried lies where they fell.
    /// </summary>
    public readonly struct Death
    {
        public readonly CauseOfDeath Cause;
        /// <summary>The local hour of the day it happened, 0 to 24.</summary>
        public readonly double LocalHour;
        /// <summary>The air and the wind at the body when it happened.</summary>
        public readonly double AirC, WindMs;
        /// <summary>The body's last heat balance: what it lost and what it made, W.</summary>
        public readonly double LossW, ProductionW;
        /// <summary>The core's temperature, °C, and the fraction of body water lost.</summary>
        public readonly double CoreC, WaterLoss;
        /// <summary>Where they fell.</summary>
        public readonly double East, North;

        public Death(CauseOfDeath cause, double localHour, double airC, double windMs, double lossW, double productionW,
                     double coreC, double waterLoss, double east, double north)
        {
            Cause = cause;
            LocalHour = localHour;
            AirC = airC;
            WindMs = windMs;
            LossW = lossW;
            ProductionW = productionW;
            CoreC = coreC;
            WaterLoss = waterLoss;
            East = east;
            North = north;
        }

        /// <summary>The hour as a clock says it, "03:09".</summary>
        public string Clock
        {
            get
            {
                double hour = ((LocalHour % 24.0) + 24.0) % 24.0;
                // Whole minutes, with a hair's grace: 3.15 hours is 189 minutes, not the 188.99999 floating point makes of it.
                int minutes = (int)Math.Floor(hour * 60.0 + 1e-6);
                int h = minutes / 60 % 24, m = minutes % 60;
                return h.ToString("00", CultureInfo.InvariantCulture) + ":" + m.ToString("00", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>The explanation, the mechanism in it, and what happens next: the one owner of the words.</summary>
        public string Explain()
        {
            string next = " A new founder wakes on the beach; what you carried lies where you fell.";
            switch (Cause)
            {
                case CauseOfDeath.Cold:
                    return "You died of the cold at " + Clock + ". A bare body in " + F0(AirC) + "° air and a " + F1(WindMs)
                           + " m/s wind loses about " + F0(LossW) + " W and makes " + F0(ProductionW)
                           + "; shivering held the core for a while and ran out, and at " + F1(CoreC) + "° the heart stops." + next;
                case CauseOfDeath.Thirst:
                    return "You died of thirst at " + Clock + ": " + F1(WaterLoss * Hydration.TotalBodyWaterL) + " litres down, "
                           + F0(WaterLoss * 100.0) + "% of the body's water, and nothing drunk in time." + next;
                default:
                    return "You died at " + Clock + "." + next;
            }
        }

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
