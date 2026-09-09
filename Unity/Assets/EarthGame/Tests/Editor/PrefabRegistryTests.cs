using System.Collections.Generic;
using EarthGame.Client;
using EarthGame.Engine;
using NUnit.Framework;
using UnityEngine;

namespace EarthGame.Tests.Editor
{
    /// <summary>
    /// The registry and the catalogue agree (M1.3 promise 7): every spawnable definition has exactly one prefab
    /// binding and every binding names a spawnable definition with a prefab. A definition made spawnable in the
    /// engine without a prefab here is red in the editor, never a missing object in a world.
    /// </summary>
    public sealed class PrefabRegistryTests
    {
        [Test]
        public void EverySpawnableDefinitionIsBoundOnceAndEveryBindingNamesOne()
        {
            PrefabRegistry registry = Resources.Load<PrefabRegistry>(PrefabRegistry.ResourcePath);
            Assert.That(registry, Is.Not.Null, "Resources/" + PrefabRegistry.ResourcePath + ".asset");
            List<string> wrong = registry.Audit();
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
            Assert.That(registry.Bindings.Count, Is.EqualTo(DefinitionCatalogue.Spawnable.Count));
            foreach (Definition d in DefinitionCatalogue.Spawnable)
            {
                GameObject prefab = registry.PrefabFor(d.Key);
                Assert.That(prefab, Is.Not.Null, d.Key);
                Assert.That(prefab.GetComponent<Renderer>(), Is.Not.Null, d.Key + " draws nothing");
            }
        }
    }
}
