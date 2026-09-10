using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.World
{
    /// <summary>
    /// What the world says covers a square metre (M1.4d promise 1): the cascade's order where two answers could
    /// claim a cell, and the byte that carries the answer and how wet the ground is.
    /// </summary>
    public sealed class GroundCoverTests
    {
        private const double Deep = 1.0;

        private static GroundCover Of(WaterClass water, Topology topology, PlantSpecies understory, PlantSpecies overstory, double soilM = Deep)
            => GroundCovers.Of(water, (uint)topology, understory, overstory, soilM);

        [Test]
        public void EveryBranchOfTheCascadeIsReachable()
        {
            Assert.That(Of(WaterClass.Sea, Topology.Sea, null, null), Is.EqualTo(GroundCover.Sea));
            Assert.That(Of(WaterClass.Lake, Topology.Lake, null, null), Is.EqualTo(GroundCover.FreshWater));
            Assert.That(Of(WaterClass.Creek, Topology.Creek, null, null), Is.EqualTo(GroundCover.FreshWater));
            Assert.That(Of(WaterClass.Swamp, Topology.Wetland, PlantSpecies.SawSedge, null), Is.EqualTo(GroundCover.SwampFloor));
            Assert.That(Of(WaterClass.Dry, Topology.Beach, null, null), Is.EqualTo(GroundCover.Sand));
            Assert.That(Of(WaterClass.Dry, Topology.Dune, PlantSpecies.Spinifex, null), Is.EqualTo(GroundCover.DuneSand));
            Assert.That(Of(WaterClass.Dry, Topology.Cliff, null, null), Is.EqualTo(GroundCover.Rock));
            Assert.That(Of(WaterClass.Dry, Topology.ShorePlatform, null, null), Is.EqualTo(GroundCover.Rock));
            Assert.That(Of(WaterClass.Dry, Topology.Heath, PlantSpecies.HeathBanksia, null), Is.EqualTo(GroundCover.Heath));
            Assert.That(Of(WaterClass.Dry, Topology.None, PlantSpecies.Bracken, null), Is.EqualTo(GroundCover.Bracken));
            Assert.That(Of(WaterClass.Dry, Topology.None, PlantSpecies.Lomandra, null), Is.EqualTo(GroundCover.Sedge));
            Assert.That(Of(WaterClass.Dry, Topology.None, PlantSpecies.KangarooGrass, null), Is.EqualTo(GroundCover.Grass));
            Assert.That(Of(WaterClass.Dry, Topology.Forest, null, PlantSpecies.Blackbutt), Is.EqualTo(GroundCover.ForestFloor));
            Assert.That(Of(WaterClass.Dry, Topology.None, null, null), Is.EqualTo(GroundCover.BareEarth));
        }

        /// <summary>Ground too thin to hold anything is rock, whatever a plant layer claims grows on it.</summary>
        [Test]
        public void SoilTooThinToHoldAnythingIsRock()
        {
            Assert.That(Of(WaterClass.Dry, Topology.Forest, PlantSpecies.Lomandra, PlantSpecies.Blackbutt, 0.01),
                Is.EqualTo(GroundCover.Rock));
            Assert.That(Of(WaterClass.Dry, Topology.Forest, PlantSpecies.Lomandra, PlantSpecies.Blackbutt, GroundCovers.BareSoilM + 0.01),
                Is.EqualTo(GroundCover.Sedge), "just enough soil and the sedge on it is what you see");
        }

        /// <summary>
        /// The order matters where two could claim a cell, and each of these pairs happens in the Bherwerre
        /// world: a beach with grass tussocks on it, a swamp under paperbark, a dune with spinifex.
        /// </summary>
        [Test]
        public void TheOrderIsSandAndWaterBeforeWhatGrowsOnThem()
        {
            Assert.That(Of(WaterClass.Dry, Topology.Beach, PlantSpecies.Spinifex, null), Is.EqualTo(GroundCover.Sand),
                "sand with tussocks on it is still a beach");
            Assert.That(Of(WaterClass.Swamp, Topology.Wetland | Topology.Forest, PlantSpecies.SawSedge, PlantSpecies.SwampPaperbark),
                Is.EqualTo(GroundCover.SwampFloor), "the wetland is under the paperbark, not beside it");
            Assert.That(Of(WaterClass.Sea, Topology.Sea | Topology.Beach, null, null), Is.EqualTo(GroundCover.Sea),
                "water first: what is under it does not show");
        }

        /// <summary>
        /// The understory is asked before the canopy, because what you stand on under an open banksia is the
        /// ground layer. Reversing them painted a third of the peninsula as one colour (the pricing of
        /// 2026-09-10); the litter colour is for ground with nothing growing on it.
        /// </summary>
        [Test]
        public void WhatGrowsUnderfootBeatsWhatGrowsOverhead()
        {
            Assert.That(Of(WaterClass.Dry, Topology.Forest, PlantSpecies.Bracken, PlantSpecies.Blackbutt),
                Is.EqualTo(GroundCover.Bracken));
            Assert.That(Of(WaterClass.Dry, Topology.Forest, null, PlantSpecies.CoastBanksia),
                Is.EqualTo(GroundCover.ForestFloor), "nothing growing under it, so what shows is the litter");
        }

        /// <summary>
        /// The dune is asked what grows on it before it is called sand (M1.2b, 2026-09-10): the other way round,
        /// every dune on the peninsula read as bare sand, where the park says the dunes are held by what grows on
        /// them. Sand shows only where nothing grows, or only the sand-binder, whose runners leave it showing.
        /// </summary>
        [Test]
        public void OnTheDuneWhatGrowsIsWhatShows()
        {
            Assert.That(Of(WaterClass.Dry, Topology.Dune, PlantSpecies.Lomandra, PlantSpecies.CoastBanksia), Is.EqualTo(GroundCover.Sedge));
            Assert.That(Of(WaterClass.Dry, Topology.Dune | Topology.Heath, PlantSpecies.HeathBanksia, null), Is.EqualTo(GroundCover.Heath));
            Assert.That(Of(WaterClass.Dry, Topology.Dune, PlantSpecies.KangarooGrass, null), Is.EqualTo(GroundCover.Grass));
            Assert.That(Of(WaterClass.Dry, Topology.Dune, null, PlantSpecies.CoastBanksia), Is.EqualTo(GroundCover.ForestFloor),
                "a banksia with nothing under it drops its litter on the dune as anywhere");
            Assert.That(Of(WaterClass.Dry, Topology.Dune, null, null), Is.EqualTo(GroundCover.DuneSand), "where nothing grows the dune is sand");
            Assert.That(Of(WaterClass.Dry, Topology.Dune, PlantSpecies.Spinifex, null), Is.EqualTo(GroundCover.DuneSand),
                "and where only the sand-binder grows, the sand shows through it");
            Assert.That(Of(WaterClass.Dry, Topology.Dune | Topology.Cliff, null, null), Is.EqualTo(GroundCover.DuneSand),
                "a dune's steep face is still sand: the dune is asked before the cliff");
            Assert.That(Of(WaterClass.Dry, Topology.None, PlantSpecies.Spinifex, null), Is.EqualTo(GroundCover.BareEarth),
                "off the dune a pioneer leaves its ground showing too");
            Assert.That(Of(WaterClass.Dry, Topology.Beach, PlantSpecies.Lomandra, null), Is.EqualTo(GroundCover.Sand),
                "and the beach is sand whatever a layer claims grows on it");
        }

        [Test]
        public void ACoverAndAQuarterGoInAndComeOut()
        {
            foreach (GroundCover cover in GroundCovers.All)
                for (int quarter = 0; quarter < GroundCovers.Quarters; quarter++)
                {
                    byte code = GroundCovers.Pack(cover, quarter);
                    Assert.That(GroundCovers.CoverOf(code), Is.EqualTo(cover));
                    Assert.That(GroundCovers.QuarterOf(code), Is.EqualTo(quarter));
                }
        }

        [Test]
        public void TheQuartersSplitTheLandsOwnWetnessAtTheQuarters()
        {
            Assert.That(GroundCovers.QuarterFor(0.0), Is.EqualTo(0));
            Assert.That(GroundCovers.QuarterFor(0.24), Is.EqualTo(0));
            Assert.That(GroundCovers.QuarterFor(0.25), Is.EqualTo(1));
            Assert.That(GroundCovers.QuarterFor(0.75), Is.EqualTo(3));
            Assert.That(GroundCovers.QuarterFor(1.0), Is.EqualTo(3), "the top of the range is still the top quarter");
            Assert.That(GroundCovers.QuarterFor(2.0), Is.EqualTo(3), "and nothing above it reaches for a fifth");
            Assert.That(GroundCovers.QuarterFor(double.NaN), Is.EqualTo(0));
        }

        /// <summary>Every cover has a name, because the verifier and the census print them, not their numbers.</summary>
        [Test]
        public void EveryCoverIsNamed()
        {
            foreach (GroundCover cover in GroundCovers.All)
                Assert.That(GroundCovers.NameOf(cover), Is.Not.Null.And.Not.Empty);
            Assert.That(GroundCovers.NameOf((GroundCover)62), Is.EqualTo(GroundCovers.NameOf(GroundCover.Unknown)),
                "a cover a later version invents reads as unknown rather than as nothing at all");
        }
    }
}
