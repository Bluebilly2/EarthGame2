using System;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.ClientCore
{
    /// <summary>
    /// The founder's body as bars (M1.F, CANON ruling 48): water from full to the lethal loss, warmth from the normal core to
    /// the lethal one, strength the capacity the walk is held to; each per cent's stage agreeing with the word under the
    /// clock; three bars in one order.
    /// </summary>
    public sealed class BodyBarsTests
    {
        [Test]
        public void WaterRunsFromFullToTheLethalLoss()
        {
            Assert.That(BodyBars.WaterShare(1.0), Is.EqualTo(1.0));
            Assert.That(BodyBars.WaterShare(1.0 - Hydration.LethalWaterLoss), Is.EqualTo(0.0).Within(1e-12));
            Assert.That(BodyBars.WaterShare(0.5), Is.EqualTo(0.0), "a body past the lethal loss shows empty, not below it");
            Assert.That(BodyBars.WaterShare(1.0 - Hydration.ThirstyAt), Is.EqualTo(1.0 - Hydration.ThirstyAt / Hydration.LethalWaterLoss).Within(1e-12));
            Assert.That(BodyBars.WaterShare(1.0 - Hydration.ThirstyAt), Is.EqualTo(0.9).Within(1e-9), "thirsty at nine-tenths of the way from death to full");
        }

        [Test]
        public void WarmthRunsFromTheNormalCoreToTheLethalOne()
        {
            Assert.That(BodyBars.WarmthShare(Warmth.NormalCoreC), Is.EqualTo(1.0));
            Assert.That(BodyBars.WarmthShare(Warmth.LethalCoreC), Is.EqualTo(0.0));
            Assert.That(BodyBars.WarmthShare(39.5), Is.EqualTo(1.0), "a body warmer than normal is full: the cold is what this bar counts down to");
            Assert.That(BodyBars.WarmthShare(Warmth.HypothermiaC),
                        Is.EqualTo((Warmth.HypothermiaC - Warmth.LethalCoreC) / (Warmth.NormalCoreC - Warmth.LethalCoreC)).Within(1e-12));
        }

        [Test]
        public void StrengthIsTheCapacityTheWalkIsHeldTo()
        {
            foreach (double water in new[] { 1.0, 0.99, 0.96, 0.93, 0.9, 0.86, 0.8 })
                Assert.That(BodyBars.StrengthShare(water), Is.EqualTo(Hydration.CapacityOf(water)).Within(1e-12), "water " + water);
        }

        [Test]
        public void EachPerCentsStageAgreesWithTheWordUnderTheClock()
        {
            Assert.That(BodyBars.WaterStage(1.0), Is.EqualTo(BodyBars.Stage.Fine));
            Assert.That(BodyBars.WaterStage(1.0 - Hydration.ThirstyAt - 1e-6), Is.EqualTo(BodyBars.Stage.Word), "thirsty");
            Assert.That(BodyBars.WaterStage(1.0 - Hydration.VeryThirstyAt - 1e-6), Is.EqualTo(BodyBars.Stage.Word), "very thirsty");
            Assert.That(BodyBars.WaterStage(1.0 - Hydration.FailingAt - 1e-6), Is.EqualTo(BodyBars.Stage.Danger), "failing");
            Assert.That(BodyBars.WaterStage(1.0 - Hydration.CollapsingAt - 1e-6), Is.EqualTo(BodyBars.Stage.Danger), "collapsing");
            Assert.That(BodyBars.WarmthStage(Warmth.NormalCoreC), Is.EqualTo(BodyBars.Stage.Fine));
            Assert.That(BodyBars.WarmthStage(Warmth.ChillyC - 0.01), Is.EqualTo(BodyBars.Stage.Word), "chilly");
            Assert.That(BodyBars.WarmthStage(Warmth.ColdC - 0.01), Is.EqualTo(BodyBars.Stage.Word), "cold");
            Assert.That(BodyBars.WarmthStage(Warmth.HypothermiaC - 0.01), Is.EqualTo(BodyBars.Stage.Danger), "hypothermic");
            Assert.That(BodyBars.WarmthStage(Warmth.SevereHypothermiaC - 0.01), Is.EqualTo(BodyBars.Stage.Danger), "severely hypothermic");
        }

        [Test]
        public void TheThreeBarsComeInOneOrderWithTheirPerCents()
        {
            BodyBars.Bar[] bars = BodyBars.Read(1.0 - Hydration.VeryThirstyAt - 1e-6, Warmth.ColdC - 0.01);
            Assert.That(bars.Length, Is.EqualTo(3));
            Assert.That(bars[0].Name, Is.EqualTo("Water"));
            Assert.That(bars[1].Name, Is.EqualTo("Warmth"));
            Assert.That(bars[2].Name, Is.EqualTo("Strength"));
            Assert.That(bars[0].Percent, Is.EqualTo((int)Math.Round(100.0 * (1.0 - Hydration.VeryThirstyAt / Hydration.LethalWaterLoss))));
            Assert.That(bars[0].Stage, Is.EqualTo(BodyBars.Stage.Word));
            Assert.That(bars[1].Stage, Is.EqualTo(BodyBars.Stage.Word));
            Assert.That(bars[2].Stage, Is.EqualTo(bars[0].Stage), "strength comes of the water, and says what the water says");
            Assert.That(bars[2].Share, Is.EqualTo(Hydration.CapacityOf(1.0 - Hydration.VeryThirstyAt - 1e-6)).Within(1e-12));
        }
    }
}
