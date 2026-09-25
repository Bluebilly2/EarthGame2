using System;

namespace EarthGame.Engine
{
    /// <summary>How sleepy the founder is, in the terms they would use (BF.7). Never renumbered: part two sends it.</summary>
    public enum Tiredness : byte
    {
        Rested = 0,
        Tired = 1,
        VeryTired = 2,
        Exhausted = 3,
    }

    /// <summary>Why a sleeper woke (BF.7). Never renumbered: part two sends it and the log records it.</summary>
    public enum WakeReason : byte
    {
        /// <summary>Still asleep.</summary>
        None = 0,
        /// <summary>The sleep's pressure spent in daylight: a nap ends of itself.</summary>
        Rested = 1,
        /// <summary>The core cooled past what sleep continues through.</summary>
        Cold = 2,
        /// <summary>Thirst.</summary>
        Thirst = 3,
        /// <summary>The dawn.</summary>
        Dawn = 4,
    }

    /// <summary>
    /// Sleep (BF.7 part one, promise 5): the pressure to sleep that waking builds and sleeping spends, what it leaves of the
    /// body's work, and the rules that wake a sleeper. The pressure is Borbély's Process S in the form Daan, Beersma and Borbély
    /// fitted to the slow waves of the sleeping brain (1984, "Timing of human sleep: recovery process gated by a circadian
    /// pacemaker", American Journal of Physiology 246:R161): rising toward one while awake and falling toward nothing while
    /// asleep, each exponentially, with their standard constants as Skeldon and Dijk restate them (2025, "The complexity and
    /// commonness of the two-process model of sleep regulation from a mathematical perspective", npj Biological Timing and
    /// Sleep 2:24). Its circadian half, Process C, is not modelled: the night itself stands for it (<see cref="WakeFor"/>).
    ///
    /// <para>The body asleep is the heat balance's (<see cref="Warmth"/>) with its basal rate at
    /// <see cref="SleepingMetabolicShare"/>, no shivering, and the ground under it (<see cref="GroundContact"/>): the hooks
    /// are the contract's (BF.7, For main to decide). This class keeps no clock of its own: its hours are the caller's, as
    /// <see cref="Hydration.Advance"/>'s days are.</para>
    /// </summary>
    public sealed class Sleep
    {
        /// <summary>
        /// The time constant of the pressure's rise while awake, hours: 18.2, the standard parameter of Daan et al. 1984 ("χw =
        /// 18.2 h", as Skeldon and Dijk 2025 give it). Rusterholz, Dürr and Achermann's eight young men, fitted one by one over
        /// 40 hours awake, ranged from 14.1 to 26.4 h (2010, Sleep 33:491–498).
        /// </summary>
        public const double RiseHours = 18.2;

        /// <summary>
        /// The time constant of its fall while asleep, hours: 4.2, the same standard set's ("χs = 4.2 h"). Rusterholz et al.'s
        /// individual fits fell faster, 1.2 to 2.9 h.
        /// </summary>
        public const double FallHours = 4.2;

        /// <summary>
        /// The pressure at which sleep comes: 0.67, the mean upper threshold of the same set ("H0+ = 0.67"). A founder reaches it
        /// after 16.8 hours awake from rested, which is the model's natural day (Skeldon and Dijk: "Twake = 16.8 h").
        /// </summary>
        public const double UpperThreshold = 0.67;

        /// <summary>The pressure at which sleep ends of itself: 0.17, the mean lower threshold ("H0− = 0.17"); a founder wakes at the beach rested, here.</summary>
        public const double LowerThreshold = 0.17;

        /// <summary>
        /// The body's metabolic rate asleep as a share of its basal: 0.95, the mean ratio of the whole night's rate to the basal
        /// in forty lean adults measured by whole-body calorimetry (Goldberg, Prentice, Davies and Murgatroyd 1988, European
        /// Journal of Clinical Nutrition 42:137–144; its lowest hour 0.88). Seale and Conway found the night's rate equal to the
        /// basal (1999, European Journal of Clinical Nutrition 53:107–111); lying down is colder than standing because the body
        /// makes a little less heat and the ground takes some of it.
        /// </summary>
        public const double SleepingMetabolicShare = 0.95;

        /// <summary>
        /// The core temperature below which sleep does not go on, °C: 35.5. "Lowest values measured during sleep in the coldest
        /// environment were 35.5°C, 30.5℃ and 78 Cal/m2 for Tr, Ts and body heat debt … These values may represent the limits
        /// of body cooling compatible with substantially continuous sleep in the cold" (Kreider and Iampietro 1959, Journal of
        /// Applied Physiology 14:765–767). A sleeper is woken by the cold half a degree above hypothermia.
        /// </summary>
        public const double ColdWakeCoreC = 35.5;

        /// <summary>
        /// The sun's elevation at which the dawn wakes a sleeper, degrees: −6, civil dawn. An estimate standing for the circadian
        /// process the model leaves out: foragers without clocks woke "on average an hour before sunrise … well before civil
        /// twilight" in two of three societies and an hour after it in the third, near "the nadir of daily ambient temperature"
        /// (Yetish et al. 2015, "Natural sleep and its seasonal variations in three pre-industrial societies", Current Biology
        /// 25:2862–2868), and "neither sleep onset nor offset were tightly linked to solar light level".
        /// </summary>
        public const double DawnElevationDeg = -6.0;

        /// <summary>
        /// The thirst that wakes a sleeper: <see cref="ThirstLevel.VeryThirsty"/>. This model's rule: no source gives thirst's
        /// part in waking a healthy sleeper, and a body losing water at rest reaches this in about sixteen hours, so a night's
        /// sleep begun thirsty ends thirstier.
        /// </summary>
        public const ThirstLevel ThirstWakesAt = ThirstLevel.VeryThirsty;

        /// <summary>
        /// The work capacity lost for each unit of pressure past <see cref="UpperThreshold"/>: 0.51, placed so 36 hours awake
        /// from rested (a pressure of 0.885) costs the 11 per cent of treadmill endurance Martin measured after 36 hours
        /// without sleep (1981, "Effect of sleep deprivation on tolerance of prolonged exercise", European Journal of Applied
        /// Physiology 47:345–354). The second source agrees: a meta-analysis of acute sleep loss found performance "~0.4%"
        /// lower "for every hour awake prior to exercise" (Craven et al. 2022, Sports Medicine 52:2669–2690), and this gives
        /// about half a per cent an hour between 16 and 40 hours awake. A body's strength outlasts its alertness.
        /// </summary>
        public const double CapacityLostPerPressure = 0.51;

        /// <summary>The sleep's pressure, 0 to 1: <see cref="LowerThreshold"/> for a founder who has just woken rested.</summary>
        public double Pressure01 { get; private set; } = LowerThreshold;

        /// <summary>Whether the founder is asleep.</summary>
        public bool Asleep { get; private set; }

        /// <summary>Hours asleep in this sleep, or 0 awake.</summary>
        public double HoursAsleep { get; private set; }

        /// <summary>Whether this sleep has been in the dark: a night's sleep, which the dawn ends, rather than a nap in daylight.</summary>
        public bool SleptInDark { get; private set; }

        /// <summary>The word for it now.</summary>
        public Tiredness Level => LevelOf(Pressure01);

        /// <summary>What the pressure leaves of the founder's work, 0 to 1.</summary>
        public double WorkCapacity01 => CapacityOf(Pressure01);

        /// <summary>
        /// The pressure after <paramref name="hours"/> awake from rested: 1 − (1 − <see cref="LowerThreshold"/>) e^(−h / 18.2).
        /// The one owner of the curve the words and the capacity are read against.
        /// </summary>
        public static double PressureAfterHoursAwake(double hours) =>
            1.0 - (1.0 - LowerThreshold) * Math.Exp(-Math.Max(0.0, hours) / RiseHours);

        /// <summary>
        /// The words' thresholds: tired at <see cref="UpperThreshold"/>, where sleep comes; very tired a whole day and night
        /// awake (24 h from rested); exhausted a day and a half (36 h, Martin's).
        /// </summary>
        public static readonly double VeryTiredAt = PressureAfterHoursAwake(24.0), ExhaustedAt = PressureAfterHoursAwake(36.0);

        /// <summary>The word for a body under this pressure, the one owner of the thresholds.</summary>
        public static Tiredness LevelOf(double pressure01)
        {
            if (pressure01 >= ExhaustedAt) return Tiredness.Exhausted;
            if (pressure01 >= VeryTiredAt) return Tiredness.VeryTired;
            if (pressure01 >= UpperThreshold) return Tiredness.Tired;
            return Tiredness.Rested;
        }

        /// <summary>The word the founder would use; nothing while rested.</summary>
        public static string WordFor(Tiredness level)
        {
            switch (level)
            {
                case Tiredness.Tired: return "tired";
                case Tiredness.VeryTired: return "very tired";
                case Tiredness.Exhausted: return "exhausted";
                default: return string.Empty;
            }
        }

        /// <summary>What a body under this pressure can do, 0 to 1.</summary>
        public static double CapacityOf(double pressure01) =>
            SimMath.Clamp01(1.0 - CapacityLostPerPressure * Math.Max(0.0, pressure01 - UpperThreshold));

        /// <summary>Lie down and sleep, the sun at <paramref name="solarElevationDeg"/>.</summary>
        public void FallAsleep(double solarElevationDeg)
        {
            Asleep = true;
            HoursAsleep = 0.0;
            SleptInDark = solarElevationDeg < DawnElevationDeg;
        }

        /// <summary>Get up.</summary>
        public void Wake()
        {
            Asleep = false;
            HoursAsleep = 0.0;
            SleptInDark = false;
        }

        /// <summary>
        /// The founder's hours going by, asleep or awake as they are, the sun at <paramref name="solarElevationDeg"/> at the
        /// step's end: the pressure falls toward nothing with <see cref="FallHours"/> asleep and rises toward one with
        /// <see cref="RiseHours"/> awake. Exact for any span, so the server's step and a night's catch-up agree.
        /// </summary>
        public void Advance(double hours, double solarElevationDeg)
        {
            if (!(hours > 0.0)) return;
            if (Asleep)
            {
                Pressure01 *= Math.Exp(-hours / FallHours);
                HoursAsleep += hours;
                if (solarElevationDeg < DawnElevationDeg) SleptInDark = true;
            }
            else
                Pressure01 = 1.0 - (1.0 - Pressure01) * Math.Exp(-hours / RiseHours);
        }

        /// <summary>
        /// Whether this sleeper wakes now, and why, by the first rule that holds: the cold (a core at or below
        /// <see cref="ColdWakeCoreC"/>), thirst (at <see cref="ThirstWakesAt"/>), the dawn (a night's sleep, the sun past
        /// <see cref="DawnElevationDeg"/>, for a sleeper not so short of sleep that the pressure is still above where sleep
        /// comes), or in daylight the pressure spent (<see cref="LowerThreshold"/>: a nap ends of itself). In the dark a rested
        /// sleeper sleeps on to the dawn, as the circadian night holds people asleep; the rules are the model's, each named with
        /// its source above. <see cref="WakeReason.None"/> for a founder awake or sleeping on.
        /// </summary>
        public WakeReason WakeFor(double coreC, ThirstLevel thirst, double solarElevationDeg)
        {
            if (!Asleep) return WakeReason.None;
            if (coreC <= ColdWakeCoreC) return WakeReason.Cold;
            if (thirst >= ThirstWakesAt) return WakeReason.Thirst;
            if (solarElevationDeg < DawnElevationDeg) return WakeReason.None;
            if (SleptInDark && Pressure01 < UpperThreshold) return WakeReason.Dawn;
            return Pressure01 <= LowerThreshold ? WakeReason.Rested : WakeReason.None;
        }

        /// <summary>A body put back as a save or a developer's setting states it; anything outside 0..1 is clamped, NaN rested.</summary>
        public void Restore(double pressure01, bool asleep, bool sleptInDark)
        {
            Pressure01 = double.IsNaN(pressure01) ? LowerThreshold : SimMath.Clamp01(pressure01);
            Asleep = asleep;
            SleptInDark = asleep && sleptInDark;
            HoursAsleep = 0.0;
        }
    }
}
