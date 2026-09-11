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
            Assert.That(DefinitionCatalogue.All.Count, Is.EqualTo(1 + PlantSpecies.All.Count + StoneType.All.Count + AnimalSpecies.All.Count + 2 + StoneType.All.Count),
                "the player, the tables, the plain cobble and the stick, and a cobble of every stone");
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
