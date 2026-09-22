using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>A thing is called by what it is (BF.1 promise 5): the words and their thresholds live in one place.</summary>
    public sealed class ThingWordsTests
    {
        private static ThingState Stick(float length, float diameter, float moisture)
        {
            ThingState s = default;
            s.SetLength(length);
            s.SetDiameter(diameter);
            s.SetMoisture(moisture);
            s.SetMass(0.3f);
            return s;
        }

        [Test]
        public void AStickIsNamedByItsTreeItsSizeAndItsDryness()
        {
            Definition blackbutt = DefinitionCatalogue.StickOf(PlantSpecies.Blackbutt);
            Assert.That(ThingWords.Describe(blackbutt, Stick(0.8f, 0.02f, 0.15f)), Is.EqualTo("a blackbutt stick, arm-long, thumb-thick, dry"));
            Assert.That(ThingWords.Describe(blackbutt, Stick(0.2f, 0.012f, 0.25f)), Is.EqualTo("a blackbutt stick, hand-long, finger-thick, damp"));
            Assert.That(ThingWords.Describe(blackbutt, Stick(0.45f, 0.04f, 0.35f)), Is.EqualTo("a blackbutt stick, forearm-long, wrist-thick, wet"));
            Assert.That(ThingWords.Describe(blackbutt, Stick(1.2f, 0.07f, 0.5f)), Is.EqualTo("a blackbutt stick, a pace long, arm-thick, sodden"));
            Assert.That(ThingWords.Describe(blackbutt, Stick(1.8f, 0.02f, 0.15f)), Is.EqualTo("a blackbutt stick, long, thumb-thick, dry"));
            Assert.That(ThingWords.Describe(DefinitionCatalogue.Stick, default), Is.EqualTo("a stick"), "nothing of its own, nothing said");
            Assert.That(ThingWords.Describe(DefinitionCatalogue.StickOf(PlantSpecies.OldManBanksia), default), Is.EqualTo("an old-man banksia stick"));
        }

        [Test]
        public void AStoneIsNamedByItsEdgeAndItsWeight()
        {
            ThingState flake = default;
            flake.SetMass(0.02f);
            flake.SetEdge(0.6f);
            Assert.That(ThingWords.Describe(DefinitionCatalogue.FlakeOf(StoneType.Silcrete), flake), Is.EqualTo("a silcrete flake, sharp, 20 g"));
            flake.SetEdge(0.3f);
            Assert.That(ThingWords.Describe(DefinitionCatalogue.FlakeOf(StoneType.Silcrete), flake), Is.EqualTo("a silcrete flake, keen, 20 g"));
            flake.SetEdge(0.1f);
            Assert.That(ThingWords.Describe(DefinitionCatalogue.FlakeOf(StoneType.Silcrete), flake), Is.EqualTo("a silcrete flake, dull, 20 g"));

            ThingState cobble = default;
            cobble.SetMass(0.6f);
            Assert.That(ThingWords.Describe(DefinitionCatalogue.CobbleOf(StoneType.Quartz), cobble), Is.EqualTo("a quartz cobble, 0.6 kg"));
            Assert.That(ThingWords.Describe(DefinitionCatalogue.CobbleOf(StoneType.Quartz), default), Is.EqualTo("a quartz cobble"));
            cobble.SetMass(1.04f);
            Assert.That(ThingWords.Describe(DefinitionCatalogue.CobbleOf(StoneType.Obsidian), cobble), Is.EqualTo("an obsidian cobble, 1.0 kg"));
        }

        [Test]
        public void TheMassSaidIsTheThingsOwnElseItsKinds()
        {
            ThingState own = default;
            own.SetMass(0.45f);
            Assert.That(ThingWords.MassOf(DefinitionCatalogue.Cobble, own), Is.EqualTo(0.45).Within(1e-6));
            Assert.That(ThingWords.MassOf(DefinitionCatalogue.Cobble, default), Is.EqualTo(DefinitionCatalogue.Cobble.MassKg));
        }
    }
}
