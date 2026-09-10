using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// A run of cells along one row of a tile, all under water standing at one height: the rectangle a view draws
    /// as a flat quad. East and north are the local metres of its corners; <see cref="SurfaceUp"/> is where the
    /// water's surface sits.
    /// </summary>
    public struct WaterQuad
    {
        public double EastFrom, EastTo;
        public double NorthFrom, NorthTo;
        public float SurfaceUp;

        public double WidthM => EastTo - EastFrom;
        public double DepthM => NorthTo - NorthFrom;
    }

    /// <summary>
    /// Turns the depth a client was streamed (M1.4b) into the rectangles a view draws (M1.4c). Engine-free, so
    /// what is drawn can be asserted without a renderer.
    ///
    /// <para>A cell is under water when all four of its posts carry depth, so the drawn edge stops at the last
    /// cell wholly under water rather than lapping over dry ground, and it sits at the lowest of those four
    /// surfaces, so an edge cell never stands above its own bank. Cells along a row are merged while their
    /// surface holds at one height, because standing water is flat: the sea across a kilometre tile is a quad a
    /// row instead of two hundred and fifty.</para>
    ///
    /// <para>The sea is not drawn from here. It has had a plane at the datum around the founder since
    /// 2026-09-08, and two surfaces at one height would fight; <see cref="Build"/> takes the height above which
    /// water is its business, which the view passes as the datum.</para>
    /// </summary>
    public static class WaterSurface
    {
        /// <summary>Below this a post is dry: the wire rounds to the centimetre, so a centimetre is noise.</summary>
        public const float MinDepthM = 0.01f;

        /// <summary>Two posts are one surface within this, in metres; a flat body is flat to the centimetre it travelled as.</summary>
        public const float SameSurfaceM = 0.011f;

        /// <summary>
        /// The quads for a tile, or an empty list when none of it is under water. The two tiles must be the same
        /// tile of the same size: a depth carries no meaning over another tile's ground.
        /// </summary>
        /// <param name="aboveM">Only water standing above this height is drawn; the view passes the sea's datum.</param>
        public static List<WaterQuad> Build(ReceivedTile ground, ReceivedTile depth, double aboveM = Heightfield.SeaLevelM)
        {
            List<WaterQuad> quads = new List<WaterQuad>();
            if (ground == null || depth == null) return quads;
            if (ground.Layer != TileLayer.Ground) throw new ArgumentException("the ground of a tile is layer " + TileLayer.Ground + ", not " + ground.Layer, nameof(ground));
            if (depth.Layer != TileLayer.WaterDepth) throw new ArgumentException("the depth of a tile is layer " + TileLayer.WaterDepth + ", not " + depth.Layer, nameof(depth));
            if (!ground.Id.Equals(depth.Id)) throw new ArgumentException("tile " + depth.Id + "'s depth over tile " + ground.Id + "'s ground", nameof(depth));
            if (ground.Posts != depth.Posts) throw new ArgumentException("a tile of " + depth.Posts + " posts over one of " + ground.Posts, nameof(depth));
            if (ground.Heights == null || depth.Heights == null) return quads;

            int posts = ground.Posts;
            double cell = ground.CellM;
            for (int z = 0; z + 1 < posts; z++)
            {
                int runFrom = -1;
                float runUp = 0f;
                for (int x = 0; x + 1 < posts; x++)
                {
                    // All four, and never short of them: a cell is water only when its whole floor is under it.
                    bool southWest = Surface(ground, depth, z, x, out float sw);
                    bool southEast = Surface(ground, depth, z, x + 1, out float se);
                    bool northWest = Surface(ground, depth, z + 1, x, out float nw);
                    bool northEast = Surface(ground, depth, z + 1, x + 1, out float ne);
                    bool wet = southWest && southEast && northWest && northEast;
                    float up = 0f;
                    if (wet)
                    {
                        // The lowest of the four, so the water never stands above the bank it meets.
                        up = Math.Min(Math.Min(sw, se), Math.Min(nw, ne));
                        wet = up > aboveM;
                    }
                    bool joins = wet && runFrom >= 0 && Math.Abs(up - runUp) <= SameSurfaceM;
                    if (!joins && runFrom >= 0)
                    {
                        quads.Add(Quad(ground, cell, z, runFrom, x, runUp));
                        runFrom = -1;
                    }
                    if (wet && runFrom < 0)
                    {
                        runFrom = x;
                        runUp = up;
                    }
                }
                if (runFrom >= 0) quads.Add(Quad(ground, cell, z, runFrom, posts - 1, runUp));
            }
            return quads;
        }

        /// <summary>The water's surface at a post, and whether any stands there at all.</summary>
        private static bool Surface(ReceivedTile ground, ReceivedTile depth, int z, int x, out float up)
        {
            float d = depth.Heights[z, x];
            up = ground.Heights[z, x] + d;
            return d >= MinDepthM;
        }

        /// <summary>The rectangle of the cells from <paramref name="fromX"/> up to (not including) <paramref name="toX"/>.</summary>
        private static WaterQuad Quad(ReceivedTile ground, double cell, int z, int fromX, int toX, float up)
        {
            WaterQuad quad;
            quad.EastFrom = ground.OriginEast + fromX * cell;
            quad.EastTo = ground.OriginEast + toX * cell;
            quad.NorthFrom = ground.OriginNorth + z * cell;
            quad.NorthTo = ground.OriginNorth + (z + 1) * cell;
            quad.SurfaceUp = up;
            return quad;
        }
    }
}
