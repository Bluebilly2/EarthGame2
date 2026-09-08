using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// One Unity Terrain tile from the region's heightfield: a kilometre square at 1025 posts, so a post every
    /// 0.977 m, sampled bilinearly from the 4 m raster (procedural detail below the raster's resolution comes
    /// later, ARCHITECTURE §3). The tile's origin is its south-west corner in local metres; Unity's +X is east
    /// and +Z is north, and the heightmap array is indexed [north, east]. The Terrain's collider is the client's
    /// ground (PhysX interpolates between these posts; the server samples the raster: the named defect class).
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

        /// <summary>Builds the tile's TerrainData: heights normalised over the tile's own range, so a flat tile is not a division by zero.</summary>
        public static TerrainData BuildData(Heightfield heightfield, double originEast, double originNorth, TerrainLayer layer, out float baseM)
        {
            float[,] posts = SamplePosts(heightfield, originEast, originNorth, Posts, PostSpacingM, out float minM, out float maxM);
            float range = Mathf.Max(1f, maxM - minM);
            baseM = minM;
            float[,] normalised = new float[Posts, Posts];
            for (int z = 0; z < Posts; z++)
                for (int x = 0; x < Posts; x++)
                    normalised[z, x] = (posts[z, x] - minM) / range;
            TerrainData data = new TerrainData();
            data.heightmapResolution = Posts;
            data.size = new Vector3(TileSizeM, range, TileSizeM);
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

        /// <summary>Builds and places the tile in the scene. The returned Terrain owns its data; destroy the GameObject to free both.</summary>
        public static Terrain Build(Heightfield heightfield, double originEast, double originNorth, Material material, TerrainLayer layer, string name)
        {
            TerrainData data = BuildData(heightfield, originEast, originNorth, layer, out float baseM);
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = name;
            go.layer = Layers.Terrain;
            go.transform.position = new Vector3((float)originEast, baseM, (float)originNorth);
            Terrain terrain = go.GetComponent<Terrain>();
            if (material != null) terrain.materialTemplate = material;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4f;
            // At the default distance the tile drew its low-resolution base map everywhere, even at the feet, and
            // the ground came out as one flat colour (frames of 2026-09-08); the splat pass is wanted at every
            // distance a tile is drawn, so the base map is pushed beyond the far clip.
            terrain.basemapDistance = 20000f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return terrain;
        }
    }
}
