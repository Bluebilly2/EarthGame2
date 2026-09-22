using System;
using System.Collections.Generic;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>A trunk standing near a point, where the client draws it (M1.6b): its foot, its height, how far up the trunk runs, and the radius a founder meets it at.</summary>
    public struct TrunkNearby
    {
        public int Row;
        public int Col;
        public PlantSpecies Species;
        public double East;
        public double Up;
        public double North;
        /// <summary>The whole tree's height, m, as its cell's code carries it.</summary>
        public double HeightM;
        /// <summary>How far up the trunk runs before the crown begins, m.</summary>
        public double TrunkM;
        /// <summary>The trunk's radius where a founder meets it, m.</summary>
        public double RadiusM;
    }

    /// <summary>
    /// The trunks round a point in the stand tiles a client holds (M1.6b), each placed where <see cref="StandPreparation"/>
    /// draws its tree and as thick as <see cref="StandForms.TrunkRadiusAt"/> draws it, so that what stops a founder stands
    /// where the bark is drawn. Nothing here collides: the Unity layer gives each of these a capsule for PhysX to stop a
    /// body against, as it draws the same trees from the same tiles.
    /// </summary>
    public static class TrunksNear
    {
        /// <summary>
        /// Every trunk whose foot lies within a distance of a point across the ground, added to a list, each with the radius
        /// it has <paramref name="atM"/> above its foot — the height at which a founder meets a tree.
        /// </summary>
        public static void Find(double east, double north, double radiusM, double atM, TileReceiver tiles, TileGrid grid, List<TrunkNearby> into)
        {
            if (tiles == null || grid == null || into == null || !(radiusM > 0.0)) return;
            ReceivedTile here = tiles.Holding(TileLayer.Stand, grid.ForPosition(east, north));
            if (here == null || here.Codes == null) return;
            double cell = here.CellM;
            if (!(cell > 0.0)) return;
            // A trunk stands up to half a cell from its cell's centre, so the cells half a cell beyond the reach are read too.
            double look = radiusM + cell;
            TileCodec.CellOf(grid.ExtentM, cell, east - look, north + look, out int rowMin, out int colMin);
            TileCodec.CellOf(grid.ExtentM, cell, east + look, north - look, out int rowMax, out int colMax);
            int cellCm = (int)Math.Round(cell * 100.0);
            double reach2 = radiusM * radiusM;
            for (int row = rowMin; row <= rowMax; row++)
                for (int col = colMin; col <= colMax; col++)
                {
                    StandLayout.CellCentre(row, col, cell, grid.ExtentM, out double centreEast, out double centreNorth);
                    TileId id = grid.ForPosition(centreEast, centreNorth);
                    ReceivedTile stand = tiles.Holding(TileLayer.Stand, id);
                    ReceivedTile ground = tiles.Holding(TileLayer.Ground, id);
                    if (stand?.Codes == null || ground?.Heights == null || ground.Posts != stand.Posts) continue;
                    int x = (int)Math.Round((centreEast - stand.OriginEast) / cell);
                    int z = (int)Math.Round((centreNorth - stand.OriginNorth) / cell);
                    if (x < 0 || z < 0 || x >= stand.Posts || z >= stand.Posts) continue;
                    byte code = stand.Codes[z, x];
                    if (code == 0) continue;
                    PlantSpecies species = StandCodes.SpeciesOf(code);
                    TreeForm form = StandForms.For(species);
                    if (form == null) continue;
                    StandLayout.Place(row, col, StandLayout.Kind.Trunk, 0, cellCm, out int eastCm, out int northCm, out _);
                    double footEast = centreEast + eastCm / 100.0;
                    double footNorth = centreNorth + northCm / 100.0;
                    double dx = footEast - east, dz = footNorth - north;
                    if (dx * dx + dz * dz > reach2) continue;
                    double height = StandCodes.HeightOf(code);
                    into.Add(new TrunkNearby
                    {
                        Row = row,
                        Col = col,
                        Species = species,
                        East = footEast,
                        Up = TileGround.HeightAt(ground, footEast, footNorth),
                        North = footNorth,
                        HeightM = height,
                        TrunkM = form.TrunkLength * height,
                        RadiusM = StandForms.TrunkRadiusAt(form, height, atM),
                    });
                }
        }
    }
}
