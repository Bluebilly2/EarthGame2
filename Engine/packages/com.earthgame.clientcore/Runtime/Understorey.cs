using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The shapes the understorey is drawn in (M1.6c). The cover says what grows, not which plant it is — a grass tree
    /// and a heath banksia are both heath — so what is drawn is a shape, and the contract says so rather than pretending.
    /// </summary>
    public enum TuftShape : byte
    {
        /// <summary>Heath: a low woody bush.</summary>
        Shrub = 0,
        /// <summary>Sedge: a fan of straps, as lomandra and saw-sedge stand.</summary>
        Clump = 1,
        /// <summary>Bracken: fronds off a short stem.</summary>
        Frond = 2,
        /// <summary>Grass: a tuft of blades.</summary>
        Tussock = 3,
    }

    /// <summary>One tuft of the understorey as the client draws it (M1.6c): where it stands, which way it faces, how tall and how wide.</summary>
    public struct UnderstoreyTuft
    {
        public float East;
        public float Up;
        public float North;
        public float YawDeg;
        public float HeightM;
        public float AcrossM;
        public TuftShape Shape;
        /// <summary>Which of the drawn variants of its shape, from its own hash.</summary>
        public int Variant;
    }

    /// <summary>
    /// What grows underfoot, placed for drawing (M1.6c): the cover the server streams (M1.4d) says what stands on each
    /// cell, the quarter of the land's wetness beside it says how thickly and how tall, and every tuft is put inside its
    /// cell by <see cref="StandLayout"/>'s hash, so the same tuft stands in the same centimetre in every client and
    /// every run. Nothing new travels: this is the cover byte read a second way.
    ///
    /// <para>A cell that carries a tree keeps its tufts out of the bark, so nothing grows through a trunk. Engine-free,
    /// as the trees' placement is, so that what is drawn can be counted without a renderer.</para>
    /// </summary>
    public static class Understorey
    {
        /// <summary>How many shapes there are, each drawn in <see cref="StandPreparation.Variants"/> variants.</summary>
        public const int Shapes = 4;

        /// <summary>How far a tuft's foot is set into the ground, m, so a slope shows no gap under it.</summary>
        public const double SinkM = 0.05;

        /// <summary>The most tufts one cell carries, whatever the cover, the wetness and the cell's size say: a safety valve, not a rule.</summary>
        public const int MostPerCell = 64;

        /// <summary>
        /// Whether a cover grows anything and what: the shape it stands in, how many stand on a square metre of it
        /// before the wetness has its say, and how tall they stand, m. A forest floor grows nothing — its litter is drawn
        /// already — and nor do sand, rock, bare earth or water. By the square metre and not by the cell, because a cell
        /// is as wide as the world's raster and this must not change with it.
        /// </summary>
        public static bool Grows(GroundCover cover, out TuftShape shape, out double perSquareM, out double heightM)
        {
            switch (cover)
            {
                case GroundCover.Heath:
                    shape = TuftShape.Shrub;
                    perSquareM = 0.19;
                    heightM = 0.90;
                    return true;
                case GroundCover.Bracken:
                    shape = TuftShape.Frond;
                    perSquareM = 0.375;
                    heightM = 0.80;
                    return true;
                case GroundCover.Sedge:
                    shape = TuftShape.Clump;
                    perSquareM = 0.25;
                    heightM = 0.70;
                    return true;
                case GroundCover.Grass:
                    shape = TuftShape.Tussock;
                    perSquareM = 0.5;
                    heightM = 0.45;
                    return true;
                case GroundCover.SwampFloor:
                    shape = TuftShape.Clump;
                    perSquareM = 0.19;
                    heightM = 0.60;
                    return true;
                default:
                    shape = TuftShape.Tussock;
                    perSquareM = 0.0;
                    heightM = 0.0;
                    return false;
            }
        }

        /// <summary>How wide a shape stands for the height it stands at: a bush spreads, a tussock does not.</summary>
        public static double AcrossShare(TuftShape shape) =>
            shape == TuftShape.Shrub ? 1.2 : shape == TuftShape.Frond ? 1.1 : shape == TuftShape.Clump ? 1.0 : 0.9;

        /// <summary>How many stand on a square metre of a cover in that quarter of the land's wetness: the wettest grows two-thirds more than the driest.</summary>
        public static double PerSquareMetreIn(double perSquareM, int quarter) => perSquareM * (0.75 + 0.17 * Quarter(quarter));

        /// <summary>How tall they stand there: the wettest quarter grows them a third taller than the driest.</summary>
        public static double HeightIn(double heightM, int quarter) => heightM * (0.85 + 0.10 * Quarter(quarter));

        /// <summary>
        /// Every tuft standing within a distance of a point across the ground, added to a list, out of the cover, ground
        /// and stand tiles the client holds. A cell whose tiles are not held grows nothing.
        /// </summary>
        public static void Find(double east, double north, double radiusM, TileReceiver tiles, TileGrid grid, List<UnderstoreyTuft> into)
        {
            if (tiles == null || grid == null || into == null || !(radiusM > 0.0)) return;
            ReceivedTile here = tiles.Holding(TileLayer.GroundCover, grid.ForPosition(east, north));
            if (here?.Codes == null) return;
            double cell = here.CellM;
            if (!(cell > 0.0)) return;
            int cellCm = (int)Math.Round(cell * 100.0);
            double reach2 = radiusM * radiusM;
            double look = radiusM + cell;
            TileCodec.CellOf(grid.ExtentM, cell, east - look, north + look, out int rowMin, out int colMin);
            TileCodec.CellOf(grid.ExtentM, cell, east + look, north - look, out int rowMax, out int colMax);
            for (int row = rowMin; row <= rowMax; row++)
                for (int col = colMin; col <= colMax; col++)
                {
                    StandLayout.CellCentre(row, col, cell, grid.ExtentM, out double centreEast, out double centreNorth);
                    TileId id = grid.ForPosition(centreEast, centreNorth);
                    ReceivedTile cover = tiles.Holding(TileLayer.GroundCover, id);
                    ReceivedTile ground = tiles.Holding(TileLayer.Ground, id);
                    if (cover?.Codes == null || ground?.Heights == null || ground.Posts != cover.Posts) continue;
                    int x = (int)Math.Round((centreEast - cover.OriginEast) / cell);
                    int z = (int)Math.Round((centreNorth - cover.OriginNorth) / cell);
                    if (x < 0 || z < 0 || x >= cover.Posts || z >= cover.Posts) continue;
                    byte code = cover.Codes[z, x];
                    if (!Grows(GroundCovers.CoverOf(code), out TuftShape shape, out double perSquareM, out double heightM)) continue;
                    int quarter = GroundCovers.QuarterOf(code);
                    int count = CountOn(row, col, PerSquareMetreIn(perSquareM, quarter) * cell * cell);
                    if (count <= 0) continue;
                    double across = AcrossShare(shape);
                    double stands = HeightIn(heightM, quarter);
                    Trunk(tiles.Holding(TileLayer.Stand, id), cover, row, col, cellCm, centreEast, centreNorth,
                          out double trunkEast, out double trunkNorth, out double trunkRadius);
                    for (int k = 0; k < count; k++)
                    {
                        StandLayout.Place(row, col, StandLayout.Kind.Tuft, k, cellCm, out int eastCm, out int northCm, out int yaw);
                        double tuftEast = centreEast + eastCm / 100.0;
                        double tuftNorth = centreNorth + northCm / 100.0;
                        double dx = tuftEast - east, dz = tuftNorth - north;
                        if (dx * dx + dz * dz > reach2) continue;
                        if (trunkRadius > 0.0)
                        {
                            double tx = tuftEast - trunkEast, tz = tuftNorth - trunkNorth;
                            if (tx * tx + tz * tz < trunkRadius * trunkRadius) continue;
                        }
                        // Each tuft of a cell is its own size and its own shape of the six, from one more hash of the cell.
                        ulong h = StandLayout.Mix((((ulong)(uint)row << 32) | (uint)col) ^ ((ulong)StandLayout.Kind.Tuft << 56) ^ (uint)(k + 1));
                        double size = 0.8 + 0.4 * (((h >> 11) % 1024) / 1023.0);
                        into.Add(new UnderstoreyTuft
                        {
                            East = (float)tuftEast,
                            Up = (float)(TileGround.HeightAt(ground, tuftEast, tuftNorth) - SinkM),
                            North = (float)tuftNorth,
                            YawDeg = yaw,
                            HeightM = (float)(stands * size),
                            AcrossM = (float)(stands * size * across),
                            Shape = shape,
                            Variant = (int)((h >> 32) % (ulong)StandPreparation.Variants),
                        });
                    }
                }
        }

        /// <summary>
        /// How many cells of each cover lie within a distance of a point, in the tiles the client holds, counted into an
        /// array by the cover's own number (M1.6c's census probe): what the layer says grows round a vantage, to set
        /// beside what was drawn there.
        /// </summary>
        public static void CoverCensus(double east, double north, double radiusM, TileReceiver tiles, TileGrid grid, int[] cells)
        {
            if (tiles == null || grid == null || cells == null || !(radiusM > 0.0)) return;
            ReceivedTile here = tiles.Holding(TileLayer.GroundCover, grid.ForPosition(east, north));
            if (here?.Codes == null) return;
            double cell = here.CellM;
            if (!(cell > 0.0)) return;
            double reach2 = radiusM * radiusM;
            TileCodec.CellOf(grid.ExtentM, cell, east - radiusM, north + radiusM, out int rowMin, out int colMin);
            TileCodec.CellOf(grid.ExtentM, cell, east + radiusM, north - radiusM, out int rowMax, out int colMax);
            for (int row = rowMin; row <= rowMax; row++)
                for (int col = colMin; col <= colMax; col++)
                {
                    StandLayout.CellCentre(row, col, cell, grid.ExtentM, out double centreEast, out double centreNorth);
                    double dx = centreEast - east, dz = centreNorth - north;
                    if (dx * dx + dz * dz > reach2) continue;
                    ReceivedTile cover = tiles.Holding(TileLayer.GroundCover, grid.ForPosition(centreEast, centreNorth));
                    if (cover?.Codes == null) continue;
                    int x = (int)Math.Round((centreEast - cover.OriginEast) / cell);
                    int z = (int)Math.Round((centreNorth - cover.OriginNorth) / cell);
                    if (x < 0 || z < 0 || x >= cover.Posts || z >= cover.Posts) continue;
                    int which = (int)GroundCovers.CoverOf(cover.Codes[z, x]);
                    if (which >= 0 && which < cells.Length) cells[which]++;
                }
        }

        /// <summary>How many tufts stand on a cell: the whole of the density, and one more as often as its fraction, by the cell's own hash.</summary>
        public static int CountOn(int row, int col, double perCell)
        {
            if (!(perCell > 0.0)) return 0;
            int whole = (int)Math.Floor(perCell);
            double part = perCell - whole;
            ulong h = StandLayout.Mix((((ulong)(uint)row << 32) | (uint)col) ^ ((ulong)StandLayout.Kind.Tuft << 56));
            if (part > 0.0 && (((h >> 21) % 1024) / 1023.0) < part) whole++;
            return whole > MostPerCell ? MostPerCell : whole;
        }

        /// <summary>The quarter of the land's wetness a code carries, held inside the four there are.</summary>
        private static int Quarter(int quarter) => quarter < 0 ? 0 : quarter >= GroundCovers.Quarters ? GroundCovers.Quarters - 1 : quarter;

        /// <summary>Where the tree of a cell stands and how thick it is at the ground, or a radius of zero where no tree stands.</summary>
        private static void Trunk(ReceivedTile stand, ReceivedTile cover, int row, int col, int cellCm, double centreEast, double centreNorth,
                                  out double trunkEast, out double trunkNorth, out double radiusM)
        {
            trunkEast = 0.0;
            trunkNorth = 0.0;
            radiusM = 0.0;
            if (stand?.Codes == null || stand.Posts != cover.Posts) return;
            int x = (int)Math.Round((centreEast - stand.OriginEast) / stand.CellM);
            int z = (int)Math.Round((centreNorth - stand.OriginNorth) / stand.CellM);
            if (x < 0 || z < 0 || x >= stand.Posts || z >= stand.Posts) return;
            byte code = stand.Codes[z, x];
            if (code == 0) return;
            TreeForm form = StandForms.For(StandCodes.SpeciesOf(code));
            if (form == null) return;
            StandLayout.Place(row, col, StandLayout.Kind.Trunk, 0, cellCm, out int eastCm, out int northCm, out _);
            trunkEast = centreEast + eastCm / 100.0;
            trunkNorth = centreNorth + northCm / 100.0;
            radiusM = StandForms.TrunkRadiusAt(form, StandCodes.HeightOf(code), 0.0);
        }
    }
}
