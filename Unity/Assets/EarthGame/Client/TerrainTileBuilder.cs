using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// Unity Terrain from the region's heightfields. The near tile is a kilometre square at 1025 posts, so a post
    /// every 0.977 m, sampled bilinearly from the 4 m raster (procedural detail below the raster's resolution comes
    /// later, ARCHITECTURE §3); the same builder makes the coarse layers under and beyond it, the whole region at
    /// 7.8 m posts and the 64 km surround at 62.5 m, with a hole cut where a finer layer sits on top. A tile's
    /// origin is its south-west corner in local metres; Unity's +X is east and +Z is north, and the heightmap
    /// array is indexed [north, east]. The near tile's collider is the client's ground (PhysX interpolates between
    /// these posts; the server samples the raster: the named defect class); the coarse layers do not collide.
    /// </summary>
    public static class TerrainTileBuilder
    {
        public const int Posts = 1025;
        public const float TileSizeM = 1000f;
        public static float PostSpacingM => TileSizeM / (Posts - 1);

        /// <summary>The posts of a tile whose south-west corner is at (originEast, originNorth), as [north, east].</summary>
        public static float[,] SamplePosts(Heightfield heightfield, double originEast, double originNorth, int posts, double spacing, out float minM, out float maxM)
        {
            float[,] heights = new float[posts, posts];
            minM = float.MaxValue;
            maxM = float.MinValue;
            for (int z = 0; z < posts; z++)
            {
                double north = originNorth + z * spacing;
                for (int x = 0; x < posts; x++)
                {
                    float h = (float)heightfield.HeightAt(originEast + x * spacing, north);
                    heights[z, x] = h;
                    if (h < minM) minM = h;
                    if (h > maxM) maxM = h;
                }
            }
            return heights;
        }

        /// <summary>Builds a tile's TerrainData: heights normalised over the tile's own range, so a flat tile is not a division by zero.</summary>
        public static TerrainData BuildData(Heightfield heightfield, double originEast, double originNorth, float sizeM, int posts, TerrainLayer layer, out float baseM)
        {
            float spacing = sizeM / (posts - 1);
            float[,] sampled = SamplePosts(heightfield, originEast, originNorth, posts, spacing, out float minM, out float maxM);
            float range = Mathf.Max(1f, maxM - minM);
            baseM = minM;
            float[,] normalised = new float[posts, posts];
            for (int z = 0; z < posts; z++)
                for (int x = 0; x < posts; x++)
                    normalised[z, x] = (sampled[z, x] - minM) / range;
            TerrainData data = new TerrainData();
            data.heightmapResolution = posts;
            data.size = new Vector3(sizeM, range, sizeM);
            data.SetHeights(0, 0, normalised);
            if (layer != null)
            {
                data.terrainLayers = new[] { layer };
                // A fresh TerrainData gives its one layer no weight anywhere; the ground rendered as a flat
                // colour until the weight map was filled by hand (frames of 2026-09-08).
                data.alphamapResolution = 64;
                float[,,] weights = new float[data.alphamapResolution, data.alphamapResolution, 1];
                for (int z = 0; z < data.alphamapResolution; z++)
                    for (int x = 0; x < data.alphamapResolution; x++)
                        weights[z, x, 0] = 1f;
                data.SetAlphamaps(0, 0, weights);
            }
            return data;
        }

        /// <summary>The kilometre tile at full detail, as before.</summary>
        public static TerrainData BuildData(Heightfield heightfield, double originEast, double originNorth, TerrainLayer layer, out float baseM)
            => BuildData(heightfield, originEast, originNorth, TileSizeM, Posts, layer, out baseM);

        /// <summary>Builds and places the near tile in the scene: collidable, full detail.</summary>
        public static Terrain Build(Heightfield heightfield, double originEast, double originNorth, Material material, TerrainLayer layer, string name)
            => Build(heightfield, originEast, originNorth, TileSizeM, Posts, material, layer, name, true, 0f);

        /// <summary>
        /// Builds and places a tile of any size. Coarse layers are sunk by <paramref name="sinkM"/> so a finer
        /// layer above them never fights their surface, and have their collider removed: the founder walks on the
        /// near tile only. The returned Terrain owns its data; destroy the GameObject to free both.
        /// </summary>
        public static Terrain Build(Heightfield heightfield, double originEast, double originNorth, float sizeM, int posts,
                                    Material material, TerrainLayer layer, string name, bool collidable, float sinkM)
        {
            TerrainData data = BuildData(heightfield, originEast, originNorth, sizeM, posts, layer, out float baseM);
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = name;
            go.layer = Layers.Terrain;
            go.transform.position = new Vector3((float)originEast, baseM - sinkM, (float)originNorth);
            Terrain terrain = go.GetComponent<Terrain>();
            if (material != null) terrain.materialTemplate = material;
            // Not instanced. In the built player the instanced terrain first drew nothing (its instancing variants
            // were stripped) and, with the variants kept, drew without the sun: a probe sphere beside it was lit
            // and the ground stayed sky-blue (frames of 2026-09-08). The non-instanced path draws lit. The cost
            // of that choice is measured when terrain is profiled (M1.4), not assumed.
            terrain.drawInstanced = false;
            terrain.heightmapPixelError = collidable ? 4f : 8f;
            // At the default distance the tile drew its low-resolution base map everywhere, even at the feet, and
            // the ground came out as one flat colour (frames of 2026-09-08); the splat pass is wanted at every
            // distance a tile is drawn, so the base map is pushed to the largest distance Unity accepts. Beyond
            // that ceiling (100 km was tried the same day) the terrain draws nothing at all.
            terrain.basemapDistance = 20000f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (!collidable)
            {
                TerrainCollider collider = go.GetComponent<TerrainCollider>();
                if (collider != null) Object.Destroy(collider);
            }
            return terrain;
        }

        /// <summary>
        /// Cuts a hole in a coarse tile where a finer tile sits on top of it, so the two never show through each
        /// other. The rectangle is in local metres; cells outside the tile are ignored.
        /// </summary>
        public static void CutHole(Terrain coarse, double originEast, double originNorth, double eastM, double northM, double sizeM)
        {
            TerrainData data = coarse.terrainData;
            int res = data.holesResolution;
            double tileSize = data.size.x;
            // Rounded inward: the finer tile overlaps the coarse one by less than a hole cell all round, so no
            // crack opens between them, and the coarse tile is sunk so the overlap never shows through.
            int x0 = Mathf.Clamp((int)System.Math.Ceiling((eastM - originEast) / tileSize * res), 0, res);
            int x1 = Mathf.Clamp((int)System.Math.Floor((eastM + sizeM - originEast) / tileSize * res), 0, res);
            int z0 = Mathf.Clamp((int)System.Math.Ceiling((northM - originNorth) / tileSize * res), 0, res);
            int z1 = Mathf.Clamp((int)System.Math.Floor((northM + sizeM - originNorth) / tileSize * res), 0, res);
            if (x1 <= x0 || z1 <= z0) return;
            bool[,] holes = new bool[z1 - z0, x1 - x0];
            data.SetHoles(x0, z0, holes);
        }
    }
}
