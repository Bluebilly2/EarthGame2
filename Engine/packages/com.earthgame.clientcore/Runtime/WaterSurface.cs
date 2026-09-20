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

        /// <summary>
        /// Where the surface sits at each of the four corners, south-west, north-west, north-east, south-east. A
        /// body of still water is flat and all four are one height; a creek falls with its bed, and a corner where
        /// no water stands sits on the ground itself, so the surface tapers to nothing at the bank instead of
        /// standing over it as a plate (M1.4g, 2026-09-20: flat plates a cell wide made a creek a staircase).
        /// </summary>
        public float UpSouthWest, UpNorthWest, UpNorthEast, UpSouthEast;

        /// <summary>The highest of the four corners: what a flat body's one surface is.</summary>
        public float SurfaceUp => Math.Max(Math.Max(UpSouthWest, UpNorthWest), Math.Max(UpNorthEast, UpSouthEast));

        /// <summary>The lowest of the four corners.</summary>
        public float LowestUp => Math.Min(Math.Min(UpSouthWest, UpNorthWest), Math.Min(UpNorthEast, UpSouthEast));

        /// <summary>True when the four corners stand at one height, within the centimetre the wire rounds to.</summary>
        public bool Flat => SurfaceUp - LowestUp <= WaterSurface.SameSurfaceM;

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

                    // Every corner takes its own height: a wet one its water's surface, a dry one the ground it stands
                    // on, a millimetre under it so the two do not fight for the same pixel. So the surface of a creek
                    // falls with its bed, and it tapers to nothing exactly where the bank begins instead of standing
                    // over it. Neighbouring cells share their posts, so the skin is continuous along a channel.
                    // Flat plates a cell wide, which is what this was until 2026-09-20, made a creek a staircase of
                    // panes stepping down the slope with gaps between them (William's own frame of that morning).
                    float upSW = southWest ? sw : gsw - GroundBiasM;
                    float upNW = northWest ? nw : gnw - GroundBiasM;
                    float upNE = northEast ? ne : gne - GroundBiasM;
                    float upSE = southEast ? se : gse - GroundBiasM;

                    // Nothing to draw where no corner is wet, and nothing of the sea's own plane: the view passes the
                    // datum and the sea has had a plane at it since 2026-09-08.
                    bool wet = wetCorners > 0 && Math.Max(Math.Max(upSW, upNW), Math.Max(upNE, upSE)) > aboveM;

                    // A body's inside is flat, and a flat row merges into one quad: the sea across a kilometre tile is
                    // a quad a row instead of two hundred and fifty. A cell that is not flat is its own quad.
                    bool flat = wet && wetCorners == 4
                        && Math.Max(Math.Max(upSW, upNW), Math.Max(upNE, upSE)) - Math.Min(Math.Min(upSW, upNW), Math.Min(upNE, upSE)) <= SameSurfaceM;
                    float up = flat ? Math.Min(Math.Min(upSW, upNW), Math.Min(upNE, upSE)) : 0f;
                    bool joins = flat && runFrom >= 0 && Math.Abs(up - runUp) <= SameSurfaceM;
                    if (!joins && runFrom >= 0)
                    {
                        quads.Add(Flat(ground, cell, z, runFrom, x, runUp));
                        runFrom = -1;
                    }
                    if (flat && runFrom < 0)
                    {
                        runFrom = x;
                        runUp = up;
                    }
                    if (wet && !flat) quads.Add(Cornered(ground, cell, z, x, upSW, upNW, upNE, upSE));
                }
                if (runFrom >= 0) quads.Add(Flat(ground, cell, z, runFrom, posts - 1, runUp));
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

        /// <summary>A dry corner sits this far under its ground, so the water's edge is buried rather than co-planar.</summary>
        private const float GroundBiasM = 0.001f;

        /// <summary>The rectangle of the cells from <paramref name="fromX"/> up to (not including) <paramref name="toX"/>, all at one height.</summary>
        private static WaterQuad Flat(ReceivedTile ground, double cell, int z, int fromX, int toX, float up)
        {
            WaterQuad quad = Bounds(ground, cell, z, fromX, toX);
            quad.UpSouthWest = quad.UpNorthWest = quad.UpNorthEast = quad.UpSouthEast = up;
            return quad;
        }

        /// <summary>One cell, each corner at its own height.</summary>
        private static WaterQuad Cornered(ReceivedTile ground, double cell, int z, int x, float sw, float nw, float ne, float se)
        {
            WaterQuad quad = Bounds(ground, cell, z, x, x + 1);
            quad.UpSouthWest = sw;
            quad.UpNorthWest = nw;
            quad.UpNorthEast = ne;
            quad.UpSouthEast = se;
            return quad;
        }

        private static WaterQuad Bounds(ReceivedTile ground, double cell, int z, int fromX, int toX)
        {
            WaterQuad quad = default;
            quad.EastFrom = ground.OriginEast + fromX * cell;
            quad.EastTo = ground.OriginEast + toX * cell;
            quad.NorthFrom = ground.OriginNorth + z * cell;
            quad.NorthTo = ground.OriginNorth + (z + 1) * cell;
            return quad;
        }
    }
}
