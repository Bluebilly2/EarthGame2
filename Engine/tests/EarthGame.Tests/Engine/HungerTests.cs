using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Hunger as an energy balance (BF.7 part one, promise 1): the stores against Cahill's reference man, a total fast against
    /// the hunger strikers who died of one and the lean man who survived one, the liver's glycogen against Rothman's NMR, the
    /// basal rate against the Minnesota men's, and the words and the work at each stage. The published numbers are written
    /// here again, not read from the class, so a slip in the class is a red test.
    /// </summary>
    public sealed class HungerTests
    {
        /// <summary>An hour at rest: the basal rate as the body makes it now.</summary>
        private static void RestOneHour(Hunger h)
        {
            double basal = 80.0 * h.BasalShare01;
            h.Advance(3600.0, basal, basal);
        }

        [Test]
        public void TheStoresAreCahillsReferenceManAtAForagersFat()
        {
            Hunger h = new Hunger();
            Assert.That(h.LiverKg, Is.EqualTo(0.07), "Cahill 1983, table 1");
            Assert.That(h.MuscleKg, Is.EqualTo(0.40));
            Assert.That(h.FatKg, Is.EqualTo(0.135 * 70.0).Within(1e-12), "the Hadza men's 13.5 per cent (Pontzer et al. 2012)");
            Assert.That(Hunger.BodyProteinKg, Is.EqualTo(6.0));
            // Chow and Hall 2008: glycogen 17.6, fat 39.5, protein 19.7 MJ/kg; lean tissue 7.6 MJ/kg at 1.6 g of water a gram.
            Assert.That(Hunger.ProteinJPerKg / (1.0 + Hunger.LeanWaterPerProtein) / 1e6, Is.EqualTo(7.6).Within(0.05));
            // The fat alone would carry the basal rate for two months, "two to three months" (Kerndt et al. 1982).
            Assert.That((h.FatKg - Hunger.FatFloorKg) * 39.5e6 / (80.0 * 86400.0), Is.InRange(40.0, 70.0));
            Assert.That(h.Level, Is.EqualTo(HungerLevel.Fed));
            Assert.That(h.WorkCapacity01, Is.EqualTo(1.0));
            Assert.That(h.IsAlive, Is.True);
        }

        [Test]
        public void ATotalFastAtRestKillsWhenTheHungerStrikersDied()
        {
            // The ten men who died on the 1981 hunger strike died after 46 to 73 days without food, mean 62 (CAIN, Ulster University).
            Hunger h = new Hunger();
            int hours = 0;
            while (h.IsAlive && hours < 150 * 24)
            {
                RestOneHour(h);
                hours++;
            }
            double days = hours / 24.0;
            Assert.That(days, Is.InRange(46.0, 73.0), "died on day " + days.ToString("0.0"));
            // Dead past the 2.5 kg of protein the Minnesota men lived with (Keys et al. 1950, p. 333), at the half of the body's
            // protein that is the most Kerndt et al. 1982 allow, with the burnable fat all but gone.
            Assert.That(h.ProteinLostKg, Is.InRange(2.5, 6.0 / 2.0 + 0.05));
            Assert.That(h.FatKg, Is.LessThan(0.3 * Hunger.StartFatKg));
            Assert.That(h.Level, Is.EqualTo(HungerLevel.Wasting));
        }

        [Test]
        public void TheLiversGlycogenGoesInAboutADayAsTheNMRSaw()
        {
            Hunger h = new Hunger();
            for (int i = 0; i < 22; i++) RestOneHour(h);
            // 70 g drawn at first at about 2.9 g an hour (4.3 µmol/(kg·min), Rothman et al. 1991); gone "in the first 18 to 24
            // hours" (Kerndt et al. 1982), keeping the blood's glucose "for 12-16 hours" (Cahill 1983).
            Assert.That(h.LiverKg, Is.InRange(0.015, 0.035), "at 22 hours");
            for (int i = 22; i < 64; i++) RestOneHour(h);
            Assert.That(1.0 - h.LiverKg / 0.07, Is.GreaterThanOrEqualTo(0.83), "at least the 83 per cent Rothman's fasters had lost by 64 hours");
            Assert.That(h.MuscleKg, Is.EqualTo(0.40).Within(1e-9), "at rest the muscles keep theirs: a day's fast leaves it (Loy et al. 1986)");
        }

        [Test]
        public void ProteinIsSpentFastAtFirstAndSparedOnceTheBrainRunsOnKetones()
        {
            Hunger h = new Hunger();
            for (int i = 0; i < 24; i++) RestOneHour(h);
            double before = h.ProteinLostKg;
            for (int i = 24; i < 48; i++) RestOneHour(h);
            double early = (h.ProteinLostKg - before) * 1000.0;
            // 62 to 81 g a day in six fasting men's first week (Cahill et al. 1966, their nitrogen at 1.8 m²), 75 g (Cahill 1983):
            // a resting body spending less, somewhat under.
            Assert.That(early, Is.InRange(40.0, 81.0), "protein on the second day, g");
            for (int i = 48; i < 21 * 24; i++) RestOneHour(h);
            before = h.ProteinLostKg;
            for (int i = 21 * 24; i < 28 * 24; i++) RestOneHour(h);
            double late = (h.ProteinLostKg - before) * 1000.0 / 7.0;
            // A lean faster after the third week: 5 to 7 g of nitrogen a day, 31 to 44 g of protein (Kerndt et al. 1982).
            Assert.That(late, Is.InRange(25.0, 45.0), "protein a day in the fourth week, g");
            Assert.That(late, Is.LessThan(early));
        }

        [Test]
        public void TheWeightFallsFastAtFirstAndThenSlowly()
        {
            // "averaging 0.9 kg per day during the first week and slowing to 0.3 kg per day by the third week" (Kerndt et al. 1982),
            // the early loss mostly salt and water (Cahill 1983: 2 to 2.5 kg in the non-obese).
            Hunger h = new Hunger();
            for (int i = 0; i < 7 * 24; i++) RestOneHour(h);
            double week1 = h.WeightLostKg / 7.0;
            double at21 = 0.0;
            for (int i = 7 * 24; i < 28 * 24; i++)
            {
                RestOneHour(h);
                if (i == 21 * 24 - 1) at21 = h.WeightLostKg;
            }
            double week4 = (h.WeightLostKg - at21) / 7.0;
            Assert.That(week1, Is.InRange(0.4, 1.2), "kg a day in the first week");
            Assert.That(week4, Is.InRange(0.12, 0.4), "kg a day in the fourth");
            Assert.That(week1, Is.GreaterThan(2.0 * week4));
            Assert.That(h.SaltWaterLostKg, Is.EqualTo(2.25).Within(0.05), "the fast's salt and water all shed");
        }

        [Test]
        public void TheBasalRateFallsAsTheMinnesotaMensDid()
        {
            // Keys et al. 1950: after 24 weeks, 15.5 per cent less per kilogram of active tissue (p. 329), 26.9 per cent of the
            // active tissue lost (p. 330) and 38.89 per cent less per man (p. 328); their 10.11 kg of active tissue lost
            // "represents roughly 2.5 kg. of protein" (p. 333).
            double active = 38.80 / 67.53 * 70.0, perProtein = 10.11 / 2.5;
            Hunger h = new Hunger();
            h.Restore(0.0, 0.0, 3.0, 0.269 * active / perProtein, 1.0, 0.0);
            Assert.That(h.BasalShare01, Is.EqualTo((1.0 - 0.155) * (1.0 - 0.269)).Within(1e-9));
            Assert.That(1.0 - h.BasalShare01, Is.EqualTo(0.3889).Within(0.01), "fully adapted");
            Assert.That(h.IsAlive, Is.True, "the Minnesota men lived");
            // Underfed and never fasting, they came to it by their fat, 9.39 kg down to 2.67 (table 166): the founder's fat spent
            // in the same measure, and their 2.5 kg of protein. The fall lags theirs by the share of the fat still to burn.
            h.Restore(0.0, 0.0, 2.67, 2.5, 0.0, 0.0);
            double spent = (0.135 * 70.0 - 2.67) / (0.135 * 70.0 - 0.03 * 70.0);
            Assert.That(h.BasalShare01, Is.EqualTo((1.0 - 0.155 * spent) * (1.0 - 2.5 * perProtein / active)).Within(1e-9));
            Assert.That(1.0 - h.BasalShare01, Is.InRange(0.33, 0.3889), "a third and more, short of their 38.89 per cent");
            // Fat's shell: 10 to 15 per cent of the body's maximal insulation (Veicsteinas et al. 1982), gone with the fat.
            Assert.That(h.TissueInsulationShare01, Is.InRange(0.85, 0.95));
            Assert.That(new Hunger().TissueInsulationShare01, Is.EqualTo(1.0));
            Assert.That(new Hunger().BasalShare01, Is.EqualTo(1.0), "a fed founder makes the whole of it");
        }

        [Test]
        public void TheWordsComeAtTheirStages()
        {
            Hunger h = new Hunger();
            for (int i = 0; i < 3; i++) RestOneHour(h);
            Assert.That(h.Level, Is.EqualTo(HungerLevel.Fed), "three hours after a meal");
            for (int i = 3; i < 8; i++) RestOneHour(h);
            Assert.That(h.Level, Is.EqualTo(HungerLevel.Hungry), "eight hours: the liver giving its glycogen");
            for (int i = 8; i < 30; i++) RestOneHour(h);
            Assert.That(h.Level, Is.EqualTo(HungerLevel.VeryHungry), "the first day");
            for (int i = 30; i < 4 * 24; i++) RestOneHour(h);
            Assert.That(h.Level, Is.EqualTo(HungerLevel.Weak), "the fourth day: ketosis subdues the hunger and leaves the weakness");
            int starving = -1, wasting = -1;
            for (int i = 4 * 24; i < 70 * 24 && h.IsAlive; i++)
            {
                RestOneHour(h);
                if (starving < 0 && h.Level == HungerLevel.Starving) starving = i / 24;
                if (wasting < 0 && h.Level == HungerLevel.Wasting) wasting = i / 24;
            }
            Assert.That(starving, Is.InRange(10, 30), "a tenth of the weight gone");
            // Hunger strikers who lived through 43 days had lost 17.9 per cent (Faintuch et al. 2000); the 18 per cent at which
            // "serious medical problems begin" (Gétaz et al. 2012).
            Assert.That(wasting, Is.InRange(35, 60));
            Assert.That(Hunger.WordFor(HungerLevel.Weak), Is.EqualTo("weak with hunger"));
            Assert.That(Hunger.WordFor(HungerLevel.Fed), Is.Empty);
        }

        [Test]
        public void TheWorkLeftFollowsTheWeightLostAsTheArmyMeasuredIt()
        {
            // Friedl 1995: nothing below 5 per cent; maximal lift 24 per cent down at 16 per cent lost (Ranger-I). Keys et al. 1950:
            // back strength 28 and aerobic capacity 43 per cent down at 24 per cent lost.
            Assert.That(Hunger.CapacityOf(0.04), Is.EqualTo(1.0));
            Assert.That(Hunger.CapacityOf(0.16), Is.EqualTo(0.76).Within(1e-12));
            Assert.That(Hunger.CapacityOf(0.24), Is.InRange(1.0 - 0.43, 1.0 - 0.28));
            Assert.That(Hunger.CapacityOf(0.13), Is.InRange(0.76, 0.95));
            Assert.That(Hunger.CapacityOf(0.9), Is.EqualTo(0.40));
        }

        [Test]
        public void EatingFillsTheStoresAndEndsTheFast()
        {
            Hunger h = new Hunger();
            for (int i = 0; i < 3 * 24; i++) RestOneHour(h);
            Assert.That(h.Level, Is.EqualTo(HungerLevel.VeryHungry), "three days without food, not yet weak");
            double adapted = h.Adaptation01, fat = h.FatKg, protein = h.ProteinLostKg;
            Assert.That(adapted, Is.InRange(0.2, 0.35), "the fast two days in, begun when the liver ran low");
            // A day's food and more, 12 MJ, eaten at once and absorbed over hours at rest.
            h.Eat(12e6);
            for (int i = 0; i < 8; i++) RestOneHour(h);
            Assert.That(h.GutJ, Is.EqualTo(12e6 * Math.Exp(-8.0 / 3.0)).Within(1.0), "absorbed with a time constant of three hours");
            Assert.That(h.LiverKg, Is.EqualTo(0.07).Within(1e-9), "the liver refilled first");
            Assert.That(h.MuscleKg, Is.EqualTo(0.40).Within(1e-9));
            Assert.That(h.ProteinLostKg, Is.LessThan(protein), "protein given back");
            Assert.That(h.FatKg, Is.GreaterThan(fat), "and the rest laid down as fat");
            Assert.That(h.Adaptation01, Is.EqualTo(adapted * Math.Exp(-8.0 / 24.0)).Within(1e-9), "undone over a day while the liver is full");
            Assert.That(h.Level, Is.EqualTo(HungerLevel.Fed));
            // Sixteen hours on, the meal spent and the liver drawn again: hungry, and the fast still going out.
            for (int i = 8; i < 24; i++) RestOneHour(h);
            Assert.That(h.Level, Is.EqualTo(HungerLevel.Hungry));
            Assert.That(h.Adaptation01, Is.LessThan(adapted * Math.Exp(-8.0 / 24.0)));
        }

        [Test]
        public void EatingEnoughEachDayKeepsTheBodyWhole()
        {
            // One meal a day of 12.5 MJ, a little over a day of rest and eight hours' walking at 180 W above it: each night's
            // deficit spends protein and each meal gives it back, and the liver never runs low enough for a fast.
            Hunger h = new Hunger();
            double proteinOnDay10 = 0.0;
            for (int day = 0; day < 30; day++)
            {
                h.Eat(12.5e6);
                for (int hour = 0; hour < 24; hour++)
                {
                    double basal = 80.0 * h.BasalShare01;
                    h.Advance(3600.0, basal + (hour >= 8 && hour < 16 ? 180.0 : 0.0), basal);
                }
                if (day == 9) proteinOnDay10 = h.ProteinLostKg;
            }
            Assert.That(h.Adaptation01, Is.EqualTo(0.0), "never in a fast");
            Assert.That(h.SaltWaterLostKg, Is.EqualTo(0.0));
            Assert.That(h.ProteinLostKg, Is.LessThan(0.1), "no more than a night's protein, given back each morning");
            Assert.That(h.ProteinLostKg, Is.EqualTo(proteinOnDay10).Within(0.005), "and none lost month on month");
            Assert.That(h.FatKg, Is.GreaterThan(Hunger.StartFatKg), "the small surplus laid down as fat");
            Assert.That(h.Level, Is.EqualTo(HungerLevel.Hungry), "hungry by the next meal");
            Assert.That(h.WorkCapacity01, Is.EqualTo(1.0));
        }

        [Test]
        public void WorkDrawsTheMusclesGlycogenAndShortensTheFast()
        {
            Hunger rest = new Hunger(), work = new Hunger();
            for (int i = 0; i < 24; i++)
            {
                rest.Advance(3600.0, 80.0, 80.0);
                work.Advance(3600.0, 80.0 + 180.0, 80.0);
            }
            Assert.That(work.MuscleKg, Is.LessThan(rest.MuscleKg), "walking burns the muscles' glycogen");
            // Half the 180 W of walking from glycogen while full: a day's walking empties it in about a day and a half.
            Assert.That(work.MuscleKg, Is.InRange(0.05, 0.30));
            Assert.That(work.WeightLostKg, Is.GreaterThan(rest.WeightLostKg));
        }

        [Test]
        public void TheSameFastHoweverItsTimeIsCut()
        {
            Hunger coarse = new Hunger(), fine = new Hunger();
            for (int i = 0; i < 10 * 24; i++) coarse.Advance(3600.0, 90.0, 80.0);
            for (int i = 0; i < 10 * 24 * 60; i++) fine.Advance(60.0, 90.0, 80.0);
            Assert.That(coarse.FatKg, Is.EqualTo(fine.FatKg).Within(0.01 * (Hunger.StartFatKg - fine.FatKg)));
            Assert.That(coarse.ProteinLostKg, Is.EqualTo(fine.ProteinLostKg).Within(0.02 * fine.ProteinLostKg));
            Assert.That(coarse.LiverKg, Is.EqualTo(fine.LiverKg).Within(1e-4));
        }
    }
}
