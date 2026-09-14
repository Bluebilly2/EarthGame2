using EarthGame.ClientCore;
using EarthGame.Engine;
using EarthGame.Shared;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// Unity Terrain from a height source: the streamed tiles (a kilometre square at 513 posts, so a post every
    /// 1.95 m, sampled bilinearly from the tile's 4 m posts; procedural detail below that comes later,
    /// ARCHITECTURE §3), and the bake on disk for the coarse layers under and beyond them, the whole region at
    /// 7.8 m posts and the 64 km surround at 62.5 m, with a hole cut where a finer layer sits on top. A tile's
    /// origin is its south-west corner in local metres; Unity's +X is east and +Z is north, and the heightmap
    /// array is indexed [north, east]. The tiles' colliders are the client's ground (PhysX interpolates between
    /// these posts; the server samples the raster: the named defect class); the skirt does not collide.
    /// </summary>
    public static class TerrainTileBuilder
    {
        /// <summary>
        /// Posts along a streamed tile's Terrain (2^n + 1, as Unity requires): 1.95 m over a kilometre, two for
        /// every 4 m post the wire carried, which is the rule the colour map follows too. Sampling them costs
        /// 21.9 ms a tile (Release, 2026-09-10), which is why it happens on a worker.
        /// </summary>
        public const int TilePosts = 513;

        /// <summary>
        /// Posts along the coarse ring and the far skirt: 7.8 m over the region, 62.5 m over the surround. Kept
        /// at what it has been since M1.A, because the ring already undersamples the 4 m bake it reads. Both are
        /// built once at a join, and sampling one costs 65.6 ms (Release, 2026-09-10): that is the rest of the
        /// debt "client view creation is still synchronous" and it is not this slice's (M1.4e non-goals).
        /// </summary>
        public const int CoarsePosts = 1025;
        public const float TileSizeM = 1000f;
        public static float PostSpacingM => TileSizeM / (TilePosts - 1);

        /// <summary>The posts of a tile whose south-west corner is at (originEast, originNorth), as [north, east].</summary>
        public static float[,] SamplePosts(IHeightSource heightfield, double originEast, double originNorth, int posts, double spacing, out float minM, out float maxM)
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
        public static TerrainData BuildData(IHeightSource heightfield, double originEast, double originNorth, float sizeM, int posts, TerrainLayer layer, out float baseM)
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
            SetOneLayer(data, layer);
            return data;
        }

        /// <summary>
        /// The tile's one layer, given all the weight. A fresh TerrainData gives its layer no weight anywhere and
        /// the ground rendered as a flat colour until the weight map was filled by hand (frames of 2026-09-08).
        /// </summary>
        private static void SetOneLayer(TerrainData data, TerrainLayer layer)
        {
            if (layer == null) return;
            data.terrainLayers = new[] { layer };
            data.alphamapResolution = 64;
            float[,,] weights = new float[data.alphamapResolution, data.alphamapResolution, 1];
            for (int z = 0; z < data.alphamapResolution; z++)
                for (int x = 0; x < data.alphamapResolution; x++)
                    weights[z, x, 0] = 1f;
            data.SetAlphamaps(0, 0, weights);
        }

        /// <summary>The kilometre tile at full detail, as before.</summary>
        public static TerrainData BuildData(IHeightSource heightfield, double originEast, double originNorth, TerrainLayer layer, out float baseM)
            => BuildData(heightfield, originEast, originNorth, TileSizeM, TilePosts, layer, out baseM);

        /// <summary>
        /// Builds and places a streamed tile from what a worker prepared (M1.4e): the heights are already
        /// sampled and normalised, so all this does is what Unity will not do off the main thread.
        /// </summary>
        public static TerrainData BuildData(PreparedTile prepared, TerrainLayer layer)
        {
            if (prepared == null) throw new System.ArgumentNullException(nameof(prepared));
            TerrainData data = new TerrainData();
            data.heightmapResolution = prepared.Posts;
            data.size = new Vector3(prepared.SizeM, prepared.RangeM, prepared.SizeM);
            // 21 ms of a tile's 24 on the main thread, measured 2026-09-10, and there is no cheaper way to say
            // it: SetHeightsDelayLOD with SyncHeightmap was measured the same day at 21 to 25 ms, the same work
            // under two names. It is Unity's own cost, it is recorded in DEBTS.md, and the budget above it stops
            // a second tile joining it in one frame.
            data.SetHeights(0, 0, prepared.Normalised);
            SetOneLayer(data, layer);
            return data;
        }

        /// <summary>Puts prepared terrain data in the scene, collidable: this is the client's ground.</summary>
        public static Terrain Place(TerrainData data, PreparedTile prepared, Material material, string name)
        {
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = name;
            go.layer = Layers.Terrain;
            go.transform.position = new Vector3((float)prepared.OriginEast, prepared.BaseM, (float)prepared.OriginNorth);
            Terrain terrain = go.GetComponent<Terrain>();
            if (material != null) terrain.materialTemplate = material;
            Settle(terrain, true);
            return terrain;
        }

        /// <summary>Builds and places the near tile in the scene: collidable, full detail.</summary>
        public static Terrain Build(IHeightSource heightfield, double originEast, double originNorth, Material material, TerrainLayer layer, string name)
            => Build(heightfield, originEast, originNorth, TileSizeM, TilePosts, material, layer, name, true, 0f);

        /// <summary>
        /// Builds and places a tile of any size. Coarse layers are sunk by <paramref name="sinkM"/> so a finer
        /// layer above them never fights their surface, and have their collider removed: the founder walks on the
        /// near tile only. The returned Terrain owns its data; destroy the GameObject to free both.
        /// </summary>
        public static Terrain Build(IHeightSource heightfield, double originEast, double originNorth, float sizeM, int posts,
                                    Material material, TerrainLayer layer, string name, bool collidable, float sinkM)
        {
            TerrainData data = BuildData(heightfield, originEast, originNorth, sizeM, posts, layer, out float baseM);
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = name;
            go.layer = Layers.Terrain;
            go.transform.position = new Vector3((float)originEast, baseM - sinkM, (float)originNorth);
            Terrain terrain = go.GetComponent<Terrain>();
            if (material != null) terrain.materialTemplate = material;
            Settle(terrain, collidable);
            return terrain;
        }

        /// <summary>What every tile is set to, wherever it was built from.</summary>
        private static void Settle(Terrain terrain, bool collidable)
        {
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
                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null) Object.Destroy(collider);
            }
        }

        /// <summary>
        /// Frees a tile's Terrain and the data it draws from (M1.4f): destroying the GameObject alone leaves the data
        /// behind, a 513-post heightmap for every tile a walk let go of.
        /// </summary>
        public static void Free(Terrain terrain)
        {
            if (terrain == null) return;
            TerrainData data = terrain.terrainData;
            UnityObjects.Free(terrain.gameObject);
            UnityObjects.Free(data);
        }

        /// <summary>
        /// Cuts a hole in a coarse tile where a finer tile sits on top of it, so the two never show through each
        /// other. The rectangle is in local metres; cells outside the tile are ignored.
        /// </summary>
        public static void CutHole(Terrain coarse, double originEast, double originNorth, double eastM, double northM, double sizeM)
            => SetHole(coarse, originEast, originNorth, eastM, northM, sizeM, true);

        /// <summary>Fills the hole <see cref="CutHole"/> cut for a finer tile, once that tile has gone (M1.4f): the same cells, by the same rounding.</summary>
        public static void FillHole(Terrain coarse, double originEast, double originNorth, double eastM, double northM, double sizeM)
            => SetHole(coarse, originEast, originNorth, eastM, northM, sizeM, false);

        private static void SetHole(Terrain coarse, double originEast, double originNorth, double eastM, double northM, double sizeM, bool cut)
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
            // Unity's holes array is true where the surface is: a cut is all false, a fill all true.
            bool[,] holes = new bool[z1 - z0, x1 - x0];
            if (!cut)
                for (int z = 0; z < z1 - z0; z++)
                    for (int x = 0; x < x1 - x0; x++)
                        holes[z, x] = true;
            data.SetHoles(x0, z0, holes);
        }
    }
}
