using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Sleep (BF.7 part one, promise 5): the pressure by Daan, Beersma and Borbély's constants, the words, what a short night
    /// costs by Martin's measurement, the rules that wake a sleeper, and the sleeping body's metabolism. The published numbers
    /// are written here again, not read from the class, so a slip in the class is a red test.
    /// </summary>
    public sealed class SleepTests
    {
        [Test]
        public void ThePressureRisesAndFallsByTheTwoProcessModelsConstants()
        {
            // Daan et al. 1984's standard set: rise 18.2 h, fall 4.2 h, thresholds 0.67 and 0.17; with no circadian swing
            // "Tsleep = 5.8 h, Twake = 16.8 h" (Skeldon and Dijk 2025).
            Sleep sleep = new Sleep();
            Assert.That(sleep.Pressure01, Is.EqualTo(0.17).Within(1e-12), "a founder wakes at the beach rested");
            sleep.Advance(16.8, 40.0);
            Assert.That(sleep.Pressure01, Is.EqualTo(1.0 - 0.83 * Math.Exp(-16.8 / 18.2)).Within(1e-12));
            Assert.That(sleep.Pressure01, Is.EqualTo(0.67).Within(0.005), "sleep comes after 16.8 hours awake");
            sleep.FallAsleep(-20.0);
            double start = sleep.Pressure01;
            sleep.Advance(5.8, -20.0);
            Assert.That(sleep.Pressure01, Is.EqualTo(start * Math.Exp(-5.8 / 4.2)).Within(1e-12));
            Assert.That(sleep.Pressure01, Is.EqualTo(0.17).Within(0.005), "and is spent after 5.8 hours asleep");
        }

        [Test]
        public void TheNightIsTheSameHoweverItsTimeIsCut()
        {
            Sleep coarse = new Sleep(), fine = new Sleep();
            coarse.Advance(15.0, 10.0);
            for (int i = 0; i < 900; i++) fine.Advance(1.0 / 60.0, 10.0);
            Assert.That(fine.Pressure01, Is.EqualTo(coarse.Pressure01).Within(1e-12));
            coarse.FallAsleep(-10.0);
            fine.FallAsleep(-10.0);
            coarse.Advance(8.0, -10.0);
            for (int i = 0; i < 480; i++) fine.Advance(1.0 / 60.0, -10.0);
            Assert.That(fine.Pressure01, Is.EqualTo(coarse.Pressure01).Within(1e-12));
            Assert.That(fine.HoursAsleep, Is.EqualTo(8.0).Within(1e-9));
        }

        [Test]
        public void ThirtySixHoursAwakeCostsMartinsElevenPerCent()
        {
            // Martin 1981: after 36 h without sleep, treadmill endurance at 80 per cent of VO2max fell by 11 per cent.
            double p36 = 1.0 - 0.83 * Math.Exp(-36.0 / 18.2);
            Assert.That(Sleep.PressureAfterHoursAwake(36.0), Is.EqualTo(p36).Within(1e-12));
            Assert.That(Sleep.CapacityOf(p36), Is.EqualTo(0.89).Within(0.005));
            // Craven et al. 2022: about 0.4 per cent lower for every hour awake before exercise.
            double perHour = (Sleep.CapacityOf(Sleep.PressureAfterHoursAwake(16.8)) - Sleep.CapacityOf(Sleep.PressureAfterHoursAwake(40.0))) / (40.0 - 16.8);
            Assert.That(perHour, Is.InRange(0.003, 0.006));
            // A normal day costs nothing: capacity is whole until sleep is due.
            Assert.That(Sleep.CapacityOf(Sleep.PressureAfterHoursAwake(16.0)), Is.EqualTo(1.0));
        }

        [Test]
        public void TheWordsComeWithTheHoursAwake()
        {
            Assert.That(Sleep.LevelOf(Sleep.PressureAfterHoursAwake(12.0)), Is.EqualTo(Tiredness.Rested));
            Assert.That(Sleep.LevelOf(Sleep.PressureAfterHoursAwake(17.0)), Is.EqualTo(Tiredness.Tired), "past where sleep comes");
            Assert.That(Sleep.LevelOf(Sleep.PressureAfterHoursAwake(24.5)), Is.EqualTo(Tiredness.VeryTired), "a night without sleep");
            Assert.That(Sleep.LevelOf(Sleep.PressureAfterHoursAwake(37.0)), Is.EqualTo(Tiredness.Exhausted));
            Assert.That(Sleep.WordFor(Tiredness.Rested), Is.Empty);
            Assert.That(Sleep.WordFor(Tiredness.VeryTired), Is.EqualTo("very tired"));
        }

        [Test]
        public void ANightsSleepEndsAtTheDawnAndANapWhenItIsSpent()
        {
            Sleep night = new Sleep();
            night.Advance(15.0, 5.0);
            night.FallAsleep(-15.0);
            double[] sun = { -15.0, -25.0, -40.0, -30.0, -12.0, -7.0 };
            foreach (double s in sun)
            {
                night.Advance(1.5, s);
                Assert.That(night.WakeFor(36.8, ThirstLevel.Fine, s), Is.EqualTo(WakeReason.None), "asleep in the dark at " + s);
            }
            Assert.That(night.Pressure01, Is.LessThan(0.17), "rested long before the dawn, and sleeping on");
            night.Advance(0.5, -5.0);
            Assert.That(night.WakeFor(36.8, ThirstLevel.Fine, -5.0), Is.EqualTo(WakeReason.Dawn));

            // A nap at noon ends of itself when the pressure is spent, not at once.
            Sleep nap = new Sleep();
            nap.Advance(8.0, 40.0);
            nap.FallAsleep(40.0);
            nap.Advance(0.5, 40.0);
            Assert.That(nap.WakeFor(36.9, ThirstLevel.Fine, 40.0), Is.EqualTo(WakeReason.None), "half an hour in, still asleep");
            nap.Advance(4.0, 35.0);
            Assert.That(nap.WakeFor(36.9, ThirstLevel.Fine, 35.0), Is.EqualTo(WakeReason.Rested));
            Assert.That(new Sleep().WakeFor(30.0, ThirstLevel.Collapsing, 0.0), Is.EqualTo(WakeReason.None), "the awake do not wake");
        }

        [Test]
        public void TheColdWakesASleeperAtKreidersLimitAndThirstWakesThemToo()
        {
            // Kreider and Iampietro 1959: 35.5 °C rectal, the limit of cooling "compatible with substantially continuous sleep".
            Sleep sleep = new Sleep();
            sleep.Advance(16.0, 0.0);
            sleep.FallAsleep(-20.0);
            sleep.Advance(2.0, -30.0);
            Assert.That(sleep.WakeFor(35.6, ThirstLevel.Fine, -30.0), Is.EqualTo(WakeReason.None));
            Assert.That(sleep.WakeFor(35.5, ThirstLevel.Fine, -30.0), Is.EqualTo(WakeReason.Cold), "half a degree above hypothermia");
            Assert.That(Sleep.ColdWakeCoreC, Is.GreaterThan(Warmth.HypothermiaC));
            Assert.That(sleep.WakeFor(36.5, ThirstLevel.Thirsty, -30.0), Is.EqualTo(WakeReason.None));
            Assert.That(sleep.WakeFor(36.5, ThirstLevel.VeryThirsty, -30.0), Is.EqualTo(WakeReason.Thirst));
            Assert.That(sleep.WakeFor(35.0, ThirstLevel.VeryThirsty, 10.0), Is.EqualTo(WakeReason.Cold), "the cold first");
        }

        [Test]
        public void TheSleepingBodyMakesGoldbergsShareOfItsBasalHeat()
        {
            // Goldberg et al. 1988: the whole night's rate 0.95 of the basal (0.85 to 1.02), its lowest hour 0.88; Seale and Conway
            // 1999 found the night equal to the basal.
            Assert.That(Sleep.SleepingMetabolicShare, Is.EqualTo(0.95).Within(1e-12));
            Assert.That(Sleep.SleepingMetabolicShare * Warmth.BasalHeatW, Is.InRange(0.88 * 80.0, 1.0 * 80.0));
        }

        [Test]
        public void ARestoredSleeperIsWhatTheSaveSaid()
        {
            Sleep s = new Sleep();
            s.Restore(0.6, true, true);
            Assert.That(s.Asleep, Is.True);
            Assert.That(s.SleptInDark, Is.True);
            Assert.That(s.Pressure01, Is.EqualTo(0.6));
            s.Restore(double.NaN, false, true);
            Assert.That(s.Pressure01, Is.EqualTo(0.17));
            Assert.That(s.SleptInDark, Is.False, "the awake have no night's sleep in them");
            s.Restore(7.0, false, false);
            Assert.That(s.Pressure01, Is.EqualTo(1.0));
        }
    }
}
