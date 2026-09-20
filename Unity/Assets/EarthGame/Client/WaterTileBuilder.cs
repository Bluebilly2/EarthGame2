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
                float west = (float)quad.EastFrom, east = (float)quad.EastTo;
                float south = (float)quad.NorthFrom, north = (float)quad.NorthTo;
                int v = i * 4;
                // Each corner at its own height (M1.4g): a still body's four are one, a creek's fall with its bed and
                // a dry corner sits on the ground, so the surface tapers to the bank instead of standing over it.
                vertices[v + 0] = new Vector3(west, quad.UpSouthWest, south);
                vertices[v + 1] = new Vector3(west, quad.UpNorthWest, north);
                vertices[v + 2] = new Vector3(east, quad.UpNorthEast, north);
                vertices[v + 3] = new Vector3(east, quad.UpSouthEast, south);
                // One normal for the whole quad, from the slope of its corners: a tilted surface must give back the
                // sky at its own angle, and the game shades every face as one plane anyway.
                float width = Mathf.Max(east - west, 1e-4f), span = Mathf.Max(north - south, 1e-4f);
                float slopeEast = ((quad.UpNorthEast + quad.UpSouthEast) - (quad.UpNorthWest + quad.UpSouthWest)) * 0.5f / width;
                float slopeNorth = ((quad.UpNorthWest + quad.UpNorthEast) - (quad.UpSouthWest + quad.UpSouthEast)) * 0.5f / span;
                Vector3 normal = new Vector3(-slopeEast, 1f, -slopeNorth).normalized;
                for (int k = 0; k < 4; k++)
                {
                    normals[v + k] = normal;
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

        /// <summary>Frees a water tile and the mesh it owns (M1.4f): destroying the GameObject alone leaves the mesh behind.</summary>
        public static void Free(GameObject water)
        {
            if (water == null) return;
            MeshFilter filter = water.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            UnityObjects.Free(water);
            UnityObjects.Free(mesh);
        }
    }
}
