using System.Collections.Generic;
using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The catalogue of definitions (M1.3 promise 1): keys in the stated form, ids that never collide, physics that lives once.</summary>
    public sealed class DefinitionTests
    {
        [Test]
        public void EveryKeyIsInTheStatedFormAndNoTwoHashAlike()
        {
            HashSet<string> keys = new HashSet<string>();
            HashSet<uint> ids = new HashSet<uint>();
            foreach (Definition d in DefinitionCatalogue.All)
            {
                Assert.That(DefinitionCatalogue.IsValidKey(d.Key), Is.True, d.Key);
                Assert.That(keys.Add(d.Key), Is.True, d.Key + " twice");
                Assert.That(ids.Add(d.Id.Value), Is.True, d.Key + " hashes like another");
                Assert.That(d.Id, Is.EqualTo(DefinitionId.Of(d.Key)));
                Assert.That(DefinitionCatalogue.ById(d.Id), Is.SameAs(d));
                Assert.That(DefinitionCatalogue.ByKey(d.Key), Is.SameAs(d));
            }
            int barks = 0, fibres = 0, bundles = 0, tubers = 0, wooded = 0;
            foreach (PlantSpecies tall in StandCodes.Tall) if (tall.StrippableBarkM > 0.0) barks++;
            foreach (PlantSpecies tall in StandCodes.Tall) if (Wood.Of(tall) != null) wooded++;
            Assert.That(wooded, Is.EqualTo(StandCodes.Tall.Count - 1), "every tall plant but the palm makes wood (WG.2c)");
            foreach (PlantSpecies plant in PlantSpecies.All)
            {
                if (plant.Fibre) fibres++;
                if (Tufts.ShapeOf(plant) != null) bundles++;
                if (plant.TuberKg > 0.0) tubers++;
            }
            Assert.That(fibres, Is.EqualTo(3), "lomandra, saw-sedge, spinifex");
            Assert.That(tubers, Is.EqualTo(2), "lomandra, bracken");
            Assert.That(DefinitionCatalogue.All.Count, Is.EqualTo(1 + PlantSpecies.All.Count + StoneType.All.Count + AnimalSpecies.All.Count + 2 + 2 * StoneType.All.Count + wooded + barks + 1
                                                                + fibres + bundles + tubers + wooded),
                "the player, the tables, the plain cobble and the stick, a cobble and a flake of every stone, a stick of every tall plant that makes wood (BF.1; a palm makes none, WG.2c), a bark strip of every tree whose bark strips and the cord (BF.2), "
                + "the fibre of every plant that gives it, a bundle of every plant of the understorey, the tubers the table names and a log of every tree that makes wood (BF.3)");
        }

        [Test]
        public void TheTablesAreInTheCatalogueByTheirSlugs()
        {
            Assert.That(DefinitionCatalogue.ByKey("plant/lomandra").Row, Is.SameAs(PlantSpecies.Lomandra));
            Assert.That(DefinitionCatalogue.ByKey("stone/silcrete").Row, Is.SameAs(StoneType.Silcrete));
            Assert.That(DefinitionCatalogue.ByKey("animal/eastern-grey-kangaroo").Row, Is.SameAs(AnimalSpecies.EasternGreyKangaroo));
            Assert.That(DefinitionCatalogue.ByKey("plant/lomandra").Kind, Is.EqualTo(DefinitionKind.Plant));
            Assert.That(DefinitionCatalogue.Player.Key, Is.EqualTo("player"));
            Assert.That(DefinitionCatalogue.Slug("OldManBanksia"), Is.EqualTo("old-man-banksia"));
            Assert.That(DefinitionCatalogue.Slug("EasternGreyKangaroo"), Is.EqualTo("eastern-grey-kangaroo"));
            Assert.That(DefinitionCatalogue.Slug("Quartz"), Is.EqualTo("quartz"));
        }

        [Test]
        public void OnlyTheItemsAreSpawnableYetAndTheyHaveMass()
        {
            List<Definition> items = new List<Definition> { DefinitionCatalogue.Cobble, DefinitionCatalogue.Stick };
            foreach (StoneType stone in StoneType.All) items.Add(DefinitionCatalogue.CobbleOf(stone));
            foreach (StoneType stone in StoneType.All) items.Add(DefinitionCatalogue.FlakeOf(stone));
            foreach (PlantSpecies tall in StandCodes.Tall) if (Wood.Of(tall) != null) items.Add(DefinitionCatalogue.StickOf(tall));
            foreach (PlantSpecies tall in StandCodes.Tall) if (tall.StrippableBarkM > 0.0) items.Add(DefinitionCatalogue.BarkOf(tall));
            items.Add(DefinitionCatalogue.Cord);
            foreach (PlantSpecies plant in PlantSpecies.All)
            {
                if (plant.Fibre) items.Add(DefinitionCatalogue.FibreOf(plant));
                if (Tufts.ShapeOf(plant) != null) items.Add(DefinitionCatalogue.BundleOf(plant));
                if (plant.TuberKg > 0.0) items.Add(DefinitionCatalogue.TuberOf(plant));
                if (StandCodes.IsTall(plant) && Wood.Of(plant) != null) items.Add(DefinitionCatalogue.LogOf(plant));
            }
            Assert.That(DefinitionCatalogue.Spawnable, Is.EquivalentTo(items));
            foreach (Definition d in DefinitionCatalogue.Spawnable)
            {
                Assert.That(d.Kind, Is.EqualTo(DefinitionKind.Item));
                Assert.That(d.MassKg, Is.GreaterThan(0.0), d.Key);
                Assert.That(d.RadiusM, Is.GreaterThan(0.0), d.Key);
            }
            Assert.That(DefinitionCatalogue.Cobble.MassKg, Is.EqualTo(0.6));
            Assert.That(DefinitionCatalogue.Player.Spawnable, Is.False, "the body stays a session's until the verbs land");
            Assert.That(DefinitionCatalogue.ByKey("plant/blackbutt").Spawnable, Is.False, "trees become entities in M1.6");
        }

        /// <summary>A cobble taken up is of its cell's stone (M1.5b): its own key and name, the plain cobble's shape, and the stone's own weight.</summary>
        [Test]
        public void ACobbleOfEachStoneWeighsByItsDensity()
        {
            Definition silcrete = DefinitionCatalogue.CobbleOf(StoneType.Silcrete);
            Assert.That(silcrete.Key, Is.EqualTo("item/cobble-silcrete"));
            Assert.That(silcrete.DisplayName, Is.EqualTo("a silcrete cobble"));
            Assert.That(DefinitionCatalogue.CobbleOf(StoneType.Obsidian).DisplayName, Is.EqualTo("an obsidian cobble"));
            Assert.That(silcrete.Row, Is.SameAs(StoneType.Silcrete));
            Assert.That(silcrete.RadiusM, Is.EqualTo(DefinitionCatalogue.Cobble.RadiusM));
            Assert.That(silcrete.MassKg, Is.EqualTo(0.6).Within(1e-12), "silcrete is the plain cobble's own density");
            Assert.That(DefinitionCatalogue.CobbleOf(StoneType.Basalt).MassKg, Is.EqualTo(0.6 * 2900.0 / 2600.0).Within(1e-12), "basalt is heavier");
            Assert.That(DefinitionCatalogue.CobbleOf(StoneType.Sandstone).MassKg, Is.EqualTo(0.6 * 2300.0 / 2600.0).Within(1e-12), "sandstone lighter");
            Assert.That(DefinitionCatalogue.CobbleOf(null), Is.SameAs(DefinitionCatalogue.Cobble), "a cell with no stone named gives the plain cobble");
        }

        /// <summary>A flake is of its stone (FP.3): one definition for each, spawnable, with the catalogue's word for a flake's weight until a blow gives it its own.</summary>
        [Test]
        public void AFlakeIsOfItsStoneAndAStonesItemsAreKnownForIt()
        {
            Definition flake = DefinitionCatalogue.FlakeOf(StoneType.Silcrete);
            Assert.That(flake.Key, Is.EqualTo("item/flake-silcrete"));
            Assert.That(flake.DisplayName, Is.EqualTo("a silcrete flake"));
            Assert.That(DefinitionCatalogue.FlakeOf(StoneType.Obsidian).DisplayName, Is.EqualTo("an obsidian flake"));
            Assert.That(flake.Row, Is.SameAs(StoneType.Silcrete));
            Assert.That(flake.Spawnable, Is.True);
            Assert.That(flake.MassKg, Is.EqualTo(0.02), "twenty grams, the catalogue's word for a flake with no mass of its own");
            Assert.That(flake.RadiusM, Is.EqualTo(0.03));
            Assert.That(DefinitionCatalogue.IsFlake(flake), Is.True);
            Assert.That(DefinitionCatalogue.IsFlake(DefinitionCatalogue.CobbleOf(StoneType.Silcrete)), Is.False, "a cobble of the same stone is no flake");
            Assert.That(DefinitionCatalogue.IsFlake(DefinitionCatalogue.Stick), Is.False);
            Assert.That(DefinitionCatalogue.IsFlake(null), Is.False);
            Assert.That(DefinitionCatalogue.StoneOf(flake), Is.SameAs(StoneType.Silcrete));
            Assert.That(DefinitionCatalogue.StoneOf(DefinitionCatalogue.CobbleOf(StoneType.Rhyolite)), Is.SameAs(StoneType.Rhyolite));
            Assert.That(DefinitionCatalogue.StoneOf(DefinitionCatalogue.Cobble), Is.Null, "the plain cobble is of no stone the country names");
            Assert.That(DefinitionCatalogue.StoneOf(DefinitionCatalogue.Stick), Is.Null);
            Assert.That(DefinitionCatalogue.StoneOf(DefinitionCatalogue.ByKey("stone/silcrete")), Is.Null, "the stone's own table row is no item");
            Assert.Throws<KeyNotFoundException>(() => DefinitionCatalogue.FlakeOf(null), "a flake is always of a stone");
        }

        [Test]
        public void TheKeyFormIsEnforced()
        {
            Assert.That(DefinitionCatalogue.IsValidKey("item/cobble"), Is.True);
            Assert.That(DefinitionCatalogue.IsValidKey("animal/eastern-grey-kangaroo"), Is.True);
            Assert.That(DefinitionCatalogue.IsValidKey("player"), Is.True);
            Assert.That(DefinitionCatalogue.IsValidKey("Item/cobble"), Is.False, "upper case");
            Assert.That(DefinitionCatalogue.IsValidKey("item/"), Is.False, "empty slug");
            Assert.That(DefinitionCatalogue.IsValidKey("item/co--bble"), Is.False, "double hyphen");
            Assert.That(DefinitionCatalogue.IsValidKey("item/cobble/x"), Is.False, "two slashes");
            Assert.That(DefinitionCatalogue.IsValidKey("item/-cobble"), Is.False, "leading hyphen");
            Assert.That(DefinitionCatalogue.IsValidKey("cobble"), Is.False, "no kind");
            Assert.That(DefinitionCatalogue.TryByKey("item/nothing", out _), Is.False);
        }

        [Test]
        public void TheHashIsFnv1a32()
        {
            Assert.That(DefinitionId.Fnv1a32(""), Is.EqualTo(2166136261u));
            Assert.That(DefinitionId.Fnv1a32("a"), Is.EqualTo(0xe40c292cu));
            Assert.That(new DefinitionId(0xe40c292cu).ToString(), Is.EqualTo("e40c292c"));
        }
    }
}
