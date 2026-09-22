using System;
using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// Work on what stands and on the ground (BF.3 promises 3, 4 and 5), judged from properties: bark strips off a trunk
    /// whose bark strips, by its girth; fibre is cut from a plant that gives it with an edge; a tuft that pulls comes up as a
    /// bundle; a cell clears at two seconds a tuft into bundles where the tufts stood; a dig wants a pointed stick and soil,
    /// takes longer in soil than sand and turns up the plant's tuber; a tree is cut through at a rate that is hours for a
    /// blackbutt with a flake and falls into logs the hands do not lift.
    /// </summary>
    public sealed class WorkStandingTests
    {
        private static readonly Definition Blackbutt = DefinitionCatalogue.PlantOf(PlantSpecies.Blackbutt);
        private static readonly Definition Banksia = DefinitionCatalogue.PlantOf(PlantSpecies.OldManBanksia);
        private static readonly Definition Lomandra = DefinitionCatalogue.PlantOf(PlantSpecies.Lomandra);
        private static readonly Definition Grass = DefinitionCatalogue.PlantOf(PlantSpecies.KangarooGrass);
        private static readonly Definition HeathBanksia = DefinitionCatalogue.PlantOf(PlantSpecies.HeathBanksia);
        private static readonly Definition Flake = DefinitionCatalogue.FlakeOf(StoneType.Silcrete);
        private static readonly Definition Core = DefinitionCatalogue.CobbleOf(StoneType.Silcrete);

        private static ThingState Edge(float edge, float massKg = 0.02f)
        {
            ThingState s = default;
            s.SetMass(massKg);
            s.SetEdge(edge);
            return s;
        }

        private static ThingState Trunk(PlantSpecies species, double heightM, byte flags = 0, byte cut = 0, int quarter = 1) =>
            StandingThings.TrunkState(species, heightM, flags, cut, quarter);

        private static ThingState PointedStick()
        {
            ThingState s = default;
            s.SetLength(1.0f);
            s.SetDiameter(0.025f);
            s.SetMass(0.5f);
            s.SetMarks(ThingMarks.Pointed);
            return s;
        }

        private static GroundSite Site(GroundCover cover, int quarter, PlantSpecies understory, double soilM, double waterM = 0.0, int row = 12, int col = 12)
        {
            byte code = GroundCovers.Pack(cover, quarter);
            return new GroundSite
            {
                Row = row, Col = col, CoverCode = code, Cover = cover, Quarter = quarter,
                Tufts = Tufts.OnCell(code, 4.0, row, col), Understory = understory, SoilDepthM = soilM, WaterDepthM = waterM,
                Centre = new Double3(0, 0, 0),
            };
        }

        [Test]
        public void BarkStripsOffATrunkWhoseBarkStripsByItsGirthAndNotTwice()
        {
            ThingState trunk = Trunk(PlantSpecies.Blackbutt, 25.0);
            WorkOffer offer = Work.Judge(WorkKind.StripTrunk, null, default, Blackbutt, trunk);
            Assert.That(offer.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(offer.Seconds, Is.EqualTo(Work.StripTrunkSeconds));
            Assert.That(offer.Words, Does.Contain("strip the bark off the blackbutt"));
            WorkResult r = Work.Apply(WorkKind.StripTrunk, null, default, Blackbutt, trunk, 7);
            double girth = Math.PI * trunk.DiameterM;
            int strips = Math.Max(1, (int)Math.Round(girth / Work.TrunkStripPerGirthM));
            Assert.That(r.Made.Count, Is.EqualTo(strips), "a strip for every tenth of a metre of girth");
            Assert.That(strips, Is.InRange(10, 60), "a 25 m blackbutt of the stand's stoutness girths some metres at the chest");
            Assert.That(r.Made[0].Definition, Is.SameAs(DefinitionCatalogue.BarkOf(PlantSpecies.Blackbutt)));
            double each = Work.TrunkStripLengthM * Work.TrunkStripWidthM * PlantSpecies.Blackbutt.StrippableBarkM * Work.BarkDensityKgM3 * (1.0 + trunk.Moisture);
            Assert.That(r.Made[0].State.MassKg, Is.EqualTo((float)each).Within(1e-6f));
            Assert.That(r.Made[0].State.Moisture, Is.EqualTo(trunk.Moisture), "as dry as the cell's quarter");
            Assert.That(r.TrunkFlags, Is.EqualTo(TrunkChange.BarkTaken));
            Assert.That(r.TargetChanged, Is.True);
            Assert.That((r.TargetAfter.Marks & ThingMarks.Stripped) != 0, Is.True);
            Assert.That(Work.Judge(WorkKind.StripTrunk, null, default, Blackbutt, r.TargetAfter).Outcome, Is.EqualTo(VerbOutcome.WontWork), "not twice");
            Assert.That(Work.Judge(WorkKind.StripTrunk, null, default, Banksia, Trunk(PlantSpecies.OldManBanksia, 8.0)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "a banksia's bark stays on");
            Assert.That(Work.Judge(WorkKind.StripTrunk, null, default, Lomandra, StandingThings.TuftState(0.7, 2)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "a tuft is no trunk");
            Assert.That(Work.Judge(WorkKind.Strip, null, default, Blackbutt, trunk).Outcome, Is.EqualTo(VerbOutcome.WontWork), "the stick's strip does not take a trunk");
        }

        [Test]
        public void FibreIsCutFromAPlantThatGivesItWithAnEdgeAndATuftThatPullsComesUpAsABundle()
        {
            ThingState tuft = StandingThings.TuftState(0.7, 2);
            Assert.That(Work.Judge(WorkKind.CutFibre, null, default, Lomandra, tuft).Outcome, Is.EqualTo(VerbOutcome.NoTool));
            Assert.That(Work.Judge(WorkKind.CutFibre, Flake, Edge(0.1f), Lomandra, tuft).Outcome, Is.EqualTo(VerbOutcome.NoTool), "a dull flake");
            WorkOffer offer = Work.Judge(WorkKind.CutFibre, Flake, Edge(0.6f), Lomandra, tuft);
            Assert.That(offer.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(offer.Seconds, Is.EqualTo(Work.CutFibreSeconds));
            Assert.That(Work.Judge(WorkKind.CutFibre, Flake, Edge(0.6f), Grass, tuft).Outcome, Is.EqualTo(VerbOutcome.WontWork), "grass gives no fibre");
            WorkResult r = Work.Apply(WorkKind.CutFibre, Flake, Edge(0.6f), Lomandra, tuft, 3);
            Assert.That(r.Made.Count, Is.EqualTo(Work.FibreStrips));
            Assert.That(r.Made[0].Definition, Is.SameAs(DefinitionCatalogue.FibreOf(PlantSpecies.Lomandra)));
            Assert.That(r.Made[0].Definition.Substance, Is.EqualTo(Substance.Fibre));
            Assert.That(r.Made[0].State.LengthM, Is.EqualTo((float)Work.FibreLengthM));
            Assert.That(r.Made[0].State.Moisture, Is.EqualTo(tuft.Moisture), "as wet as the ground");
            Assert.That(r.TargetSpent, Is.True, "the tuft is taken");
            Assert.That(r.Words, Does.Contain("lomandra fibre"));

            // Fibre lays into cord as bark does.
            WorkOffer twist = Work.Judge(WorkKind.Twist, r.Made[0].Definition, r.Made[0].State, r.Made[1].Definition, r.Made[1].State);
            Assert.That(twist.Outcome, Is.EqualTo(VerbOutcome.Done), "a fibre strip in hand on a fibre strip");
            WorkResult cord = Work.Apply(WorkKind.Twist, r.Made[0].Definition, r.Made[0].State, r.Made[1].Definition, r.Made[1].State, 4);
            Assert.That(cord.Made.Count, Is.EqualTo(1));
            Assert.That(cord.Made[0].Definition, Is.SameAs(DefinitionCatalogue.Cord));
            Assert.That(cord.Made[0].State.LengthM, Is.EqualTo((float)(Work.CordPerStripLength * 2 * Work.FibreLengthM)).Within(1e-5f));

            WorkOffer pull = Work.Judge(WorkKind.PullTuft, null, default, Grass, tuft);
            Assert.That(pull.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(pull.Seconds, Is.EqualTo(Work.PullSeconds));
            Assert.That(Work.Judge(WorkKind.PullTuft, null, default, HeathBanksia, tuft).Outcome, Is.EqualTo(VerbOutcome.WontWork), "a heath bush is woody");
            Assert.That(Work.Judge(WorkKind.PullTuft, null, default, Blackbutt, Trunk(PlantSpecies.Blackbutt, 25.0)).Outcome, Is.EqualTo(VerbOutcome.WontWork));
            WorkResult bundle = Work.Apply(WorkKind.PullTuft, null, default, Grass, tuft, 5);
            Assert.That(bundle.Made.Count, Is.EqualTo(1));
            Assert.That(bundle.Made[0].Definition, Is.SameAs(DefinitionCatalogue.BundleOf(PlantSpecies.KangarooGrass)));
            Assert.That(bundle.Made[0].Definition.Substance, Is.EqualTo(Substance.Plant));
            Assert.That(bundle.Made[0].State.MassKg, Is.EqualTo((float)(Work.BundleKg * (1.0 + tuft.Moisture))).Within(1e-6f));
            Assert.That(bundle.TargetSpent, Is.True);
            Assert.That(Work.First(Work.Offers(null, default, Grass, tuft)).Value.Kind, Is.EqualTo(WorkKind.PullTuft), "with nothing in hand, the pull is what a tussock offers");
            Assert.That(Work.First(Work.Offers(Flake, Edge(0.6f), Lomandra, tuft)).Value.Kind, Is.EqualTo(WorkKind.CutFibre), "with an edge, the lomandra offers its fibre first");
        }

        [Test]
        public void ACellClearsAtTwoSecondsATuftIntoBundlesWhereTheTuftsStoodAndNotTwice()
        {
            GroundSite site = Site(GroundCover.Grass, 3, PlantSpecies.KangarooGrass, 0.6);
            int left = site.TuftsLeft;
            Assert.That(left, Is.GreaterThan(3), "a wet grass cell grows a handful of tussocks");
            WorkOffer offer = Work.JudgeGround(WorkKind.ClearGround, null, default, site);
            Assert.That(offer.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(offer.Seconds, Is.EqualTo(Work.ClearSecondsPerTuft * left));
            Assert.That(offer.Words, Does.Contain("clear the ground"));
            GroundResult r = Work.ApplyGround(WorkKind.ClearGround, null, default, site, 9);
            Assert.That(r.Cleared, Is.True);
            int taken = 0;
            for (int k = 0; k < WorldChanges.MostTufts; k++) if ((r.TuftsTaken & (1 << k)) != 0) taken++;
            Assert.That(taken, Is.EqualTo(left), "every tuft taken");
            Assert.That(r.Made.Count, Is.EqualTo(left), "a bundle for each");
            foreach (MadeThing made in r.Made)
            {
                Assert.That(made.Definition.Substance, Is.EqualTo(Substance.Plant));
                Assert.That(Math.Abs(made.AlongM) <= 2.0 && Math.Abs(made.AcrossM) <= 2.0, Is.True, "inside its cell");
            }
            Assert.That(r.Made[0].AlongM != 0.0 || r.Made[0].AcrossM != 0.0, Is.True, "where its tuft stood, not at the centre");
            GroundSite cleared = site;
            cleared.Cleared = true;
            cleared.TuftsTaken = r.TuftsTaken;
            Assert.That(Work.JudgeGround(WorkKind.ClearGround, null, default, cleared).Outcome, Is.EqualTo(VerbOutcome.WontWork), "cleared already");
            GroundSite sand = Site(GroundCover.Sand, 0, null, 0.05);
            Assert.That(Work.JudgeGround(WorkKind.ClearGround, null, default, sand).Outcome, Is.EqualTo(VerbOutcome.WontWork), "nothing grows on sand");
            GroundSite half = site;
            half.TuftsTaken = 1;
            Assert.That(Work.JudgeGround(WorkKind.ClearGround, null, default, half).Seconds, Is.EqualTo(Work.ClearSecondsPerTuft * (left - 1)), "one taken already, one less to clear");
        }

        [Test]
        public void ADigWantsAPointedStickAndSoilTakesLongerInSoilThanSandAndTurnsUpTheTuber()
        {
            GroundSite soil = Site(GroundCover.Sedge, 2, PlantSpecies.Lomandra, 0.6);
            Assert.That(Work.JudgeGround(WorkKind.Dig, null, default, soil).Outcome, Is.EqualTo(VerbOutcome.NoTool));
            ThingState plain = PointedStick();
            plain.SetMarks(0);
            Assert.That(Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, plain, soil).Outcome, Is.EqualTo(VerbOutcome.NoTool), "an unpointed stick");
            Assert.That(Work.JudgeGround(WorkKind.Dig, Flake, Edge(0.6f), soil).Outcome, Is.EqualTo(VerbOutcome.NoTool), "a flake is no digging stick");
            WorkOffer offer = Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), soil);
            Assert.That(offer.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(offer.Seconds, Is.EqualTo(Work.DigSecondsSoil));
            GroundSite sand = Site(GroundCover.DuneSand, 0, null, 0.5);
            Assert.That(Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), sand).Seconds, Is.EqualTo(Work.DigSecondsSand), "sand digs twice as fast");
            Assert.That(Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), Site(GroundCover.Rock, 0, null, 0.0)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "rock");
            Assert.That(Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), Site(GroundCover.SwampFloor, 3, null, 0.8)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "wet swamp");
            Assert.That(Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), Site(GroundCover.Grass, 1, null, 0.5, waterM: 0.1)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "under water");
            Assert.That(Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), Site(GroundCover.Grass, 1, null, 0.05)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "thin soil");

            GroundResult first = Work.ApplyGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), soil, 11);
            Assert.That(first.DugCm, Is.EqualTo((byte)Work.DigStepCm));
            Assert.That(first.Made.Count, Is.EqualTo(1), "a lomandra grows here: its tuber comes up");
            Assert.That(first.Made[0].Definition, Is.SameAs(DefinitionCatalogue.TuberOf(PlantSpecies.Lomandra)));
            Assert.That(first.Made[0].Definition.Substance, Is.EqualTo(Substance.Food));
            Assert.That(first.Made[0].State.MassKg, Is.InRange(0.7 * PlantSpecies.Lomandra.TuberKg, 1.3 * PlantSpecies.Lomandra.TuberKg));
            Assert.That(first.Words, Does.Contain("tuber"));
            GroundSite deeper = soil;
            deeper.DugCm = (byte)Work.TuberDepthCm;
            GroundResult nothing = Work.ApplyGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), deeper, 11);
            Assert.That(nothing.Made.Count, Is.EqualTo(0), "below the tubers, nothing but earth");
            Assert.That(nothing.DugCm, Is.EqualTo((byte)(Work.TuberDepthCm + Work.DigStepCm)));
            deeper.DugCm = 55;
            Assert.That(Work.JudgeGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), deeper).Outcome, Is.EqualTo(VerbOutcome.WontWork), "0.6 m of soil, 0.55 dug: the hole is down to the stone");
            GroundSite grass = Site(GroundCover.Grass, 1, PlantSpecies.KangarooGrass, 0.5);
            Assert.That(Work.ApplyGround(WorkKind.Dig, DefinitionCatalogue.Stick, PointedStick(), grass, 11).Made.Count, Is.EqualTo(0), "grass has no tuber");
        }

        [Test]
        public void ATreeIsCutThroughWithAHeavyEdgeAtARateThatIsHoursForABlackbuttWithAFlakeAndFallsIntoLogsTheHandsDoNotLift()
        {
            ThingState blackbutt = Trunk(PlantSpecies.Blackbutt, 25.0);
            Assert.That(Work.Judge(WorkKind.CutTrunk, null, default, Blackbutt, blackbutt).Outcome, Is.EqualTo(VerbOutcome.NoTool));
            Assert.That(Work.Judge(WorkKind.CutTrunk, Flake, Edge(0.6f, 0.02f), Blackbutt, blackbutt).Outcome, Is.EqualTo(VerbOutcome.NoTool), "a flake is too light");
            Assert.That(Work.Judge(WorkKind.CutTrunk, Core, Edge(0.3f, 0.5f), Blackbutt, blackbutt).Outcome, Is.EqualTo(VerbOutcome.NoTool), "an edge under 0.4");
            WorkOffer offer = Work.Judge(WorkKind.CutTrunk, Core, Edge(0.6f, 0.5f), Blackbutt, blackbutt);
            Assert.That(offer.Outcome, Is.EqualTo(VerbOutcome.Done));
            Assert.That(offer.Seconds, Is.InRange(86400.0, 432000.0), "days, not hours: the truth of a stone chopper against a blackbutt as stout as the stand draws it");
            Assert.That(offer.Words, Does.Contain(" h"));
            ThingState axe = Edge(1.0f, 2.0f);
            ThingState middling = Trunk(PlantSpecies.Blackbutt, 20.0);
            double d = 2.0 * TreeGeometries.TrunkRadiusAt(TreeGeometries.Blackbutt, 20.0, Work.CutHeightM);
            Assert.That(Work.Judge(WorkKind.CutTrunk, Core, axe, Blackbutt, middling).Seconds, Is.InRange(3600.0 * Math.Pow(d / 0.6, 3) * 0.5, 3600.0 * Math.Pow(d / 0.6, 3) * 2.0),
                "a 2 kg steel axe fells a 60 cm hardwood in about an hour, and this one by the cube of its thickness");
            ThingState halfCut = Trunk(PlantSpecies.Blackbutt, 25.0, 0, 128);
            Assert.That(Work.Judge(WorkKind.CutTrunk, Core, Edge(0.6f, 0.5f), Blackbutt, halfCut).Seconds, Is.EqualTo(offer.Seconds * (1.0 - 128.0 / 255.0)).Within(1.0), "a cut half done takes half the time");

            ThingState sapling = Trunk(PlantSpecies.SwampPaperbark, 3.0);
            WorkOffer small = Work.Judge(WorkKind.CutTrunk, Core, Edge(0.6f, 0.5f), DefinitionCatalogue.PlantOf(PlantSpecies.SwampPaperbark), sapling);
            Assert.That(small.Seconds, Is.InRange(30.0, 1800.0), "a small paperbark in minutes");
            WorkResult r = Work.Apply(WorkKind.CutTrunk, Core, Edge(0.6f, 0.5f), Blackbutt, blackbutt, 13);
            Assert.That(r.TargetSpent, Is.True);
            Assert.That(r.TrunkFlags & TrunkChange.Felled, Is.EqualTo(TrunkChange.Felled));
            double trunkLength = TreeGeometries.TrunkLengthM(TreeGeometries.Blackbutt, 25.0);
            int logs = 0, limbs = 0;
            double along = 0.0;
            foreach (MadeThing made in r.Made)
            {
                if (DefinitionCatalogue.IsLog(made.Definition))
                {
                    logs++;
                    Assert.That(made.State.LengthM, Is.LessThanOrEqualTo((float)Work.LogLengthM + 1e-5f));
                    Assert.That(made.State.MassKg, Is.GreaterThan((float)Hands.MaxLiftKg), "a blackbutt log is more than the hands lift");
                    Assert.That(made.State.Moisture, Is.EqualTo((float)Wood.Blackbutt.GreenMoisture), "green");
                    Assert.That(made.AlongM, Is.GreaterThan(along), "laid down the line in order");
                    along = made.AlongM;
                }
                else
                {
                    limbs++;
                    Assert.That(made.Definition, Is.SameAs(DefinitionCatalogue.StickOf(PlantSpecies.Blackbutt)));
                    Assert.That(made.AlongM, Is.GreaterThan(trunkLength), "the limbs beyond the trunk");
                    Assert.That(Math.Abs(made.AcrossM), Is.EqualTo(0.8).Within(1e-9));
                }
            }
            Assert.That(logs, Is.EqualTo((int)Math.Ceiling(trunkLength / Work.LogLengthM)));
            Assert.That(limbs, Is.EqualTo(Math.Min(Work.MostLimbs, (int)Math.Round(25.0 * Work.LimbsPerMetre))));
            Assert.That(r.Words, Does.Contain("came down"));
            Assert.That(Work.Judge(WorkKind.CutTrunk, Core, Edge(0.6f, 0.5f), Lomandra, StandingThings.TuftState(0.7, 2)).Outcome, Is.EqualTo(VerbOutcome.WontWork), "a tuft is no tree");
        }

        [Test]
        public void TheHandsLiftAtMostWhatAPersonLiftsAndSayWhenTheyCannot()
        {
            Assert.That(Hands.MaxLiftKg, Is.EqualTo(23.0), "NIOSH's load constant");
            Assert.That((byte)VerbOutcome.TooHeavy, Is.EqualTo((byte)17));
            Region fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg, Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);
            WorldState world = new WorldState(1347UL, fixture, fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()));
            Definition log = DefinitionCatalogue.LogOf(PlantSpecies.Blackbutt);
            Entity heavy = world.SpawnItem(log, 301, -300);
            ItemComponent item = default;
            item.Resting = true;
            item.State.SetMass(180f);
            heavy.SetItem(item, world.Tick);
            Entity light = world.SpawnItem(log, 302, -300);
            ItemComponent small = default;
            small.Resting = true;
            small.State.SetMass(15f);
            light.SetItem(small, world.Tick);
            world.Step(0.05);
            Hands hands = new Hands();
            Double3 eye = new Double3(300, world.GroundAt(300, -300) + 1.6, -300);
            Assert.That(hands.PickUp(world, heavy.Id.Value, eye), Is.EqualTo(VerbOutcome.TooHeavy), "180 kg stays where it fell");
            Assert.That(hands.Things.Count, Is.EqualTo(0));
            Assert.That(hands.PickUp(world, light.Id.Value, eye), Is.EqualTo(VerbOutcome.Done), "a 15 kg log is lifted");
            Assert.That(hands.Things.Count, Is.EqualTo(1));
        }
    }
}
