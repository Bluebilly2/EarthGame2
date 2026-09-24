using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The founder's body as bars (M1.F, CANON ruling 48): William, of the early versions, "a way to view active metrics like
    /// thirst, hunger, stamina etc. something like a percentage bar", beside the words FP.1 and FP.2 put under the clock. The
    /// one owner of how the models' numbers become a bar's share: the water from full to the lethal loss (FP.1), the warmth
    /// from the normal core to the lethal one (FP.2), and the strength the work capacity the walk is held to, which falls as
    /// the body dries. Each reads the models' own constants, so a model that moves its ends moves its bar. Hunger and stamina
    /// have no bar until the game models them (BF.7). Engine-free, so the shares can be asserted without a screen.
    /// </summary>
    public static class BodyBars
    {
        /// <summary>What the body says of a bar: nothing yet, one of its first words, or a word that kills if it goes on.</summary>
        public enum Stage : byte
        {
            Fine = 0,
            Word = 1,
            Danger = 2,
        }

        /// <summary>One bar: its name, its share of full, 0 to 1, and its stage.</summary>
        public struct Bar
        {
            public string Name;
            public double Share;
            public Stage Stage;

            /// <summary>The share as the screen writes it, a whole per cent.</summary>
            public int Percent => (int)Math.Round(Share * 100.0);
        }

        /// <summary>The water's share: 1 fully watered, 0 at the loss that kills.</summary>
        public static double WaterShare(double water01) => Clamp01(1.0 - (1.0 - water01) / Hydration.LethalWaterLoss);

        /// <summary>The warmth's share: 1 at a normal core or warmer, 0 at the core that kills.</summary>
        public static double WarmthShare(double coreC) =>
            Clamp01((coreC - Warmth.LethalCoreC) / (Warmth.NormalCoreC - Warmth.LethalCoreC));

        /// <summary>The strength's share: the work a body with this water can do, which the walk's top speed is held to.</summary>
        public static double StrengthShare(double water01) => Clamp01(Hydration.CapacityOf(water01));

        /// <summary>The water's stage, by the thirst's own words: thirsty and very thirsty first, failing and collapsing after.</summary>
        public static Stage WaterStage(double water01)
        {
            ThirstLevel level = Hydration.LevelOf(water01);
            return level == ThirstLevel.Fine ? Stage.Fine
                 : level == ThirstLevel.Thirsty || level == ThirstLevel.VeryThirsty ? Stage.Word
                 : Stage.Danger;
        }

        /// <summary>The warmth's stage, by the cold's own words: chilly and cold first, hypothermic and severely so after.</summary>
        public static Stage WarmthStage(double coreC)
        {
            ColdLevel level = Warmth.LevelOf(coreC);
            return level == ColdLevel.Well ? Stage.Fine
                 : level == ColdLevel.Chilly || level == ColdLevel.Cold ? Stage.Word
                 : Stage.Danger;
        }

        /// <summary>The three bars, water, warmth and strength, for a body with this water and this core.</summary>
        public static Bar[] Read(double water01, double coreC)
        {
            Stage water = WaterStage(water01);
            return new[]
            {
                new Bar { Name = "Water", Share = WaterShare(water01), Stage = water },
                new Bar { Name = "Warmth", Share = WarmthShare(coreC), Stage = WarmthStage(coreC) },
                // The strength comes of the water alone, so it says what the water says.
                new Bar { Name = "Strength", Share = StrengthShare(water01), Stage = water },
            };
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;
    }
}
