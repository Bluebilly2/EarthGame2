using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime;
using System.Text;
using EarthGame.Engine;

namespace EarthGame.Server
{
    /// <summary>
    /// The world-creation pipeline (M1.2 promise 9): the chain run once from the baked heights and the seed,
    /// every layer written under <c>&lt;world&gt;/layers/</c> in the raster format's second version, the wake chosen
    /// by the scorer and the census written beside them. The server is the world folder's only writer
    /// (ARCHITECTURE §6); this is the first thing it writes.
    /// </summary>
    public static class WorldCreation
    {
        public const string LayersFolder = "layers";
        public const string CensusFile = "census.txt";
        /// <summary>Distances are stored as whole metres in u16; anything beyond this is "none within reach".</summary>
        public const int DistanceCapM = 65535;

        /// <summary>The name of a kind's capacity layer in a world folder: what creation writes (M1.2) and the loader reads (M1.7a).</summary>
        public static string CapacityLayer(AnimalSpecies species) => "capacity_" + species.Name.ToLowerInvariant();

        /// <summary>What creation produced, for the host to log and the save to record.</summary>
        public sealed class Result
        {
            public string Dir;
            public WakeScore Wake;
            public string Census;
            /// <summary>Layer name → the sidecar path written.</summary>
            public Dictionary<string, string> Layers = new Dictionary<string, string>(StringComparer.Ordinal);
            /// <summary>Layer name → the raw file's sha256, as its sidecar states.</summary>
            public Dictionary<string, string> Checksums = new Dictionary<string, string>(StringComparer.Ordinal);
            public WorldLayers Computed;
        }

        /// <summary>
        /// Runs the chain and writes the folder. The date is the host's; the engine reads no clock. The water bodies
        /// are the bake's mapped outlines when the region has them (<c>water_bodies.json</c> beside the heights).
        /// </summary>
        public static Result Create(string worldDir, Region region, ulong seed, RegionRaster heights, string nowUtcText, RegionRaster waterBodies = null, Action<string> progress = null,
                                    IMakingWatcher watcher = null)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            SavedWorldLayers.CheckRegion(heights, region);
            // Each stage of the making lets go of the last one's scratch before it asks for its own (WG.2b, 2026-09-23): a 16 km world
            // was made at a peak of 5.2 to 5.8 GB committed of which 2.9 were in use when the wake was chosen, and 1.7 once collected.
            Action<string> stage = s => { Settle(); progress?.Invoke(s); };
            WorldLayers layers = WorldLayers.Compute(heights, seed, waterBodies, stage, null, watcher);
            stage("Choosing the wake");
            WakeScorer scorer = new WakeScorer(layers);
            WakeScore wake = scorer.Best();
            watcher?.WakeChosen(wake.East, wake.North);
            string census = scorer.Census(wake) + layers.WaterCensus();

            string dir = Path.Combine(worldDir, LayersFolder);
            Directory.CreateDirectory(dir);
            string by = "EarthGame.Server WorldCreation (seed " + seed + ")";
            Result result = new Result { Dir = dir, Wake = wake, Census = census, Computed = layers };
            SaveLayer("heights", RegionRaster.Write(dir, "heights", heights, "heights", "f32", 1.0, "m", layers.HeightsWithFloor,
                "the bake's heights with the sea floor of WorldLayers.SeaFloor", by, nowUtcText));
            SaveLayer("surface", RegionRaster.Write(dir, "surface", heights, "surface", "f32", 1.0, "m", layers.Surface,
                "the water's surface where water stands (the sea at the datum, a lake at its level), else the ground's", by, nowUtcText));
            SaveLayer("soil_depth", RegionRaster.Write(dir, "soil_depth", heights, "soil_depth", "u16", 0.01, "m", layers.Soil.DepthM,
                "SoilModel over DrainageNetwork", by, nowUtcText));
            SaveLayer("wetness", RegionRaster.Write(dir, "wetness", heights, "wetness", "u8", 1.0 / 255.0, "1", layers.Soil.Wetness01,
                "SoilModel topographic wetness, scaled to the land's own percentiles", by, nowUtcText));
            SaveLayer("suitability", RegionRaster.Write(dir, "suitability", heights, "suitability", "u8", 1.0 / 255.0, "1", layers.Suitability,
                "the winning canopy's suitability (PlantCommunity)", by, nowUtcText));
            SaveLayer("water", RegionRaster.WriteCodes(dir, "water", heights, "water", "u8", "id", Of(layers.Water),
                "WaterClass: 0 dry, 1 damp, 2 trickle, 3 creek, 4 stream, 5 lake (fresh), 6 swamp, 7 sea (salt)", by, nowUtcText));
            SaveLayer("overstory", RegionRaster.WriteCodes(dir, "overstory", heights, "overstory", "u8", "id", Of(layers.Overstory),
                "PlantSpecies.All index + 1, 0 for none: " + SpeciesList(), by, nowUtcText));
            SaveLayer("understory", RegionRaster.WriteCodes(dir, "understory", heights, "understory", "u8", "id", Of(layers.Understory),
                "PlantSpecies.All index + 1, 0 for none: " + SpeciesList(), by, nowUtcText));
            SaveLayer("topology", RegionRaster.WriteCodes(dir, "topology", heights, "topology", "u32", "flags", layers.TopologyMask,
                "Topology bits: 1 sea, 2 beach, 4 dune, 8 wetland, 16 forest, 32 heath, 64 crest, 128 cliff, 256 shore platform, 512 lake, 1024 creek", by, nowUtcText));
            SaveLayer("cover", RegionRaster.WriteCodes(dir, "cover", heights, "cover", "u8", "id", Of(layers.Cover),
                GroundCovers.Legend(), by, nowUtcText));
            SaveLayer("stand", RegionRaster.WriteCodes(dir, "stand", heights, "stand", "u16", "id", Of(layers.Stand),
                StandCodes.Legend(), by, nowUtcText));
            SaveLayer("loose", RegionRaster.WriteCodes(dir, "loose", heights, "loose", "u8", "counts", Of(layers.Loose),
                LooseCodes.Legend(), by, nowUtcText));
            SaveLayer("stone", RegionRaster.WriteCodes(dir, "stone", heights, "stone", "u8", "id", Of(layers.Stone),
                "StoneType.All index + 1, 0 for none: " + StoneList(), by, nowUtcText));
            int width = layers.Width;
            SaveLayer("catchment", RegionRaster.WriteCodes(dir, "catchment", heights, "catchment", "u32", "cells",
                i => (uint)Math.Round(layers.Drainage.CatchmentM2(i % width, i / width) / layers.Drainage.CellAreaM2),
                "DrainageNetwork: the cells draining through each cell, itself included, on the filled surface of the ground with each open lake's and dam's way out cut, the sea and the lakes that hold their water its sinks", by, nowUtcText));
            SaveLayer("shore_distance", RegionRaster.WriteCodes(dir, "shore_distance", heights, "shore_distance", "u16", "m", MetresOf(layers.ShoreDistanceM),
                "metres to the nearest sea cell, capped at 65535", by, nowUtcText));
            SaveLayer("fresh_water_distance", RegionRaster.WriteCodes(dir, "fresh_water_distance", heights, "fresh_water_distance", "u16", "m", MetresOf(layers.FreshWaterDistanceM),
                "metres to the nearest creek, stream or lake cell; 65535 for none", by, nowUtcText));
            IReadOnlyList<AnimalSpecies> species = AnimalSpecies.All;
            for (int s = 0; s < species.Count; s++)
            {
                string name = CapacityLayer(species[s]);
                SaveLayer(name, RegionRaster.Write(dir, name, heights, name, "u16", 0.01, CapacitySquares.Unit, layers.Capacity[s],
                    "AnimalCapacity.PerKm2 for " + species[s].DisplayName, by, nowUtcText));
            }
            SaveLayer("stone_distance", RegionRaster.WriteCodes(dir, "stone_distance", heights, "stone_distance", "u16", "m", MetresOf(scorer.StoneDistanceM),
                "metres to the nearest knappable stone (Knappability at or above WakeScorer.KnappableFloor); 65535 for none", by, nowUtcText));
            SaveLayer("fibre_distance", RegionRaster.WriteCodes(dir, "fibre_distance", heights, "fibre_distance", "u16", "m", MetresOf(scorer.FibreDistanceM),
                "metres to the nearest fibre plant (lomandra, saw-sedge, spinifex); 65535 for none", by, nowUtcText));
            SaveLayer("firewood_distance", RegionRaster.WriteCodes(dir, "firewood_distance", heights, "firewood_distance", "u16", "m", MetresOf(scorer.FirewoodDistanceM),
                "metres to the nearest canopy; 65535 for none", by, nowUtcText));
            SaveLayer("shelter_distance", RegionRaster.WriteCodes(dir, "shelter_distance", heights, "shelter_distance", "u16", "m", MetresOf(scorer.ShelterDistanceM),
                "metres to the nearest cliff or shore platform; 65535 for none", by, nowUtcText));
            SaveLayer("wake_score", RegionRaster.Write(dir, "wake_score", heights, "wake_score", "u8", 1.0 / 255.0, "1", scorer.ScoreField(),
                "WakeScorer: the product of the five graded criteria on standable ground; the wake is its greatest cell, first in row order", by, nowUtcText));

            progress?.Invoke("Writing the census");
            string censusPath = Path.Combine(worldDir, CensusFile);
            File.WriteAllText(censusPath + ".part", census, new UTF8Encoding(false));
            if (File.Exists(censusPath)) File.Delete(censusPath);
            File.Move(censusPath + ".part", censusPath);
            return result;

            void SaveLayer(string name, string sidecarPath)
            {
                Put(result, name, sidecarPath);
                progress?.Invoke("Saved " + name);
            }
        }

        private static void Put(Result result, string name, string sidecarPath)
        {
            result.Layers[name] = sidecarPath;
            result.Checksums[name] = RegionRaster.CheckedSha256(sidecarPath);
        }

        /// <summary>A byte layer's cells as codes, read where they lie rather than widened into a copy (WG.2b).</summary>
        private static Func<int, uint> Of(byte[] bytes) => i => bytes[i];

        /// <summary>A two-byte layer's cells as codes, the same way (the stand since WG.2c).</summary>
        private static Func<int, uint> Of(ushort[] codes) => i => codes[i];

        /// <summary>Distances in metres as u16 codes, capped at <see cref="DistanceCapM"/>, a cell at a time (WG.2b).</summary>
        private static Func<int, uint> MetresOf(float[] distances) => i =>
        {
            float d = distances[i];
            return float.IsInfinity(d) || float.IsNaN(d) || d >= DistanceCapM ? (uint)DistanceCapM : (uint)Math.Round(d);
        };

        /// <summary>
        /// A full collection, the large objects compacted, between the making's stages (WG.2b, 2026-09-23). Each stage leaves arrays
        /// of a whole layer behind it; left to the collector's own pace they piled up to nearly twice what was in use (a 16 km world
        /// at 5.2 GB committed with 2.9 in use, 3.5 when the collector was told to spend less), and a 32 km world would have needed
        /// most of the machine. A collection costs a fraction of a second against a stage's seconds.
        /// </summary>
        internal static void Settle()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }

        private static string SpeciesList()
        {
            StringBuilder sb = new StringBuilder();
            IReadOnlyList<PlantSpecies> all = PlantSpecies.All;
            for (int i = 0; i < all.Count; i++) sb.Append(i > 0 ? ", " : "").Append(i + 1).Append('=').Append(all[i].Name);
            return sb.ToString();
        }

        private static string StoneList()
        {
            StringBuilder sb = new StringBuilder();
            IReadOnlyList<StoneType> all = StoneType.All;
            for (int i = 0; i < all.Count; i++) sb.Append(i > 0 ? ", " : "").Append(i + 1).Append('=').Append(all[i].Name);
            return sb.ToString();
        }

        // A world's terrain is opened by WorldPreparation.Load and nowhere else (M1.4 loading, 2026-09-10).
        // The lenient reader that used to live here returned null for a folder whose heights were missing OR
        // unreadable, and both of its callers then reached for the region's bake: a corrupt layer was answered
        // by silently standing the world on other ground. SourceRulesTests keeps that path from growing back.
    }
}
