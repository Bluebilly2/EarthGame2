using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Striking flakes off stone (FP.3), held to the fracture mechanics v1's Knapping restated: Auerbach's law over the
    /// hammer's radius, the cone's fixed angle, the shatter limit, the floor a heavy hammer puts under a flake, the edge
    /// docked for violence, the arm's ceiling; and the stones' derived qualities from their mineralogy. The numbers are
    /// written here again, not read from the class.
    /// </summary>
    public sealed class KnappingTests
    {
        private static double Radius(double massKg, double density) => Math.Pow(3.0 * (massKg / density) / (4.0 * Math.PI), 1.0 / 3.0);

        [Test]
        public void TheStonesQualitiesComeFromTheirMineralogy()
        {
            // Flint: fine, conchoidal, hard: near the best edge, one of the worst hammers. Basalt the other way round.
            Assert.That(StoneType.Flint.EdgeQuality, Is.EqualTo(0.95 / (1.0 + 0.001 * 4.0) * (0.35 + 0.65 * 1.0)).Within(1e-9));
            Assert.That(StoneType.Flint.EdgeQuality, Is.GreaterThan(0.9));
            Assert.That(StoneType.Quartzite.EdgeQuality, Is.EqualTo(0.25).Within(1e-9), "quartzite derives to a 0.25 edge, crude and real");
            Assert.That(StoneType.Sandstone.Knappability, Is.LessThan(Knapping.MinimumKnappability), "sandstone does not flake");
            Assert.That(StoneType.Silcrete.Knappability, Is.GreaterThan(0.5), "the coast's everyday knapping stone");
            Assert.That(StoneType.Flint.PoundingQuality, Is.LessThan(0.1));
            Assert.That(StoneType.Basalt.PoundingQuality, Is.GreaterThan(0.5));
            double tough = Math.Min(1.0, Math.Max(0.0, (2.2 - 0.4) / 1.8)), solid = 1.0 - 0.85 * 0.45, coherent = 1.0 / (1.0 + 0.5 * 0.25);
            double heavy = Math.Min(1.0, (2900.0 - 2200.0) / 700.0), hard = Math.Min(1.0, (6.0 - 2.5) / 3.0);
            Assert.That(StoneType.Basalt.PoundingQuality, Is.EqualTo(tough * solid * coherent * (0.55 + 0.25 * heavy + 0.20 * hard)).Within(1e-9));
        }

        [Test]
        public void AKiloAndAHalfOfBasaltStartsAFractureInFlintAtAboutSixJoulesAndNeedsTwiceThatInQuartzite()
        {
            double r = Radius(1.4, 2900.0);
            double flint = 2825.0 * 0.9 * r * r;
            Assert.That(Knapping.CriticalEnergyJ(StoneType.Flint, StoneType.Basalt, 1.4), Is.EqualTo(flint).Within(1e-9));
            Assert.That(flint, Is.InRange(5.0, 7.0), "a gentle tap");
            Assert.That(Knapping.CriticalEnergyJ(StoneType.Quartzite, StoneType.Basalt, 1.4), Is.EqualTo(flint * 1.8 / 0.9).Within(1e-9));
            Assert.That(Knapping.CriticalEnergyJ(null, StoneType.Basalt, 1.4), Is.EqualTo(double.PositiveInfinity));
            Assert.That(Knapping.HammerRadiusM(null, 0.6), Is.EqualTo(Radius(0.6, DefinitionCatalogue.CobbleDensityKgM3)).Within(1e-12), "a plain cobble's density for a stone of no name");
        }

        [Test]
        public void BelowTheCriticalTheHammerBouncesAndAboveItAFlakeComesAwayByTheExcess()
        {
            StoneCore core = new StoneCore(StoneType.Silcrete, 1.2);
            double critical = Knapping.CriticalEnergyJ(StoneType.Silcrete, StoneType.Basalt, 1.4);
            KnapResult bounce = Knapping.Strike(core, StoneType.Basalt, 1.4, critical * 0.5);
            Assert.That(bounce.Outcome, Is.EqualTo(KnapOutcome.NoFracture));
            Assert.That(bounce.Note, Does.Contain("bounced"));
            Assert.That(core.MassKg, Is.EqualTo(1.2), "nothing happened");

            double energy = 12.0;
            KnapResult flake = Knapping.Strike(core, StoneType.Basalt, 1.4, energy);
            Assert.That(flake.Outcome, Is.EqualTo(KnapOutcome.Flake));
            double expected = Math.Min(Math.Max(0.0022 * (energy - critical), 0.010 * 1.4), 1.2 * 0.22);
            Assert.That(flake.FlakeMassKg, Is.EqualTo(expected).Within(1e-12), "the excess buys size, above the floor the hammer's width puts under it");
            double overshoot = Math.Min(1.0, Math.Max(0.0, (energy / critical - 1.0) / 4.0));
            Assert.That(flake.EdgeQuality, Is.EqualTo(StoneType.Silcrete.EdgeQuality * (1.0 - 0.35 * overshoot)).Within(1e-12));
            Assert.That(core.MassKg, Is.EqualTo(1.2 - expected).Within(1e-12));
            Assert.That(core.PlatformAngleDeg, Is.EqualTo(68.0 + 4.5 + overshoot * 5.0).Within(1e-12), "every flake works the platform toward ninety");
            Assert.That(core.FlakesTaken, Is.EqualTo(1));
            Assert.That(Knapping.IsUsableTool(flake), Is.True);
        }

        [Test]
        public void TooHardAndTheCoreShatters()
        {
            StoneCore core = new StoneCore(StoneType.Silcrete, 1.2);
            KnapResult r = Knapping.Strike(core, StoneType.Basalt, 1.4, 1.2 * 40.0 + 1.0);
            Assert.That(r.Outcome, Is.EqualTo(KnapOutcome.Shatter));
            Assert.That(core.MassKg, Is.EqualTo(1.2 * 0.45).Within(1e-12));
            Assert.That(core.PlatformAngleDeg, Is.EqualTo(68.0 + 14.0));
            Assert.That(core.FlakesTaken, Is.EqualTo(0));
        }

        [Test]
        public void ACoreBreaksUnderAHammerWhoseSmallestBiteIsMoreThanItCanSpare()
        {
            // A kilo of flint under a twenty-three-kilo boulder: the boulder's smallest bite (0.23 kg) is more than the most a
            // blow may take of the core (0.22 kg), and a blow just over the critical (about 39 J) and under the shatter limit
            // (40 J for the kilo) does not reduce the core, it destroys it. A corner the small hammer never reaches.
            StoneCore core = new StoneCore(StoneType.Flint, 1.0);
            double critical = Knapping.CriticalEnergyJ(StoneType.Flint, StoneType.Basalt, 23.0);
            Assert.That(critical, Is.InRange(38.0, 39.9), "just under the shatter limit, so the blow can be over one and under the other");
            KnapResult r = Knapping.Strike(core, StoneType.Basalt, 23.0, 39.95);
            Assert.That(r.Outcome, Is.EqualTo(KnapOutcome.Shatter));
            Assert.That(r.Note, Does.Contain("Too heavy a hammer"));
            Assert.That(core.MassKg, Is.EqualTo(0.5).Within(1e-12));
        }

        [Test]
        public void SandstoneCrumblesAndWillNeverTakeAnEdge()
        {
            StoneCore core = new StoneCore(StoneType.Sandstone, 1.0);
            KnapResult r = Knapping.Strike(core, StoneType.Basalt, 1.4, 10.0);
            Assert.That(r.Outcome, Is.EqualTo(KnapOutcome.Crushed));
            Assert.That(r.Note, Does.Contain("crumbles").And.Contain("never take an edge"));
            Assert.That(core.MassKg, Is.EqualTo(0.98).Within(1e-12), "and a little is lost to the crumbling");
        }

        [Test]
        public void ADeadPlatformCrushesAndTurningTheCoreFindsANewOne()
        {
            StoneCore core = new StoneCore(StoneType.Silcrete, 1.0, 90.0);
            Assert.That(core.IsWorkable, Is.False);
            KnapResult r = Knapping.Strike(core, StoneType.Basalt, 1.4, 10.0);
            Assert.That(r.Outcome, Is.EqualTo(KnapOutcome.Crushed));
            Assert.That(r.Note, Does.Contain("Turn the core"));
            core.Turn();
            Assert.That(core.PlatformAngleDeg, Is.EqualTo(62.0), "a fresh edge, the first turn");
            Assert.That(core.MassKg, Is.EqualTo(0.99 * 0.94).Within(1e-12), "the old edge taken off is mass gone");
            Assert.That(core.IsWorkable, Is.True);
            StoneCore spent = new StoneCore(StoneType.Silcrete, 0.1);
            Assert.That(spent.IsSpent, Is.True);
            Assert.That(Knapping.Strike(spent, StoneType.Basalt, 1.4, 10.0).Note, Does.Contain("nothing left"));
        }

        [Test]
        public void TheArmsCeilingMakesAHammersSizeAChoice()
        {
            Assert.That(Knapping.SwingEnergyJ(1.0, 1.4, 1.0), Is.EqualTo(0.5 * 1.4 * 6.8 * 6.8).Within(1e-9), "a full swing of a kilo and a half");
            Assert.That(Knapping.SwingEnergyJ(1.0, 5.0, 1.0), Is.EqualTo(40.0).Within(1e-9), "the arm's ceiling: five kilos cannot be swung to full speed");
            Assert.That(Knapping.SwingEnergyJ(0.0, 1.4, 1.0), Is.EqualTo(0.0), "no wind-up, no blow");
            Assert.That(Knapping.SwingEnergyJ(1.0, 1.4, 0.5), Is.EqualTo(0.5 * 1.4 * 6.8 * 6.8 * (0.55 + 0.225)).Within(1e-9), "thirst takes from the swing");
            Assert.That(Knapping.SwingEnergyJ(1.0, 20.0, 1.0), Is.LessThan(Knapping.CriticalEnergyJ(StoneType.Silcrete, StoneType.Basalt, 20.0)), "a twenty-kilo boulder cannot start a fracture: Auerbach's law");
            Assert.That(Knapping.SwingEnergyJ(1.0, 1.4, 1.0), Is.GreaterThan(Knapping.CriticalEnergyJ(StoneType.Silcrete, StoneType.Basalt, 1.4)), "a cobble can");
        }

        [Test]
        public void AHarderBlowGivesABiggerAndCoarserFlake()
        {
            StoneCore gentle = new StoneCore(StoneType.Silcrete, 1.2), hard = new StoneCore(StoneType.Silcrete, 1.2);
            double critical = Knapping.CriticalEnergyJ(StoneType.Silcrete, StoneType.Basalt, 1.4);
            KnapResult a = Knapping.Strike(gentle, StoneType.Basalt, 1.4, critical * 1.2);
            KnapResult b = Knapping.Strike(hard, StoneType.Basalt, 1.4, critical * 4.0);
            Assert.That(a.Outcome, Is.EqualTo(KnapOutcome.Flake));
            Assert.That(b.Outcome, Is.EqualTo(KnapOutcome.Flake));
            Assert.That(b.FlakeMassKg, Is.GreaterThan(a.FlakeMassKg));
            Assert.That(b.EdgeQuality, Is.LessThan(a.EdgeQuality));
            Assert.That(a.Note, Does.Contain("sharp"));
            StoneCore quartzite = new StoneCore(StoneType.Quartzite, 1.2);
            KnapResult crude = Knapping.Strike(quartzite, StoneType.Basalt, 1.4, Knapping.CriticalEnergyJ(StoneType.Quartzite, StoneType.Basalt, 1.4) * 4.0);
            Assert.That(Knapping.IsUsableTool(crude), Is.False, "quartzite swung too hard drops below a usable edge on its own");
        }

        [Test]
        public void AFlintFlakeMakesSixMinutesOfCuttingAboutOne()
        {
            Assert.That(Knapping.CuttingMinutes(6.0, 0.0), Is.EqualTo(6.0));
            Assert.That(Knapping.CuttingMinutes(6.0, StoneType.Flint.EdgeQuality), Is.InRange(1.0, 1.3));
            Assert.That(Knapping.CuttingMinutes(6.0, 2.0), Is.EqualTo(6.0 / 5.5).Within(1e-12), "an edge is a fraction, held to one");
            Assert.That(Knapping.IsUsableTool(new KnapResult(KnapOutcome.Flake, 0.002, 0.9, "")), Is.False, "too small to keep");
            Assert.That(Knapping.IsUsableTool(new KnapResult(KnapOutcome.Flake, 0.01, 0.19, "")), Is.False, "too blunt to keep");
            Assert.That(Knapping.IsUsableTool(new KnapResult(KnapOutcome.Shatter, 0.0, 0.0, "")), Is.False);
        }
    }
}
