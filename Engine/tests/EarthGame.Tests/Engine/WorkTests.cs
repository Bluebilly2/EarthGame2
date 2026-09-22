using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// One model of work (BF.2 promises 1 and 2): each work is judged from properties alone — a stick too thick to break
    /// over the knee refuses in words, bark strips only from a tree whose bark strips, a point wants an edge and takes
    /// longer in harder wood, cord wants a strip in another place — and applying a finished work makes what it promises.
    /// </summary>
    public sealed class WorkTests
    {
        private static readonly Definition Bangalay = DefinitionCatalogue.StickOf(PlantSpecies.Bangalay);
        private static readonly Definition Banksia = DefinitionCatalogue.StickOf(PlantSpecies.CoastBanksia);
        private static readonly Definition Blackbutt = DefinitionCatalogue.StickOf(PlantSpecies.Blackbutt);
        private static readonly Definition Flake = DefinitionCatalogue.FlakeOf(StoneType.Silcrete);

        private static ThingState Stick(float length, float diameter, float moisture = 0.15f, double density = 900.0)
        {
            ThingState s = default;
            s.SetLength(length);
            s.SetDiameter(diameter);
            s.SetMoisture(moisture);
            s.SetMass((float)(density * Math.PI / 4.0 * diameter * diameter * length * (1.0 + moisture)));
            s.SetLook(2);
            return s;
        }

        private static ThingState Edge(float edge)
        {
            ThingState s = default;
            s.SetMass(0.02f);
            s.SetEdge(edge);
            return s;
        }

        [Test]
        public void AStickBreaksOverTheKneeWhenTheWoodsMomentIsBelowAPersonsAndRefusesInWordsWhenNot()
        {
            double limit = Work.MaxBreakableDiameterM(Wood.Of(PlantSpecies.Bangalay).RuptureMPa);
            Assert.That(limit, Is.EqualTo(Math.Pow(32.0 * Work.KneeMomentNm / (Math.PI * 100e6), 1.0 / 3.0)).Within(1e-9), "pi d^3 sigma / 32 against 60 N m");
            Assert.That(limit, Is.InRange(0.017, 0.020), "about eighteen millimetres of bangalay");
            WorkOffer thin = Work.Judge(WorkKind.Break, null, default, Bangalay, Stick(1.0f, 0.016f));
            Assert.That(thin.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(thin.Seconds, Is.EqualTo(Work.BreakSeconds));
            Assert.That(thin.Words, Does.Contain("break"));
            WorkOffer thick = Work.Judge(WorkKind.Break, null, default, Bangalay, Stick(1.0f, 0.035f));
            Assert.That(thick.Outcome, Is.EqualTo(VerbOutcome.WontWork));
            Assert.That(thick.Words, Does.Contain("too thick"));
            Assert.That(Work.Judge(WorkKind.Break, null, default, DefinitionCatalogue.Stick, Stick(1.0f, 0.020f, 0.15f, 500.0)).Outcome, Is.EqualTo(VerbOutcome.Done), "the plain stick breaks at its 60 MPa");
            Assert.That(Work.Judge(WorkKind.Break, null, default, DefinitionCatalogue.CobbleOf(StoneType.Silcrete), default).Outcome, Is.EqualTo(VerbOutcome.WontWork), "a cobble is no stick");
            Assert.That(Work.Judge(WorkKind.Break, Flake, Edge(0.6f), Bangalay, Stick(1.0f, 0.016f)).Outcome, Is.EqualTo(VerbOutcome.Done), "a full hand breaks a stick too");
        }

        [Test]
        public void BreakingLeavesTwoSticksOfTheSameWoodWhoseLengthsAndMassesAddUp()
        {
            ThingState whole = Stick(1.2f, 0.016f);
            WorkResult r = Work.Apply(WorkKind.Break, null, default, Bangalay, whole, 7UL);
            Assert.That(r.TargetSpent, Is.True, "the one stick is gone");
            Assert.That(r.Made.Count, Is.EqualTo(2));
            Assert.That(r.Made[0].Definition, Is.SameAs(Bangalay));
            Assert.That(r.Made[1].Definition, Is.SameAs(Bangalay));
            float a = r.Made[0].State.LengthM, b = r.Made[1].State.LengthM;
            Assert.That(a + b, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(Math.Min(a, b) / 1.2f, Is.InRange(0.35f, 0.65f), "a hashed split, never a sliver");
            Assert.That(r.Made[0].State.MassKg + r.Made[1].State.MassKg, Is.EqualTo(whole.MassKg).Within(1e-4f), "mass by length");
            Assert.That(r.Made[0].State.DiameterM, Is.EqualTo(0.016f));
            Assert.That(r.Made[0].State.Moisture, Is.EqualTo(0.15f));
            Assert.That(r.Made[0].State.Has(ThingFields.Look), Is.True);
            WorkResult again = Work.Apply(WorkKind.Break, null, default, Bangalay, whole, 7UL);
            Assert.That(again.Made[0].State.LengthM, Is.EqualTo(a), "the same salt, the same break");
            Assert.That(Work.Apply(WorkKind.Break, null, default, Bangalay, whole, 8UL).Made[0].State.LengthM, Is.Not.EqualTo(a), "another salt, another");
        }

        [Test]
        public void BarkStripsOnlyFromATreeWhoseBarkStripsAndOnlyOnce()
        {
            Assert.That(PlantSpecies.Bangalay.StrippableBarkM, Is.GreaterThan(0.0));
            Assert.That(PlantSpecies.CoastBanksia.StrippableBarkM, Is.EqualTo(0.0), "a banksia's bark stays on");
            WorkOffer yes = Work.Judge(WorkKind.Strip, null, default, Bangalay, Stick(1.2f, 0.025f));
            Assert.That(yes.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(yes.Seconds, Is.EqualTo(1.2 * Work.StripSecondsPerMetre).Within(1e-5), "six seconds a metre");
            Assert.That(Work.Judge(WorkKind.Strip, null, default, Banksia, Stick(1.2f, 0.025f)).Outcome, Is.EqualTo(VerbOutcome.WontWork));
            Assert.That(Work.Judge(WorkKind.Strip, null, default, DefinitionCatalogue.Stick, Stick(1.2f, 0.025f)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "a stick of no tree has no bark to name");
            ThingState stripped = Stick(1.2f, 0.025f);
            stripped.SetMarks(ThingMarks.Stripped);
            Assert.That(Work.Judge(WorkKind.Strip, null, default, Bangalay, stripped).Outcome, Is.EqualTo(VerbOutcome.WontWork), "once");

            WorkResult r = Work.Apply(WorkKind.Strip, null, default, Bangalay, Stick(1.2f, 0.025f, 0.22f), 3UL);
            Assert.That(r.Made.Count, Is.EqualTo(4), "a strip for every 0.3 m");
            Definition bark = DefinitionCatalogue.BarkOf(PlantSpecies.Bangalay);
            Assert.That(r.Made[0].Definition, Is.SameAs(bark));
            Assert.That(bark.Key, Is.EqualTo("item/bark-bangalay"));
            Assert.That(bark.Substance, Is.EqualTo(Substance.Bark));
            Assert.That(r.Made[0].State.LengthM, Is.EqualTo((float)Work.StripLengthM));
            Assert.That(r.Made[0].State.Moisture, Is.EqualTo(0.22f), "as wet as the stick");
            double thickness = Math.Min(PlantSpecies.Bangalay.StrippableBarkM, Work.BarkShareOfDiameter * 0.025);
            double each = Math.PI * 0.025 * Work.StripLengthM * thickness * Work.BarkDensityKgM3 * 1.22;
            Assert.That(r.Made[0].State.MassKg, Is.EqualTo(each).Within(1e-5), "a strip of that bark's thickness at 600 kg/m3");
            Assert.That(r.TargetChanged, Is.True);
            Assert.That((r.TargetAfter.Marks & ThingMarks.Stripped) != 0, Is.True);
            Assert.That(r.TargetAfter.MassKg, Is.EqualTo(Stick(1.2f, 0.025f, 0.22f).MassKg - 4 * each).Within(1e-4), "lighter by its bark");
            Assert.That(r.TargetSpent, Is.False);
        }

        [Test]
        public void APointWantsAnEdgeInHandAndAStickThinEnoughAndTakesLongerInHarderWood()
        {
            ThingState stick = Stick(0.9f, 0.020f);
            Assert.That(Work.Judge(WorkKind.Point, null, default, Banksia, stick).Outcome, Is.EqualTo(VerbOutcome.NoTool), "an empty hand");
            Assert.That(Work.Judge(WorkKind.Point, Bangalay, Stick(0.5f, 0.02f), Banksia, stick).Outcome, Is.EqualTo(VerbOutcome.NoTool), "a stick is no edge");
            Assert.That(Work.Judge(WorkKind.Point, Flake, Edge(0.1f), Banksia, stick).Outcome, Is.EqualTo(VerbOutcome.NoTool), "a dull flake");
            Assert.That(Work.Judge(WorkKind.Point, Flake, Edge(0.6f), Banksia, Stick(0.9f, 0.04f)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "too thick to point");
            WorkOffer soft = Work.Judge(WorkKind.Point, Flake, Edge(0.6f), Banksia, stick);
            WorkOffer hard = Work.Judge(WorkKind.Point, Flake, Edge(0.6f), Blackbutt, stick);
            Assert.That(soft.Outcome, Is.EqualTo(VerbOutcome.Done));
            double volumeCm3 = Math.PI / 4.0 * Math.Pow(0.020, 3) * 1e6;
            double rate = Work.CarveCm3PerSecond * 0.6 * (0.3 + 0.7 * Wood.Of(PlantSpecies.CoastBanksia).Softness01);
            Assert.That(soft.Seconds, Is.EqualTo(volumeCm3 / rate).Within(1e-3), "a cone three diameters long at the stated rate");
            Assert.That(hard.Seconds, Is.GreaterThan(soft.Seconds * 1.8), "blackbutt is the harder wood");
            Assert.That(soft.Words, Does.Contain("point"));
            ThingState pointed = stick;
            pointed.SetMarks(ThingMarks.Pointed);
            Assert.That(Work.Judge(WorkKind.Point, Flake, Edge(0.6f), Banksia, pointed).Outcome, Is.EqualTo(VerbOutcome.WontWork), "already pointed");

            WorkResult r = Work.Apply(WorkKind.Point, Flake, Edge(0.6f), Banksia, stick, 1UL);
            Assert.That(r.TargetChanged && (r.TargetAfter.Marks & ThingMarks.Pointed) != 0, Is.True);
            Assert.That(r.TargetAfter.LengthM, Is.EqualTo(0.9f), "the stick keeps its length");
            Assert.That(r.ToolChanged, Is.True, "the edge wore");
            Assert.That(r.ToolAfter.Edge01, Is.LessThan(0.6f));
            Assert.That(r.ToolAfter.Edge01, Is.GreaterThan(0.5f), "a little");
            WorkResult harder = Work.Apply(WorkKind.Point, Flake, Edge(0.6f), Blackbutt, stick, 1UL);
            Assert.That(harder.ToolAfter.Edge01, Is.LessThan(r.ToolAfter.Edge01), "harder wood wears the edge more");
        }

        [Test]
        public void CordIsTwistedFromAStripInHandAndAStripInAnotherPlaceAndGrowsByTheStrip()
        {
            Definition bark = DefinitionCatalogue.BarkOf(PlantSpecies.Bangalay);
            ThingState strip = default;
            strip.SetLength((float)Work.StripLengthM);
            strip.SetMass(0.024f);
            strip.SetMoisture(0.2f);
            Assert.That(Work.Judge(WorkKind.Twist, null, default, bark, strip).Outcome, Is.EqualTo(VerbOutcome.NoTool), "nothing in hand to lay it with");
            Assert.That(Work.Judge(WorkKind.Twist, bark, strip, Bangalay, Stick(1f, 0.02f)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "a stick will not lay into cord");
            WorkOffer yes = Work.Judge(WorkKind.Twist, bark, strip, bark, strip);
            Assert.That(yes.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(yes.Seconds, Is.EqualTo(Work.TwistSecondsPerJoin));

            WorkResult first = Work.Apply(WorkKind.Twist, bark, strip, bark, strip, 5UL);
            Assert.That(first.ToolSpent && first.TargetSpent, Is.True, "both strips are laid in");
            Assert.That(first.Made.Count, Is.EqualTo(1));
            Assert.That(first.Made[0].Definition, Is.SameAs(DefinitionCatalogue.Cord));
            Assert.That(DefinitionCatalogue.Cord.Substance, Is.EqualTo(Substance.Cord));
            ThingState cord = first.Made[0].State;
            Assert.That(cord.LengthM, Is.EqualTo((float)(Work.CordPerStripLength * 2 * Work.StripLengthM)).Within(1e-6f), "0.45 m of cord a metre of strip");
            Assert.That(cord.MassKg, Is.EqualTo(0.048f).Within(1e-6f));
            Assert.That(cord.Moisture, Is.EqualTo(0.2f));
            Assert.That(cord.DiameterM, Is.EqualTo((float)Work.CordDiameterM));

            WorkOffer more = Work.Judge(WorkKind.Twist, DefinitionCatalogue.Cord, cord, bark, strip);
            Assert.That(more.Outcome, Is.EqualTo(VerbOutcome.Done), "a cord in hand takes another strip");
            WorkResult grown = Work.Apply(WorkKind.Twist, DefinitionCatalogue.Cord, cord, bark, strip, 6UL);
            Assert.That(grown.Made.Count, Is.EqualTo(0), "the cord in hand grows; nothing new is made");
            Assert.That(grown.ToolChanged, Is.True);
            Assert.That(grown.ToolAfter.LengthM, Is.EqualTo(cord.LengthM + (float)(Work.CordPerStripLength * Work.StripLengthM)).Within(1e-6f));
            Assert.That(grown.ToolAfter.MassKg, Is.EqualTo(0.072f).Within(1e-6f));
            Assert.That(grown.TargetSpent, Is.True);
        }

        [Test]
        public void TheOffersComeInTheStatedOrderAndSayWhyNot()
        {
            IReadOnlyList<WorkOffer> empty = Work.Offers(null, default, Bangalay, Stick(1.0f, 0.016f));
            Assert.That(empty.Count, Is.EqualTo(8), "every kind of work on a thing, judged (BF.2's four, BF.3's four)");
            Assert.That(empty[0].Kind, Is.EqualTo(WorkKind.Break));
            Assert.That(empty[1].Kind, Is.EqualTo(WorkKind.Strip));
            Assert.That(empty[2].Kind, Is.EqualTo(WorkKind.Point));
            Assert.That(empty[3].Kind, Is.EqualTo(WorkKind.Twist));
            Assert.That(empty[4].Kind, Is.EqualTo(WorkKind.StripTrunk));
            Assert.That(empty[7].Kind, Is.EqualTo(WorkKind.CutTrunk));
            for (int i = 4; i < 8; i++) Assert.That(empty[i].Possible, Is.False, "a stick is no trunk and no tuft");
            Assert.That(Work.First(empty).HasValue && Work.First(empty).Value.Kind == WorkKind.Break, Is.True, "the first that can be done");
            IReadOnlyList<WorkOffer> edge = Work.Offers(Flake, Edge(0.6f), Banksia, Stick(0.9f, 0.02f));
            Assert.That(Work.First(edge).Value.Kind, Is.EqualTo(WorkKind.Break), "a thin banksia stick breaks before it is pointed; the point is offered second");
            IReadOnlyList<WorkOffer> thick = Work.Offers(Flake, Edge(0.6f), Banksia, Stick(0.9f, 0.028f));
            Assert.That(Work.First(thick).Value.Kind, Is.EqualTo(WorkKind.Point), "too thick to break, no bark to strip, thin enough to point");
            IReadOnlyList<WorkOffer> barked = Work.Offers(Flake, Edge(0.6f), Blackbutt, Stick(0.9f, 0.028f));
            Assert.That(Work.First(barked).Value.Kind, Is.EqualTo(WorkKind.Strip), "a blackbutt's bark strips before it is pointed");
            Assert.That(Work.First(Work.Offers(null, default, DefinitionCatalogue.CobbleOf(StoneType.Quartz), default)).HasValue, Is.False, "nothing to do to a cobble by hand");
        }
    }
}
