using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>A tree as the client draws it (M1.6a): the foot of its trunk, which way it faces, how tall and how wide.</summary>
    public struct StandTree
    {
        public float East;
        public float Up;
        public float North;
        public float YawDeg;
        public float HeightM;
        /// <summary>The crown's diameter, m: the species' crown share of its height.</summary>
        public float CrownM;
        /// <summary>The tall plant, as an index into <see cref="StandCodes.Tall"/>.</summary>
        public int Tall;
        /// <summary>Which of the drawn variants of its form, from its cell.</summary>
        public int Variant;
        /// <summary>Whether its bark has been taken (BF.3): drawn pale.</summary>
        public bool Stripped;
        public int Row;
        public int Col;
    }

    /// <summary>A stick or a cobble as the client draws it: where it lies and which way it points.</summary>
    public struct LooseInstance
    {
        public float East;
        public float Up;
        public float North;
        public float YawDeg;
        public int Variant;
    }

    /// <summary>A tile's trees, sticks and cobbles, placed for drawing, and the tiles they were placed from.</summary>
    public sealed class PreparedStand
    {
        public TileId Id;
        public uint StandCrc;
        public uint LooseCrc;
        public uint GroundCrc;
        /// <summary>The checksum of the one ground they were stood on (BF.4, <see cref="GroundSnapshot.Crc"/>); zero for the raster alone.</summary>
        public uint ReliefCrc;
        /// <summary>The stone tile the rocks were decided from (BF.4 stage three); zero for none, and then no rocks.</summary>
        public uint StoneCrc;
        public StandTree[] Trees;
        public LooseInstance[] Sticks;
        public LooseInstance[] Cobbles;
        /// <summary>The rocks that stand on the tile's cells (BF.4 stage three), as the server decides them.</summary>
        public StandingRock[] Rocks;
    }

    /// <summary>
    /// What stands and lies on a streamed tile, placed for drawing (M1.6a): a worker's job, as a tile's ground is
    /// (M1.4e). Every post names its cell by <see cref="TileCodec.CellOf"/> and every thing on it its place by
    /// <see cref="StandLayout"/>, so what is drawn is where the server put it, to the centimetre; the ground under each
    /// thing is the one ground (<see cref="ClientGround"/>, BF.4) read off the tile's own posts, its cover's relief and the
    /// hollows dug in it.
    ///
    /// <para>A tile shares its edge posts with its neighbours, so each tile draws its posts but its last row and its
    /// last column, which are the first of the tile beyond — unless no tile lies beyond, at the region's east and
    /// north edges. So every cell is drawn once, by one tile, whichever tiles a client holds.</para>
    /// </summary>
    public static class StandPreparation
    {
        /// <summary>How many shapes each form, and the sticks and the cobbles, are drawn in; a thing's cell picks one.</summary>
        public const int Variants = StandLayout.Looks;

        /// <param name="taken">What has been taken from the tile's cells (M1.5b), a copy the worker alone reads (<see cref="TakenIn"/>); those things are not placed.</param>
        /// <summary>The deepest water a stick or a cobble is seen and reached under, m: a hand. Deeper, the lake bed keeps its things to itself (M1.6e).</summary>
        public const double DeepestLyingM = 0.10;

        /// <summary>Whether a thing lying on the ground is seen and reached with water of a depth over it.</summary>
        public static bool LiesUnder(double waterDepthM) => waterDepthM <= DeepestLyingM;

        /// <param name="depth">The tile of the water's depth over the same ground, where the client holds it: a cell under more than a hand of water gives up its sticks and cobbles (M1.6e). Null draws them all, as before.</param>
        public static PreparedStand Prepare(ReceivedTile stand, ReceivedTile loose, ReceivedTile ground, TileGrid grid, LooseTaken taken = null, ReceivedTile depth = null)
            => Prepare(stand, loose, ground, grid, taken, depth, null);

        /// <param name="trunkFlags">What has been done to the tile's trunks (BF.3), a copy the worker alone reads (<see cref="TrunkFlagsIn"/>): a felled trunk is not placed, a stripped one is marked.</param>
        public static PreparedStand Prepare(ReceivedTile stand, ReceivedTile loose, ReceivedTile ground, TileGrid grid, LooseTaken taken, ReceivedTile depth, Dictionary<long, byte> trunkFlags)
            => Prepare(stand, loose, ground, grid, taken, depth, trunkFlags, null);

        /// <param name="fine">The one ground over the tile and the tiles its things spill into (BF.4), a copy the worker alone reads (<see cref="ClientGround.SnapshotFor"/>); null stands everything on the tile's raster alone.</param>
        public static PreparedStand Prepare(ReceivedTile stand, ReceivedTile loose, ReceivedTile ground, TileGrid grid, LooseTaken taken, ReceivedTile depth, Dictionary<long, byte> trunkFlags,
                                            GroundSnapshot fine)
            => Prepare(stand, loose, ground, grid, taken, depth, trunkFlags, fine, null);

        /// <param name="stone">The tile's stone codes (BF.4 stage three): with the snapshot's cover and the loose layer, what the rocks that stand are decided from; null places no rocks yet.</param>
        public static PreparedStand Prepare(ReceivedTile stand, ReceivedTile loose, ReceivedTile ground, TileGrid grid, LooseTaken taken, ReceivedTile depth, Dictionary<long, byte> trunkFlags,
                                            GroundSnapshot fine, ReceivedTile stone)
        {
            if (stand == null) throw new ArgumentNullException(nameof(stand));
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (stand.Codes == null) throw new ArgumentException("tile " + stand.Id + " carries no stand codes", nameof(stand));
            if (ground.Heights == null) throw new ArgumentException("tile " + ground.Id + " carries no heights", nameof(ground));
            if (!stand.Id.Equals(ground.Id) || stand.Posts != ground.Posts)
                throw new ArgumentException("the stand of tile " + stand.Id + " and the ground of tile " + ground.Id + " are not one tile's", nameof(ground));
            if (loose != null && (loose.Codes == null || !loose.Id.Equals(stand.Id) || loose.Posts != stand.Posts))
                throw new ArgumentException("the loose layer of tile " + loose.Id + " does not match the stand of " + stand.Id, nameof(loose));

            int posts = stand.Posts;
            double cell = stand.CellM;
            int cellCm = (int)Math.Round(cell * 100.0);
            int lastX = stand.Id.Ix == grid.TilesPerSide - 1 ? posts - 1 : posts - 2;
            int lastZ = stand.Id.Iz == grid.TilesPerSide - 1 ? posts - 1 : posts - 2;
            List<StandTree> trees = new List<StandTree>();
            List<LooseInstance> sticks = new List<LooseInstance>();
            List<LooseInstance> cobbles = new List<LooseInstance>();
            List<StandingRock> rocks = new List<StandingRock>();
            // The rocks are decided from the cover, the loose layer, the stone and the stand on the tile's own posts, and the one
            // ground without its hollows: all of it, or none are placed until it is held.
            ReceivedTile cover = fine?.OwnCover;
            bool rocksKnown = loose != null && OnePosts(stand, cover) && OnePosts(stand, stone);
            IHeightSource undug = fine?.Undug;
            for (int z = 0; z <= lastZ; z++)
                for (int x = 0; x <= lastX; x++)
                {
                    double postEast = stand.OriginEast + x * cell;
                    double postNorth = stand.OriginNorth + z * cell;
                    TileCodec.CellOf(grid.ExtentM, cell, postEast, postNorth, out int row, out int col);
                    byte code = stand.Codes[z, x];
                    PlantSpecies species = code == 0 ? null : StandCodes.SpeciesOf(code);
                    byte flags = 0;
                    if (trunkFlags != null) trunkFlags.TryGetValue(LooseTaken.Key(row, col), out flags);
                    if (species != null && (flags & TrunkChange.Felled) == 0)
                    {
                        StandLayout.Place(row, col, StandLayout.Kind.Trunk, 0, cellCm, out int eastCm, out int northCm, out int yaw);
                        double east = postEast + eastCm / 100.0;
                        double north = postNorth + northCm / 100.0;
                        double height = StandCodes.HeightOf(code);
                        trees.Add(new StandTree
                        {
                            East = (float)east,
                            Up = (float)GroundUnder(fine, ground, east, north),
                            North = (float)north,
                            YawDeg = yaw,
                            HeightM = (float)height,
                            CrownM = (float)(species.CrownShare * height),
                            Tall = TallIndex(species),
                            Variant = VariantOf(row, col, 0UL),
                            Stripped = (flags & TrunkChange.BarkTaken) != 0,
                            Row = row,
                            Col = col,
                        });
                    }
                    if (loose == null) continue;
                    byte things = loose.Codes[z, x];
                    StandingRock rock = default;
                    bool hasRock = rocksKnown && StandingRocks.TryDecide(row, col, cover.Codes[z, x], things, stone.Codes[z, x], code, cell, grid.ExtentM, undug, out rock);
                    if (hasRock) rocks.Add(rock);
                    if (things == 0) continue;
                    if (depth?.Heights != null && !LiesUnder(TileGround.HeightAt(depth, postEast, postNorth))) continue;
                    LooseTaken.Cell gone = default;
                    taken?.TryGet(row, col, out gone);
                    for (int k = 0; k < LooseCodes.SticksOf(things); k++)
                        if ((gone.Sticks & (1 << k)) == 0) sticks.Add(Lying(StandLayout.Kind.Stick, row, col, k, cellCm, postEast, postNorth, ground, fine, hasRock, rock));
                    for (int k = 0; k < LooseCodes.CobblesOf(things); k++)
                        if ((gone.Cobbles & (1 << k)) == 0) cobbles.Add(Lying(StandLayout.Kind.Cobble, row, col, k, cellCm, postEast, postNorth, ground, fine, hasRock, rock));
                }
            return new PreparedStand
            {
                Id = stand.Id,
                StandCrc = stand.Crc32,
                LooseCrc = loose != null ? loose.Crc32 : 0u,
                GroundCrc = ground.Crc32,
                ReliefCrc = fine != null ? fine.Crc : 0u,
                StoneCrc = rocksKnown ? stone.Crc32 : 0u,
                Trees = trees.ToArray(),
                Sticks = sticks.ToArray(),
                Cobbles = cobbles.ToArray(),
                Rocks = rocks.ToArray(),
            };
        }

        /// <summary>Whether a code tile lies on a tile's own posts.</summary>
        private static bool OnePosts(ReceivedTile tile, ReceivedTile codes) =>
            codes?.Codes != null && codes.Id.Equals(tile.Id) && codes.Posts == tile.Posts && Math.Abs(codes.CellM - tile.CellM) < 1e-9;

        /// <summary>
        /// A thing lying, placed as it is drawn (M1.5b): its cell's centre moved by the layout, on the ground the tile
        /// carries. A streamed tile's post for a cell stands at the cell's centre, so this is where the drawing put it.
        /// </summary>
        public static LooseInstance Lying(LyingThing thing, ReceivedTile ground, double cellM, double extentM)
            => Lying(thing, ground, cellM, extentM, null);

        /// <summary>A thing lying, placed as it is drawn, on the one ground (BF.4) where it is given; the tile's raster alone where it is not.</summary>
        public static LooseInstance Lying(LyingThing thing, ReceivedTile ground, double cellM, double extentM, IHeightSource fine)
            => Lying(thing, ground, cellM, extentM, fine, false, default);

        /// <summary>The same, moved off the rock standing on its cell where its place falls inside it (BF.4 stage three).</summary>
        public static LooseInstance Lying(LyingThing thing, ReceivedTile ground, double cellM, double extentM, IHeightSource fine, bool hasRock, in StandingRock rock)
        {
            StandLayout.CellCentre(thing.Row, thing.Col, cellM, extentM, out double east, out double north);
            return Lying(thing.Kind, thing.Row, thing.Col, thing.Index, (int)Math.Round(cellM * 100.0), east, north, ground, fine, hasRock, rock);
        }

        /// <summary>The ground under a thing: the one ground where it is held there, else the tile's own raster.</summary>
        private static double GroundUnder(IHeightSource fine, ReceivedTile ground, double east, double north)
        {
            double h = fine != null ? fine.HeightAt(east, north) : double.NaN;
            return double.IsNaN(h) ? TileGround.HeightAt(ground, east, north) : h;
        }

        /// <summary>
        /// The takings on a tile's cells, copied (M1.5b): what a worker preparing the tile reads, while the main thread goes
        /// on adding to the client's own as the server tells of more.
        /// </summary>
        public static LooseTaken TakenIn(LooseTaken taken, ReceivedTile tile, TileGrid grid)
        {
            LooseTaken copy = new LooseTaken();
            if (taken == null || taken.Count == 0 || tile == null || grid == null) return copy;
            foreach (LooseTaken.Cell cell in taken.Cells())
                if (Covers(tile, grid, cell.Row, cell.Col)) copy.Merge(cell);
            return copy;
        }

        /// <summary>The trunks' changes on a tile's cells, copied (BF.3): the flags of every cell with a trunk change, keyed as the takings are.</summary>
        public static Dictionary<long, byte> TrunkFlagsIn(WorldChanges changes, ReceivedTile tile, TileGrid grid)
        {
            Dictionary<long, byte> copy = new Dictionary<long, byte>();
            if (changes == null || tile == null || grid == null) return copy;
            foreach (CellChange cell in changes.Cells())
                if (cell.HasTrunk && cell.TrunkFlags != 0 && Covers(tile, grid, cell.Row, cell.Col)) copy[LooseTaken.Key(cell.Row, cell.Col)] = cell.TrunkFlags;
            return copy;
        }

        /// <summary>Whether one of a tile's posts stands for a cell of the world's raster; a cell on a tile's edge is two tiles'.</summary>
        public static bool Covers(ReceivedTile tile, TileGrid grid, int row, int col)
        {
            // The tile's first post is its south-west corner: the southernmost row and the westernmost column it holds.
            TileCodec.CellOf(grid.ExtentM, tile.CellM, tile.OriginEast, tile.OriginNorth, out int southRow, out int westCol);
            int span = tile.Posts - 1;
            return col >= westCol && col <= westCol + span && row <= southRow && row >= southRow - span;
        }

        private static LooseInstance Lying(StandLayout.Kind kind, int row, int col, int index, int cellCm, double postEast, double postNorth, ReceivedTile ground, IHeightSource fine,
                                           bool hasRock, in StandingRock rock)
        {
            StandLayout.Place(row, col, kind, index, cellCm, out int eastCm, out int northCm, out int yaw);
            double east = postEast + eastCm / 100.0;
            double north = postNorth + northCm / 100.0;
            StandingRocks.LyingPlace(hasRock, rock, ref east, ref north);
            return new LooseInstance
            {
                East = (float)east,
                Up = (float)GroundUnder(fine, ground, east, north),
                North = (float)north,
                YawDeg = yaw,
                Variant = StandLayout.LookOf(row, col, kind, index),
            };
        }

        private static int VariantOf(int row, int col, ulong salt) =>
            (int)(StandLayout.Mix((((ulong)(uint)row << 32) | (uint)col) ^ (salt << 40)) % Variants);

        private static int TallIndex(PlantSpecies species)
        {
            IReadOnlyList<PlantSpecies> all = StandCodes.Tall;
            for (int i = 0; i < all.Count; i++)
                if (ReferenceEquals(all[i], species)) return i;
            return -1;
        }
    }
}
