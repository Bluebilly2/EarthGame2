using System;
using System.Globalization;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>
    /// A thing called by what it is (BF.1): its kind's name, then what it has of its own in a person's words — a stick by
    /// its length against the body, its thickness against the hand and its dryness; a stone by its edge and its weight.
    /// The thresholds live here and nowhere else; the screen shows these words and holds none of its own for a thing.
    /// </summary>
    public static class ThingWords
    {
        /// <summary>The thing's own mass when it has one, else its kind's, kg.</summary>
        public static double MassOf(Definition definition, in ThingState state) => state.Has(ThingFields.Mass) ? state.MassKg : definition.MassKg;

        public static string Describe(Definition definition, in ThingState state)
        {
            if (definition == null) return string.Empty;
            StringBuilder sb = new StringBuilder(definition.DisplayName);
            if (definition.Substance == Substance.Wood)
            {
                if (state.Has(ThingFields.Length)) sb.Append(", ").Append(LengthWord(state.LengthM));
                if (state.Has(ThingFields.Diameter)) sb.Append(", ").Append(ThicknessWord(state.DiameterM));
                if (state.Has(ThingFields.Moisture)) sb.Append(", ").Append(MoistureWord(state.Moisture));
            }
            else if (definition.Substance == Substance.Stone)
            {
                if (state.Has(ThingFields.Edge)) sb.Append(", ").Append(EdgeWord(state.Edge01));
                if (state.Has(ThingFields.Mass)) sb.Append(", ").Append(MassWord(state.MassKg));
            }
            return sb.ToString();
        }

        /// <summary>A length against the body: hand-long under 0.25 m, forearm-long to 0.5, arm-long to 0.9, a pace long to 1.5, long beyond.</summary>
        public static string LengthWord(double m) => m < 0.25 ? "hand-long" : m < 0.5 ? "forearm-long" : m < 0.9 ? "arm-long" : m < 1.5 ? "a pace long" : "long";

        /// <summary>A thickness against the hand: finger-thick under 15 mm, thumb-thick to 30, wrist-thick to 60, arm-thick beyond.</summary>
        public static string ThicknessWord(double m) => m < 0.015 ? "finger-thick" : m < 0.03 ? "thumb-thick" : m < 0.06 ? "wrist-thick" : "arm-thick";

        /// <summary>Water as a share of dry mass: dry under 0.20, damp to 0.30 (the fibre saturation point), wet to 0.40, sodden beyond.</summary>
        public static string MoistureWord(double shareOfDryMass) => shareOfDryMass < 0.20 ? "dry" : shareOfDryMass < 0.30 ? "damp" : shareOfDryMass < 0.40 ? "wet" : "sodden";

        /// <summary>An edge: dull under 0.2, keen to 0.5, sharp beyond.</summary>
        public static string EdgeWord(double edge01) => edge01 < 0.2 ? "dull" : edge01 < 0.5 ? "keen" : "sharp";

        /// <summary>A mass in grams under a tenth of a kilogram, else in kilograms to a tenth.</summary>
        public static string MassWord(double kg) =>
            kg < 0.1 ? Math.Round(kg * 1000.0).ToString("0", CultureInfo.InvariantCulture) + " g" : kg.ToString("0.0", CultureInfo.InvariantCulture) + " kg";
    }
}
