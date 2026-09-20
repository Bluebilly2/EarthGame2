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
    /// <para>A cell wholly under water sits at the lowest of its four surfaces, so it never stands above its own
    /// bank. A cell with dry corners — a shore, or a channel narrower than the 4 m between posts — takes the
    /// highest wet corner's surface, held down to the lowest dry corner's ground and drawn only while a
    /// centimetre of water is still left above the wettest corner's bed. Until 2026-09-20 such a cell was not
    /// drawn at all, which required four wet corners and so hid every creek in the world (M1.4g). Cells along a
    /// row are merged while their surface holds at one height, because standing water is flat: the sea across a
    /// kilometre tile is a quad a row instead of two hundred and fifty.</para>
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
                    bool southWest = Surface(ground, depth, z, x, out float sw, out float gsw);
                    bool southEast = Surface(ground, depth, z, x + 1, out float se, out float gse);
                    bool northWest = Surface(ground, depth, z + 1, x, out float nw, out float gnw);
                    bool northEast = Surface(ground, depth, z + 1, x + 1, out float ne, out float gne);
                    int wetCorners = (southWest ? 1 : 0) + (southEast ? 1 : 0) + (northWest ? 1 : 0) + (northEast ? 1 : 0);
                    bool wet = wetCorners > 0;
                    float up = 0f;
                    if (wetCorners == 4)
                    {
                        // A cell wholly under water is a body's inside: the lowest of the four, so the surface never
                        // stands above a bank it meets (M1.4c).
                        up = Math.Min(Math.Min(sw, se), Math.Min(nw, ne));
                    }
                    else if (wet)
                    {
                        // A cell with dry corners is an edge or a channel. Until 2026-09-20 it was left undrawn, which
                        // hid every creek in the world: WG.1's channels are one cell wide, so no cell along them has
                        // four wet corners and nothing was ever built, while the founder waded and drank there. The
                        // surface is the highest wet corner's — the water is really there — held down to the lowest dry
                        // corner's ground so it can never float over dry land, and drawn only if what is left still
                        // stands a centimetre over the wettest corner's own bed. On a shore that clamp puts the sheet
                        // at the sand and the opaque ground hides what reaches past the waterline (M1.4g).
                        up = float.NegativeInfinity;
                        if (southWest && sw > up) up = sw;
                        if (southEast && se > up) up = se;
                        if (northWest && nw > up) up = nw;
                        if (northEast && ne > up) up = ne;
                        float dryGround = float.PositiveInfinity;
                        if (!southWest && gsw < dryGround) dryGround = gsw;
                        if (!southEast && gse < dryGround) dryGround = gse;
                        if (!northWest && gnw < dryGround) dryGround = gnw;
                        if (!northEast && gne < dryGround) dryGround = gne;
                        if (dryGround < up) up = dryGround;
                        float bed = float.PositiveInfinity;
                        if (southWest && gsw < bed) bed = gsw;
                        if (southEast && gse < bed) bed = gse;
                        if (northWest && gnw < bed) bed = gnw;
                        if (northEast && gne < bed) bed = gne;
                        wet = up - bed >= MinDepthM;
                    }
                    if (wet) wet = up > aboveM;
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

        /// <summary>The water's surface and the ground at a post, and whether any water stands there at all.</summary>
        private static bool Surface(ReceivedTile ground, ReceivedTile depth, int z, int x, out float up, out float bed)
        {
            float d = depth.Heights[z, x];
            bed = ground.Heights[z, x];
            up = bed + d;
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
