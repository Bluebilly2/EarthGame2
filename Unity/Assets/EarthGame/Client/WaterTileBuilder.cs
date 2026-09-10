using System.Collections.Generic;
using EarthGame.ClientCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthGame.Client
{
    /// <summary>
    /// The rectangles `WaterSurface` found in a streamed tile (M1.4c), as one mesh. Two triangles a rectangle,
    /// flat and facing up, with the local metres as the material's coordinates so a tiling texture keeps one
    /// scale across tiles. The mesh carries no collider: wading is decided by the server and the mover, not by
    /// what is drawn, and it waits for its own slice.
    /// </summary>
    public static class WaterTileBuilder
    {
        /// <summary>Builds the mesh, or returns null when the tile holds no standing water.</summary>
        public static GameObject Build(IReadOnlyList<WaterQuad> quads, Material material, string name)
        {
            if (quads == null || quads.Count == 0) return null;
            Vector3[] vertices = new Vector3[quads.Count * 4];
            Vector2[] uv = new Vector2[vertices.Length];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[quads.Count * 6];
            for (int i = 0; i < quads.Count; i++)
            {
                WaterQuad quad = quads[i];
                float up = quad.SurfaceUp;
                float west = (float)quad.EastFrom, east = (float)quad.EastTo;
                float south = (float)quad.NorthFrom, north = (float)quad.NorthTo;
                int v = i * 4;
                vertices[v + 0] = new Vector3(west, up, south);
                vertices[v + 1] = new Vector3(west, up, north);
                vertices[v + 2] = new Vector3(east, up, north);
                vertices[v + 3] = new Vector3(east, up, south);
                for (int k = 0; k < 4; k++)
                {
                    normals[v + k] = Vector3.up;
                    uv[v + k] = new Vector2(vertices[v + k].x, vertices[v + k].z);
                }
                int t = i * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 0;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            Mesh mesh = new Mesh { name = name };
            // A kilometre tile wholly under water is a quad a row, so this rarely bites; a 16-bit mesh would.
            if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            GameObject go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
