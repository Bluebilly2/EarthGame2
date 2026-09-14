using EarthGame.ClientCore;
using UnityEngine;

namespace EarthGame.Client
{
    /// <summary>
    /// The colour map `GroundColourMap` built for a tile (M1.4d), as the terrain layer that tile is drawn with:
    /// one texture stretched once over the tile, so a texel is about two metres of country and the terrain
    /// shader needs nothing it does not already have.
    ///
    /// <para>Everything but the picture is taken from the flat layer the client shipped with — its smoothness
    /// above all, which was tuned in the frames of 2026-09-08 so that ground does not read as water.</para>
    /// </summary>
    public static class GroundLayerBuilder
    {
        /// <summary>The layer for a tile, or null when the tile carried no cover.</summary>
        public static TerrainLayer Build(byte[] rgb, int texels, float sizeM, TerrainLayer like, string name)
        {
            if (rgb == null || rgb.Length != texels * texels * GroundColourMap.BytesPerTexel) return null;
            // Mip-mapped, or the ground shimmers as the founder turns; clamped, because the map is this tile's
            // own square and the neighbouring tile has its own.
            Texture2D texture = new Texture2D(texels, texels, TextureFormat.RGB24, true, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            // The one level, and Apply makes the rest: LoadRawTextureData wants the whole mip chain and threw
            // "not enough data provided" nine times on the first streamed run (2026-09-10).
            texture.SetPixelData(rgb, 0);
            texture.Apply(true, true);
            TerrainLayer layer = new TerrainLayer
            {
                name = name,
                diffuseTexture = texture,
                tileSize = new Vector2(sizeM, sizeM),
                tileOffset = Vector2.zero,
            };
            if (like != null)
            {
                layer.specular = like.specular;
                layer.metallic = like.metallic;
                layer.smoothness = like.smoothness;
                layer.diffuseRemapMin = like.diffuseRemapMin;
                layer.diffuseRemapMax = like.diffuseRemapMax;
            }
            return layer;
        }

        /// <summary>Frees a layer and the picture it owns; both are objects and neither is collected on its own.</summary>
        public static void Free(TerrainLayer layer)
        {
            if (layer == null) return;
            UnityObjects.Free(layer.diffuseTexture);
            UnityObjects.Free(layer);
        }
    }
}
