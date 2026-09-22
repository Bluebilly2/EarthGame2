using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>One tuft of the understorey as the client draws it (M1.6c): where it stands, which way it faces, how tall and how wide, and which tuft of which cell it is (BF.3), so a verb can name it.</summary>
    public struct UnderstoreyTuft
    {
        public int Row;
        public int Col;
        public int Index;
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
        /// <summary>How many shapes there are, each drawn in <see cref="StandPreparation.Variants"/> variants; the rule's own count (<see cref="Tufts.Shapes"/>).</summary>
        public const int Shapes = Tufts.Shapes;

        /// <summary>The rule's own numbers (<see cref="Tufts"/>), kept here by name for the client's callers.</summary>
        public const double SmallestSize = Tufts.SmallestSize;
        public const double LargestSize = Tufts.LargestSize;
        public const double WetFootM = Tufts.WetFootM;
        public const double DeepestTuftM = Tufts.DeepestTuftM;
        public const double HerbPerSquareM = Tufts.HerbPerSquareM;
        public const double HerbHeightM = Tufts.HerbHeightM;
        public const int PatchCells = Tufts.PatchCells;
        public const int MostPerCell = Tufts.MostPerCell;

        /// <summary>How far a tuft's foot is set into the ground, m, so a slope shows no gap under it.</summary>
        public const double SinkM = 0.05;

        /// <summary>The rule's own (<see cref="Tufts.Grows"/>), kept here by name for the client's callers.</summary>
        public static bool Grows(GroundCover cover, out TuftShape shape, out double perSquareM, out double heightM) => Tufts.Grows(cover, out shape, out perSquareM, out heightM);

        public static double AcrossShare(TuftShape shape) => Tufts.AcrossShare(shape);
        public static double NaturalHeightM(TuftShape shape) => Tufts.NaturalHeightM(shape);
        public static bool Companion(GroundCover cover, out TuftShape shape, out double share) => Tufts.Companion(cover, out shape, out share);
        public static bool Stands(TuftShape shape, double waterDepthM) => Tufts.Stands(shape, waterDepthM);
        public static double PatchAt(int row, int col) => Tufts.PatchAt(row, col);
        public static double PerSquareMetreIn(double perSquareM, int quarter) => Tufts.PerSquareMetreIn(perSquareM, quarter);
        public static double HeightIn(double heightM, int quarter) => Tufts.HeightIn(heightM, quarter);

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
                    CellTufts grows = Tufts.OnCell(code, cell, row, col);
                    int count = grows.Count, herbs = grows.Herbs;
                    if (count + herbs <= 0) continue;
                    // Water standing over the cell's ground, where the client holds it: what stands in it is the shape's own rule.
                    // Read at a point by its own raster, so it need not share the cover's posts.
                    ReceivedTile depth = tiles.Holding(TileLayer.WaterDepth, id);
                    if (depth?.Heights == null) depth = null;
                    Trunk(tiles.Holding(TileLayer.Stand, id), cover, row, col, cellCm, centreEast, centreNorth,
                          out double trunkEast, out double trunkNorth, out double trunkRadius);
                    for (int k = 0; k < count + herbs; k++)
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
                        // Each tuft of a cell is its own size, its own shape of the six, and the companion at its share, from one
                        // more hash of the cell (the rule's, <see cref="Tufts.HashOf"/>); the herbs are the tail of the count.
                        ulong h = Tufts.HashOf(row, col, k);
                        TuftShape own = Tufts.ShapeOfIndex(grows, k, h);
                        double size = Tufts.SizeOf(h);
                        double tall = Tufts.HeightOf(grows, k, own, size) / size;
                        if (depth != null && !Tufts.Stands(own, TileGround.HeightAt(depth, tuftEast, tuftNorth))) continue;
                        into.Add(new UnderstoreyTuft
                        {
                            Row = row,
                            Col = col,
                            Index = k,
                            East = (float)tuftEast,
                            Up = (float)(TileGround.HeightAt(ground, tuftEast, tuftNorth) - SinkM),
                            North = (float)tuftNorth,
                            YawDeg = yaw,
                            HeightM = (float)(tall * size),
                            AcrossM = (float)(tall * size * AcrossShare(own)),
                            Shape = own,
                            Variant = Tufts.VariantOf(h, StandPreparation.Variants),
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

        /// <summary>The rule's own count (<see cref="Tufts.CountOn"/>), kept here by name.</summary>
        public static int CountOn(int row, int col, double perCell) => Tufts.CountOn(row, col, perCell);
        public static int CountOn(int row, int col, double perCell, int salt) => Tufts.CountOn(row, col, perCell, salt);

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
