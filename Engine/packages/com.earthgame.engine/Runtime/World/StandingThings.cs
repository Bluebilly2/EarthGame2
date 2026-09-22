using System;

namespace EarthGame.Engine
{
    /// <summary>A trunk standing on a cell of the world, as the server finds it (BF.3): its plant, its height, its foot, and what has been done to it.</summary>
    public struct StandingTrunk
    {
        public int Row;
        public int Col;
        public PlantSpecies Species;
        public TreeGeometry Geometry;
        public double HeightM;
        public Double3 Foot;
        /// <summary>The bits of <see cref="TrunkChange"/> the world's changes hold for it.</summary>
        public byte Flags;
        /// <summary>The cut's progress, 0 to 255.</summary>
        public byte Cut;
        /// <summary>The quarter of the land's wetness its cell is in: how dry its bark is.</summary>
        public int Quarter;

        public bool BarkTaken => (Flags & TrunkChange.BarkTaken) != 0;
        public bool Felled => (Flags & TrunkChange.Felled) != 0;
    }

    /// <summary>A tuft of the understorey standing on a cell, as the server finds it (BF.3): where, what shape and plant, how tall.</summary>
    public struct StandingTuft
    {
        public int Row;
        public int Col;
        public int Index;
        public TuftShape Shape;
        /// <summary>The plant it is: the cell's understorey plant when it stands in this shape, else the shape's representative.</summary>
        public PlantSpecies Species;
        public double HeightM;
        public Double3 At;
        public int Quarter;
    }

    /// <summary>The ground of a cell as a work on it reads it (BF.3): its cover, its tufts and which are gone, its soil, the water over it, what grows in it.</summary>
    public struct GroundSite
    {
        public int Row;
        public int Col;
        public byte CoverCode;
        public GroundCover Cover;
        public int Quarter;
        public CellTufts Tufts;
        /// <summary>The tufts taken, bits by index (<see cref="WorldChanges"/>).</summary>
        public ushort TuftsTaken;
        /// <summary>
        /// The tufts the place keeps from standing, bits by index (BF.3, found on review 2026-09-23): inside the cell's trunk,
        /// or in water deeper than their shape stands in, as <see cref="StandingThings.TryFindTuft"/> finds them. The server
        /// fills it; a client, which holds no understorey and judges only an offer, leaves it empty.
        /// </summary>
        public ushort Absent;
        /// <summary>The cell's size, m: where each of its tufts stands inside it, and so where a clearing's bundles lie.</summary>
        public double CellM;
        public bool Cleared;
        public byte DugCm;
        public double SoilDepthM;
        /// <summary>Water standing over the cell's centre, m; zero on dry ground.</summary>
        public double WaterDepthM;
        /// <summary>The plant the understorey layer names on the cell, or null.</summary>
        public PlantSpecies Understory;
        public Double3 Centre;

        /// <summary>How many of the cover's own tufts still stand: none once the cell is cleared, and never more than the change bits can mark.</summary>
        public int TuftsLeft
        {
            get
            {
                if (Cleared) return 0;
                int left = 0;
                int count = Math.Min(Tufts.Count, WorldChanges.MostTufts);
                for (int k = 0; k < count; k++) if ((TuftsTaken & (1 << k)) == 0 && (Absent & (1 << k)) == 0) left++;
                return left;
            }
        }

        /// <summary>Whether the k-th tuft still stands on the cell.</summary>
        public bool Stands(int k) => !Cleared && k >= 0 && k < Math.Min(Tufts.Count, WorldChanges.MostTufts) && (TuftsTaken & (1 << k)) == 0 && (Absent & (1 << k)) == 0;
    }

    /// <summary>
    /// Standing things as targets (BF.3, 2026-09-23): a trunk of the stand layer and a tuft of the tuft rule, found from
    /// the world's own rasters and its changes, refused when gone, and handed to the work model as a plant's definition
    /// with a state of what the thing has of its own — a trunk its height and its girth, a tuft its height — and never
    /// stored: the layers stay what they were, and the changes say what is gone. The client finds the same trunks and
    /// tufts from its tiles (<c>TrunksNear</c>, <c>Understorey</c>) and makes their states by the same functions here.
    /// </summary>
    public static class StandingThings
    {
        /// <summary>Whether a cell lies inside a raster.</summary>
        private static bool Inside(RegionRaster r, int row, int col) => r != null && row >= 0 && col >= 0 && row < r.Height && col < r.Width;

        /// <summary>The trunk standing on a cell, unless none stands there or it has been felled.</summary>
        public static bool TryFindTrunk(WorldState world, int row, int col, out StandingTrunk trunk)
        {
            trunk = default;
            RegionRaster stand = world?.Stand;
            if (!Inside(stand, row, col)) return false;
            byte code = (byte)stand.Code(row, col);
            if (code == 0) return false;
            PlantSpecies species = StandCodes.SpeciesOf(code);
            TreeGeometry geometry = TreeGeometries.For(species);
            if (species == null || geometry == null) return false;
            (byte flags, byte cut) = world.Changes.TrunkOf(row, col);
            if ((flags & TrunkChange.Felled) != 0) return false;
            StandLayout.CellCentre(row, col, stand.CellM, stand.ExtentM, out double east, out double north);
            StandLayout.Place(row, col, StandLayout.Kind.Trunk, 0, (int)Math.Round(stand.CellM * 100.0), out int eastCm, out int northCm, out _);
            east += eastCm / 100.0;
            north += northCm / 100.0;
            trunk.Row = row;
            trunk.Col = col;
            trunk.Species = species;
            trunk.Geometry = geometry;
            trunk.HeightM = StandCodes.HeightOf(code);
            trunk.Foot = new Double3(east, world.GroundAt(east, north), north);
            trunk.Flags = flags;
            trunk.Cut = cut;
            trunk.Quarter = Inside(world.Cover, row, col) ? GroundCovers.QuarterOf((byte)world.Cover.Code(row, col)) : 0;
            return true;
        }

        /// <summary>
        /// The tuft of the understorey standing at an index on a cell, unless the cell grows fewer, it has been taken or the
        /// cell cleared, it stands inside its cell's trunk, or it is a herb, which no verb names. The same placement the
        /// client draws it by.
        /// </summary>
        public static bool TryFindTuft(WorldState world, int row, int col, int index, out StandingTuft tuft)
        {
            tuft = default;
            RegionRaster cover = world?.Cover;
            if (!Inside(cover, row, col) || index < 0) return false;
            byte code = (byte)cover.Code(row, col);
            CellTufts cell = Tufts.OnCell(code, cover.CellM, row, col);
            if (index >= cell.Count || index >= WorldChanges.MostTufts) return false;
            if (world.Changes.IsTuftTaken(row, col, index)) return false;
            if ((world.Changes.GroundOf(row, col).Flags & GroundChange.Cleared) != 0) return false;
            int cellCm = (int)Math.Round(cover.CellM * 100.0);
            StandLayout.CellCentre(row, col, cover.CellM, cover.ExtentM, out double centreEast, out double centreNorth);
            StandLayout.Place(row, col, StandLayout.Kind.Tuft, index, cellCm, out int eastCm, out int northCm, out _);
            double east = centreEast + eastCm / 100.0, north = centreNorth + northCm / 100.0;
            // A cell that carries a tree keeps its tufts out of the bark: the client draws none there, so none stands there.
            if (Inside(world.Stand, row, col))
            {
                byte standCode = (byte)world.Stand.Code(row, col);
                TreeGeometry geometry = TreeGeometries.For(StandCodes.SpeciesOf(standCode));
                if (standCode != 0 && geometry != null)
                {
                    StandLayout.Place(row, col, StandLayout.Kind.Trunk, 0, cellCm, out int te, out int tn, out _);
                    double radius = TreeGeometries.TrunkRadiusAt(geometry, StandCodes.HeightOf(standCode), 0.0);
                    double tx = east - (centreEast + te / 100.0), tz = north - (centreNorth + tn / 100.0);
                    if (tx * tx + tz * tz < radius * radius) return false;
                }
            }
            ulong hash = Tufts.HashOf(row, col, index);
            TuftShape own = Tufts.ShapeOfIndex(cell, index, hash);
            double size = Tufts.SizeOf(hash);
            // What stands in water is the shape's own rule, as the client draws it: nothing in the deep, only a sedge's clump in the shallows.
            world.WaterAt(east, north, out double waterDepth);
            if (!Tufts.Stands(own, waterDepth)) return false;
            tuft.Row = row;
            tuft.Col = col;
            tuft.Index = index;
            tuft.Shape = own;
            tuft.Species = Tufts.SpeciesOf(own, UnderstoryAt(world, row, col));
            tuft.HeightM = Tufts.HeightOf(cell, index, own, size);
            tuft.At = new Double3(east, world.GroundAt(east, north), north);
            tuft.Quarter = cell.Quarter;
            return true;
        }

        /// <summary>The plant the understorey layer names on a cell, or null where the world has no such layer or it names none.</summary>
        public static PlantSpecies UnderstoryAt(WorldState world, int row, int col)
        {
            RegionRaster understory = world?.Understory;
            if (!Inside(understory, row, col)) return null;
            uint code = understory.Code(row, col);
            return code >= 1 && code <= PlantSpecies.All.Count ? PlantSpecies.All[(int)code - 1] : null;
        }

        /// <summary>The ground of a cell as a work reads it; false for a cell outside the world's cover.</summary>
        public static bool TryGround(WorldState world, int row, int col, out GroundSite site)
        {
            site = default;
            RegionRaster cover = world?.Cover;
            if (!Inside(cover, row, col)) return false;
            byte code = (byte)cover.Code(row, col);
            site.Row = row;
            site.Col = col;
            site.CoverCode = code;
            site.Cover = GroundCovers.CoverOf(code);
            site.Quarter = GroundCovers.QuarterOf(code);
            site.Tufts = Tufts.OnCell(code, cover.CellM, row, col);
            site.CellM = cover.CellM;
            if (world.Changes.TryGet(row, col, out CellChange change))
            {
                site.TuftsTaken = change.Tufts;
                site.Cleared = (change.GroundFlags & GroundChange.Cleared) != 0;
                site.DugCm = change.DugCm;
            }
            // The tufts the place keeps from standing (inside the trunk, in water), found as a tuft is found, so a clearing counts
            // and bundles only what the client drew.
            if (!site.Cleared)
            {
                int most = Math.Min(site.Tufts.Count, WorldChanges.MostTufts);
                for (int k = 0; k < most; k++)
                    if ((site.TuftsTaken & (1 << k)) == 0 && !TryFindTuft(world, row, col, k, out _)) site.Absent |= (ushort)(1 << k);
            }
            site.SoilDepthM = Inside(world.SoilDepth, row, col) ? world.SoilDepth[row, col] : 0.0;
            site.Understory = UnderstoryAt(world, row, col);
            StandLayout.CellCentre(row, col, cover.CellM, cover.ExtentM, out double east, out double north);
            site.Centre = new Double3(east, world.GroundAt(east, north), north);
            world.WaterAt(east, north, out double depth);
            site.WaterDepthM = depth;
            return true;
        }

        /// <summary>
        /// A trunk as a work's target: the state of what it has of its own — its height as its length, its girth at breast
        /// height as its thickness, its bark as dry as its cell's quarter says, stripped when its bark is taken, and its
        /// soundness what the cut has left. The same on a client that reads the numbers off its tiles.
        /// </summary>
        public static ThingState TrunkState(PlantSpecies species, double heightM, byte flags, byte cut, int quarter)
        {
            ThingState s = default;
            TreeGeometry geometry = TreeGeometries.For(species);
            s.SetLength((float)heightM);
            s.SetDiameter((float)(2.0 * TreeGeometries.TrunkRadiusAt(geometry, heightM, TreeGeometries.BreastHeightM)));
            s.SetMoisture(LyingProperties.MoistureByQuarter[Tufts.Quarter(quarter)]);
            s.SetCondition((float)(1.0 - cut / 255.0));
            if ((flags & TrunkChange.BarkTaken) != 0) s.SetMarks(ThingMarks.Stripped);
            return s;
        }

        public static ThingState TrunkState(in StandingTrunk trunk) => TrunkState(trunk.Species, trunk.HeightM, trunk.Flags, trunk.Cut, trunk.Quarter);

        /// <summary>A tuft as a work's target: its height as its length, as wet as its ground.</summary>
        public static ThingState TuftState(double heightM, int quarter)
        {
            ThingState s = default;
            s.SetLength((float)heightM);
            s.SetMoisture(LyingProperties.MoistureByQuarter[Tufts.Quarter(quarter)]);
            return s;
        }

        public static ThingState TuftState(in StandingTuft tuft) => TuftState(tuft.HeightM, tuft.Quarter);

        /// <summary>A trunk's definition as a target: its plant's.</summary>
        public static Definition TrunkDefinition(PlantSpecies species) => DefinitionCatalogue.PlantOf(species);

        /// <summary>A tuft's definition as a target: its plant's; null for a herb, which is no target.</summary>
        public static Definition TuftDefinition(TuftShape shape, PlantSpecies species)
        {
            PlantSpecies plant = Tufts.SpeciesOf(shape, species);
            return plant == null ? null : DefinitionCatalogue.PlantOf(plant);
        }

        /// <summary>A standing trunk in a person's words: "a blackbutt, 22 m".</summary>
        public static string Describe(in StandingTrunk trunk) =>
            ThingWords.TrunkWords(trunk.Species, trunk.HeightM, trunk.BarkTaken, trunk.Cut);

        /// <summary>A standing tuft in a person's words: "a lomandra tuft", or by its shape when no plant is named.</summary>
        public static string Describe(in StandingTuft tuft) => ThingWords.TuftWords(tuft.Shape, tuft.Species);
    }
}
