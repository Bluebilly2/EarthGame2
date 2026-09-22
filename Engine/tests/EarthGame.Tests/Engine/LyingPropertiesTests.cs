using System;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>
    /// A lying thing's properties come from its place, as its position does (BF.1 promises 3 and 4): the same address gives
    /// the same state on every machine; a stick is of the tree over it, sized and weighed by that wood and the ground's
    /// wetness; a cobble is of its stone and weighs by its density; a thing taken up keeps all of it.
    /// </summary>
    public sealed class LyingPropertiesTests
    {
        private const int Row = 110, Col = 110;
        private const double CellM = TestRasters.MadeCellM;

        private static readonly Region Fixture = new Region("fixture", "Fixture", Region.Bherwerre.CentreLatitudeDeg,
            Region.Bherwerre.CentreLongitudeDeg, TestRasters.MadeExtentM, 237, 8.0);

        private static int StoneCode(StoneType stone)
        {
            for (int i = 0; i < StoneType.All.Count; i++)
                if (ReferenceEquals(StoneType.All[i], stone)) return i + 1;
            throw new ArgumentException(stone.Name);
        }

        private static byte DryForestFloor => GroundCovers.Pack(GroundCover.ForestFloor, 0);

        [Test]
        public void AStickIsOfTheTreeOnItsCellSizedAndWeighedByItsWoodAndTheGroundsWetness()
        {
            LyingThing stick = new LyingThing(Row, Col, StandLayout.Kind.Stick, 0);
            LyingSite site = new LyingSite(PlantSpecies.Blackbutt, DryForestFloor, null);
            ThingState s = LyingProperties.StateOf(stick, site);
            Assert.That(s.Has(ThingFields.Mass | ThingFields.Length | ThingFields.Diameter | ThingFields.Moisture | ThingFields.Look), Is.True);
            Assert.That(s.Has(ThingFields.Edge), Is.False, "a stick has no edge");
            Assert.That(s.LengthM, Is.InRange(0.4f, 1.6f));
            Assert.That(s.DiameterM, Is.InRange(0.012f, 0.045f));
            Assert.That(s.Moisture, Is.EqualTo(0.15f), "the driest quarter's stick is air-dry");
            double volume = Math.PI / 4.0 * s.DiameterM * s.DiameterM * s.LengthM;
            Assert.That(s.MassKg, Is.EqualTo(Wood.Of(PlantSpecies.Blackbutt).DensityDryKgM3 * volume * (1.0 + s.Moisture)).Within(1e-4), "dry density by volume, plus its water");
            int variant = (int)(StandLayout.Mix((((ulong)(uint)Row << 32) | (uint)Col) ^ ((((ulong)StandLayout.Kind.Stick << 8) | 1UL) << 40)) % StandLayout.Looks);
            Assert.That(s.Look, Is.EqualTo((byte)variant), "the shape it lay in, by the client's own rule");
            Assert.That(StandLayout.Looks, Is.EqualTo(6));
        }

        [Test]
        public void TheSameAddressGivesTheSameStateAndAnotherIndexAnother()
        {
            LyingSite site = new LyingSite(PlantSpecies.Bangalay, DryForestFloor, null);
            ThingState a = LyingProperties.StateOf(new LyingThing(Row, Col, StandLayout.Kind.Stick, 2), site);
            ThingState again = LyingProperties.StateOf(new LyingThing(Row, Col, StandLayout.Kind.Stick, 2), site);
            ThingState other = LyingProperties.StateOf(new LyingThing(Row, Col, StandLayout.Kind.Stick, 3), site);
            Assert.That((a.LengthM, a.DiameterM, a.MassKg, a.Look), Is.EqualTo((again.LengthM, again.DiameterM, again.MassKg, again.Look)));
            Assert.That(a.LengthM == other.LengthM && a.DiameterM == other.DiameterM, Is.False, "two sticks of one cell are not one stick");
        }

        [Test]
        public void TheWetnessQuarterSetsTheMoistureAndTheWettestIsSodden()
        {
            LyingThing stick = new LyingThing(Row, Col, StandLayout.Kind.Stick, 0);
            float[] expected = { 0.15f, 0.22f, 0.32f, 0.45f };
            for (int quarter = 0; quarter < 4; quarter++)
            {
                ThingState s = LyingProperties.StateOf(stick, new LyingSite(PlantSpecies.Blackbutt, GroundCovers.Pack(GroundCover.ForestFloor, quarter), null));
                Assert.That(s.Moisture, Is.EqualTo(expected[quarter]), "quarter " + quarter);
            }
            Assert.That(LyingProperties.FibreSaturation, Is.EqualTo(0.30f), "above it a stick will not take a coal");
        }

        [Test]
        public void AStickOfNoTreeIsThePlainStickWeighedAsALightWood()
        {
            ThingState s = LyingProperties.StateOf(new LyingThing(Row, Col, StandLayout.Kind.Stick, 0), new LyingSite(null, GroundCovers.Pack(GroundCover.Sand, 0), null));
            Assert.That(s.Has(ThingFields.Length | ThingFields.Mass), Is.True);
            Assert.That(LyingProperties.DefinitionOf(StandLayout.Kind.Stick, new LyingSite(null, 0, null)), Is.SameAs(DefinitionCatalogue.Stick));
            Definition of = LyingProperties.DefinitionOf(StandLayout.Kind.Stick, new LyingSite(PlantSpecies.Blackbutt, 0, null));
            Assert.That(of, Is.SameAs(DefinitionCatalogue.StickOf(PlantSpecies.Blackbutt)));
            Assert.That(of.Key, Is.EqualTo("item/stick-blackbutt"));
            Assert.That(of.Substance, Is.EqualTo(Substance.Wood));
            Assert.That(DefinitionCatalogue.WoodOf(of), Is.SameAs(Wood.Of(PlantSpecies.Blackbutt)));
            Assert.That(DefinitionCatalogue.WoodOf(DefinitionCatalogue.Stick), Is.Null, "the plain stick is of no tree");
            Assert.That(DefinitionCatalogue.Stick.Substance, Is.EqualTo(Substance.Wood));
            Assert.That(DefinitionCatalogue.CobbleOf(StoneType.Silcrete).Substance, Is.EqualTo(Substance.Stone));
            Assert.That(DefinitionCatalogue.Player.Substance, Is.EqualTo(Substance.None));
        }

        [Test]
        public void ACobbleIsOfItsStoneAndWeighsByItsDensity()
        {
            LyingThing cobble = new LyingThing(Row, Col, StandLayout.Kind.Cobble, 1);
            ThingState s = LyingProperties.StateOf(cobble, new LyingSite(null, DryForestFloor, StoneType.Silcrete));
            double scale = StoneType.Silcrete.DensityKgM3 / DefinitionCatalogue.CobbleDensityKgM3;
            Assert.That(s.Has(ThingFields.Mass | ThingFields.Diameter | ThingFields.Look), Is.True);
            Assert.That(s.Has(ThingFields.Moisture), Is.False, "stone carries no water");
            Assert.That(s.MassKg, Is.InRange(0.35 * scale, 1.0 * scale));
            double sphere = Math.Pow(6.0 * s.MassKg / (Math.PI * StoneType.Silcrete.DensityKgM3), 1.0 / 3.0);
            Assert.That(s.DiameterM, Is.EqualTo(sphere * 1.15).Within(1e-4), "a cobble is a flattened sphere of its mass");
            Assert.That(LyingProperties.DefinitionOf(StandLayout.Kind.Cobble, new LyingSite(null, 0, StoneType.Silcrete)), Is.SameAs(DefinitionCatalogue.CobbleOf(StoneType.Silcrete)));
            Assert.That(LyingProperties.DefinitionOf(StandLayout.Kind.Cobble, new LyingSite(null, 0, null)), Is.SameAs(DefinitionCatalogue.Cobble));
        }

        /// <summary>A world of the made coast with one trunk beside the cell, and the cell's loose things.</summary>
        private static WorldState World(int trunkRow, int trunkCol)
        {
            RegionRaster loose = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_loose", "loose",
                (row, col) => row == Row && col == Col ? LooseCodes.Pack(3, 2) : 0u, null);
            RegionRaster stand = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_stand", "stand",
                (row, col) => row == trunkRow && col == trunkCol ? StandCodes.Pack(PlantSpecies.CoastBanksia, 8.0) : 0u, null);
            RegionRaster cover = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_cover", "cover",
                (row, col) => GroundCovers.Pack(GroundCover.ForestFloor, 2), null);
            RegionRaster stone = TestRasters.FromCodes(TestRasters.MadeSide, TestRasters.MadeCellM, TestRasters.MadeExtentM, "made_stone", "stone",
                (row, col) => (uint)StoneCode(StoneType.Quartzite), null);
            return new WorldState(1347UL, Fixture, Fixture.WakeClock(), new Heightfield(TestRasters.MadeCoast()), 0, null, null, cover, stand, loose, stone);
        }

        [Test]
        public void TheServerReadsTheSiteFromItsRastersTheNearestTrunkInTheThreeByThree()
        {
            WorldState beside = World(Row - 1, Col + 1);
            LyingSite site = LyingSites.Of(beside, new LyingThing(Row, Col, StandLayout.Kind.Stick, 0));
            Assert.That(site.Species, Is.SameAs(PlantSpecies.CoastBanksia), "the trunk on the diagonal cell");
            Assert.That(GroundCovers.QuarterOf(site.Cover), Is.EqualTo(2));
            Assert.That(site.Stone, Is.SameAs(StoneType.Quartzite));

            WorldState far = World(Row - 2, Col);
            Assert.That(LyingSites.Of(far, new LyingThing(Row, Col, StandLayout.Kind.Stick, 0)).Species, Is.Null, "two cells off is not over the stick");
        }

        [Test]
        public void AThingTakenUpKeepsWhatItWasAndALitterCobbleItsYaw()
        {
            WorldState w = World(Row, Col);
            LyingThing stick = new LyingThing(Row, Col, StandLayout.Kind.Stick, 1);
            Assert.That(LyingThings.TryFind(w, stick, out Double3 at), Is.True);
            Hands hands = new Hands();
            Assert.That(hands.PickUpLying(w, stick, new Double3(at.X, at.Y + 1.65, at.Z)), Is.EqualTo(VerbOutcome.Done));
            CarriedThing held = hands.Things[0];
            Assert.That(held.Definition, Is.SameAs(DefinitionCatalogue.StickOf(PlantSpecies.CoastBanksia)));
            ThingState expected = LyingProperties.StateOf(stick, LyingSites.Of(w, stick));
            Assert.That(held.Item.State.Fields, Is.EqualTo(expected.Fields));
            Assert.That(held.Item.State.LengthM, Is.EqualTo(expected.LengthM));
            Assert.That(held.Item.State.MassKg, Is.EqualTo(expected.MassKg));
            Assert.That(held.Item.State.Look, Is.EqualTo(expected.Look), "the shape it lay in");
            Assert.That(held.Item.Resting, Is.True);

            LyingThing cobble = new LyingThing(Row, Col, StandLayout.Kind.Cobble, 0);
            Assert.That(LyingThings.TryFind(w, cobble, out Double3 cobbleAt), Is.True);
            Assert.That(LyingThings.YawOf(cobble, w.Loose.CellM), Is.InRange(0f, 359f), "the layout's yaw, the one it lay at");
            StandLayout.Place(Row, Col, StandLayout.Kind.Cobble, 0, (int)Math.Round(w.Loose.CellM * 100.0), out _, out _, out int yaw);
            Assert.That(LyingThings.YawOf(cobble, w.Loose.CellM), Is.EqualTo((float)yaw));
        }
    }
}
