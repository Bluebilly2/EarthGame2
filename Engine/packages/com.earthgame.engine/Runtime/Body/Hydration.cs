using System;

namespace EarthGame.Engine
{
    /// <summary>How thirsty the founder is, in the terms they would use (v1's <c>ThirstLevel</c>).</summary>
    public enum ThirstLevel : byte
    {
        Fine = 0,
        Thirsty = 1,
        VeryThirsty = 2,
        Failing = 3,
        Collapsing = 4,
    }

    /// <summary>
    /// The water in a founder's body (FP.1, the Founder's Path's first beat): how much there is against normal, how
    /// it goes on the world's clock, what thirst leaves of them and what a drink gives back. Ported from the water
    /// part of v1's <c>BodyState</c> (Assets/EarthGame/Sim/Body/BodyState.cs), whose numbers are published human
    /// physiology restated in code; the sweat, heat and illness that v1 charged water for come with the thermal
    /// model, so for now the body loses water at rest alone. The three needs run on different clocks: warmth kills in
    /// hours, thirst in days, hunger in weeks.
    /// </summary>
    public sealed class Hydration
    {
        /// <summary>Water lost per day at rest, litres: breath, skin and urine.</summary>
        public const double BaseWaterLossLPerDay = 2.4;

        /// <summary>
        /// The breath's share of the resting loss, litres a day: the water a resting body exhales (about 0.3 to 0.4 L a
        /// day in the physiology texts; Guyton's insensible loss), already inside <see cref="BaseWaterLossLPerDay"/>.
        /// Exertion is charged only for the breath above this (2026-09-16).
        /// </summary>
        public const double RestingBreathLPerDay = 0.35;

        /// <summary>Total body water in a 70 kg adult, litres, about 60% of them.</summary>
        public const double TotalBodyWaterL = 42.0;

        /// <summary>
        /// The fraction of body water whose loss is usually fatal. At the resting rate it is reached in a little
        /// under three days, which is why water is the fourth thing on the founder's list and not the first.
        /// </summary>
        public const double LethalWaterLoss = 0.15;

        /// <summary>
        /// Water is absorbed at a limited rate, so a litre and a half is about as much as one visit to a creek is
        /// worth, which is why water is somewhere the founder keeps having to go back to until there is something to
        /// carry it in.
        /// </summary>
        public const double MaxDrinkPerVisitL = 1.5;

        /// <summary>The words' thresholds, as a fraction of body water lost (v1's).</summary>
        public const double ThirstyAt = 0.015, VeryThirstyAt = 0.04, FailingAt = 0.07, CollapsingAt = 0.11;

        // What thirst leaves of the founder, interpolated straight from the published dehydration and work-capacity
        // figures rather than fitted to a curve, so the numbers in the table are the numbers in the game: 2% of body
        // water lost costs a little, 4% about a tenth, 6% a quarter, 10% half, and the lethal 15% nearly everything.
        private static readonly double[] LossPoints = { 0.00, 0.02, 0.04, 0.06, 0.10, 0.15 };
        private static readonly double[] CapacityPoints = { 1.00, 0.97, 0.90, 0.75, 0.50, 0.15 };

        /// <summary>Body water against normal, 1 fully watered. Never above 1; it falls below 1 − <see cref="LethalWaterLoss"/> only for a body nothing has yet judged dead.</summary>
        public double Water01 { get; private set; } = 1.0;

        /// <summary>The fraction of body water lost, 0 fully watered.</summary>
        public double Loss => 1.0 - Water01;

        /// <summary>Litres the founder is down on normal.</summary>
        public double DeficitL => Loss * TotalBodyWaterL;

        /// <summary>The word for it now.</summary>
        public ThirstLevel Thirst => LevelOf(Water01);

        /// <summary>What thirst leaves of the founder's work, 0 to 1, by the table.</summary>
        public double WorkCapacity01 => CapacityOf(Water01);

        /// <summary>Whether the water lost is short of the lethal fraction. Nothing acts on it yet: death is the next beat's.</summary>
        public bool IsAlive => Loss < LethalWaterLoss;

        /// <summary>Hours until the loss is lethal at the resting rate; infinite for a body that has stopped losing.</summary>
        public double HoursToCollapse => HoursToCollapseAt(BaseWaterLossLPerDay);

        public double HoursToCollapseAt(double lossLPerDay)
        {
            double remaining = (LethalWaterLoss - Loss) * TotalBodyWaterL;
            if (remaining <= 0.0) return 0.0;
            if (lossLPerDay <= 0.0) return double.PositiveInfinity;
            return remaining / lossLPerDay * 24.0;
        }

        /// <summary>The word for a body with this much water, the one owner of the thresholds for every reader of the wire.</summary>
        public static ThirstLevel LevelOf(double water01)
        {
            double loss = 1.0 - water01;
            if (loss < ThirstyAt) return ThirstLevel.Fine;
            if (loss < VeryThirstyAt) return ThirstLevel.Thirsty;
            if (loss < FailingAt) return ThirstLevel.VeryThirsty;
            if (loss < CollapsingAt) return ThirstLevel.Failing;
            return ThirstLevel.Collapsing;
        }

        /// <summary>The word the founder would use, and the screen shows under the clock; nothing while fine.</summary>
        public static string WordFor(ThirstLevel level)
        {
            switch (level)
            {
                case ThirstLevel.Thirsty: return "thirsty";
                case ThirstLevel.VeryThirsty: return "very thirsty";
                case ThirstLevel.Failing: return "failing";
                case ThirstLevel.Collapsing: return "collapsing";
                default: return string.Empty;
            }
        }

        /// <summary>What a body with this much water can do, 0 to 1, by the table; the one owner for the mover and the validator.</summary>
        public static double CapacityOf(double water01)
        {
            double loss = 1.0 - water01;
            if (loss <= 0.0) return 1.0;
            if (loss >= LossPoints[LossPoints.Length - 1]) return CapacityPoints[CapacityPoints.Length - 1];
            for (int i = 1; i < LossPoints.Length; i++)
            {
                if (loss > LossPoints[i]) continue;
                double t = (loss - LossPoints[i - 1]) / (LossPoints[i] - LossPoints[i - 1]);
                return CapacityPoints[i - 1] + t * (CapacityPoints[i] - CapacityPoints[i - 1]);
            }
            return CapacityPoints[CapacityPoints.Length - 1];
        }

        /// <summary>The last day's rate of loss, litres a day, for the screen and the log.</summary>
        public double LossLPerDay { get; private set; } = BaseWaterLossLPerDay;

        /// <summary>
        /// The body's day going by: <paramref name="days"/> of the world's clock, at rest or working. The clock is the
        /// caller's (<see cref="WorldClock.DaysFor"/>), so a held clock holds the body and a sped one dries it faster.
        /// The loss is the resting 2.4 L a day (FP.1), plus what the breath carries out above its resting share
        /// (<paramref name="breathLPerHour"/>, the heat balance's own latent respiratory term in litres,
        /// <see cref="Warmth.BreathWaterLPerHour"/>), plus the sweat the balance shed (<paramref name="sweatLPerHour"/>,
        /// FP.2). Until 2026-09-16 exertion multiplied the whole resting loss by the metabolism's ratio (v1's pricing:
        /// walking 3.25 times, running 6.25), as if urine and the skin's diffusion rose with the work, and a founder walking
        /// a cool day died of thirst in fourteen hours; the owner asked whether that was right, and it was not. Walking
        /// in cool weather now costs about three litres a day, two days to the lethal loss, and it is the sweat of a
        /// warm day or a run that makes thirst quick: thirst in days, as the design's clock says. Illness, which v1 also
        /// charged, waits for illness.
        /// </summary>
        public void Advance(double days, double breathLPerHour = 0.0, double sweatLPerHour = 0.0)
        {
            if (!(days > 0.0)) return;
            double breathAboveRest = Math.Max(0.0, breathLPerHour * 24.0 - RestingBreathLPerDay);
            LossLPerDay = BaseWaterLossLPerDay + breathAboveRest + Math.Max(0.0, sweatLPerHour) * 24.0;
            Water01 = Math.Max(0.0, Water01 - LossLPerDay * days / TotalBodyWaterL);
        }

        /// <summary>A drink: up to <see cref="MaxDrinkPerVisitL"/> of what is offered, and never past full. Returns the litres taken.</summary>
        public double Drink(double litres)
        {
            double taken = Math.Min(Math.Max(0.0, litres), MaxDrinkPerVisitL);
            taken = Math.Min(taken, (1.0 - Water01) * TotalBodyWaterL);
            Water01 = Math.Min(1.0, Water01 + taken / TotalBodyWaterL);
            return taken;
        }

        /// <summary>A body put back as a save or a developer's setting states it; anything outside 0..1 is clamped.</summary>
        public void Restore(double water01)
        {
            Water01 = double.IsNaN(water01) ? 1.0 : Math.Min(1.0, Math.Max(0.0, water01));
        }
    }
}
