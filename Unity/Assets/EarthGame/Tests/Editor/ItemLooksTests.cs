using System.Collections.Generic;
using EarthGame.Client;
using EarthGame.ClientCore;
using EarthGame.Engine;
using NUnit.Framework;
using UnityEngine;

namespace EarthGame.Tests.Editor
{
    /// <summary>
    /// Every spawnable definition has a look, and it is the stand's own (M1.5a promise 7): a stick put down is the mesh of
    /// a stick lying in the litter, and a cobble is drawn at the item's own size, which is the size the litter's are.
    /// </summary>
    public sealed class ItemLooksTests
    {
        [Test]
        public void EverySpawnableDefinitionHasALookFromTheStandsMeshes()
        {
            List<string> wrong = ItemLooks.Audit();
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));

            Assert.That(ItemLooks.TryLook(DefinitionCatalogue.Stick, 7, out Mesh stick, out float stickScale), Is.True);
            Assert.That(stick, Is.SameAs(StandMeshes.Stick(7 % StandPreparation.Variants)), "the stand's own stick");
            Assert.That(stickScale, Is.EqualTo(1f));
            Assert.That(stick.bounds.size.x, Is.InRange(0.6f, 1.3f), "about a metre long");
            Assert.That(stick.bounds.min.y, Is.EqualTo(0f).Within(0.01f), "lying on the ground, not in it");

            Assert.That(ItemLooks.TryLook(DefinitionCatalogue.Cobble, 8, out Mesh cobble, out float cobbleScale), Is.True);
            Assert.That(cobble, Is.SameAs(StandMeshes.Cobble(8 % StandPreparation.Variants)));
            Assert.That(cobbleScale, Is.EqualTo((float)(2.0 * DefinitionCatalogue.Cobble.RadiusM)).Within(1e-6f), "the item's own diameter");
            Assert.That(cobbleScale, Is.EqualTo(StandViews.CobbleSizeM), "which is the size the litter's cobbles are drawn at");

            Assert.That(ItemLooks.TryLook(DefinitionCatalogue.CobbleOf(StoneType.Silcrete), 8, out Mesh silcrete, out float silcreteScale), Is.True);
            Assert.That(silcrete, Is.SameAs(cobble), "a stone's cobble is the cobble's shape (M1.5b)");
            Assert.That(silcreteScale, Is.EqualTo(cobbleScale));

            Assert.That(ItemLooks.TryLook(DefinitionCatalogue.Player, 1, out _, out _), Is.False, "a founder is no item");
        }
    }
}
