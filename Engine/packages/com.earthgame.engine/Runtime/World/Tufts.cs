using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// The shapes the understorey stands in (M1.6c). The cover says what grows, not which plant it is — a grass tree
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
        /// <summary>A low herb between the others (M1.6e): a rosette of flat leaves, ankle-high.</summary>
        Herb = 4,
    }

    /// <summary>What grows on one cell by the tuft rule: how many of the cover's own tufts, how many herbs after them, and the shapes and heights they stand in.</summary>
    public struct CellTufts
    {
        /// <summary>The cover's own tufts, companions among them, indexed 0 to Count less one.</summary>
        public int Count;
        /// <summary>The herbs between them, indexed from Count on.</summary>
        public int Herbs;
        /// <summary>The cover's own shape.</summary>
        public TuftShape Shape;
        /// <summary>How tall the cover's own stand here, m, before each tuft's own size.</summary>
        public double StandsM;
        public bool Companions;
        public TuftShape Companion;
        /// <summary>What share of the tufts are the companion's shape.</summary>
        public double Share;
        public double CompanionStandsM;
        /// <summary>The quarter of the land's wetness the cell is in.</summary>
        public int Quarter;
    }

    /// <summary>
    /// The tuft rule (M1.6c, M1.6e; in the engine since BF.3, 2026-09-23): what grows underfoot, by the cover byte the
    /// server streams and the cell's own hashes, so the same tuft stands in the same centimetre in every client, in every
    /// run, and on the server that judges a founder's work on it. Nothing new travels: this is the cover byte read a
    /// second way. The client's <c>Understorey</c> draws by these numbers; the server's <c>StandingThings</c> finds a tuft by
    /// them. Until BF.3 the client alone held the rule, and a tuft was a thing no verb could name.
    /// </summary>
    public static class Tufts
    {
        /// <summary>How many shapes there are.</summary>
        public const int Shapes = 5;

        /// <summary>A tuft's size runs from this share of its cover's height (M1.6e; 0.8 before it) ...</summary>
        public const double SmallestSize = 0.6;

        /// <summary>... to this one (1.2 before).</summary>
        public const double LargestSize = 1.5;

        /// <summary>Water shallower than this over a tuft's foot is a damp foot, not water standing, m.</summary>
        public const double WetFootM = 0.02;

        /// <summary>The deepest water a margin's sedge stands in, m; nothing stands in deeper (M1.6e promise 2).</summary>
        public const double DeepestTuftM = 0.25;

        /// <summary>How many herbs stand on a square metre of any growing cover, between the cover's own tufts, and how tall they are, m.</summary>
        public const double HerbPerSquareM = 0.5;
        public const double HerbHeightM = 0.12;

        /// <summary>How many cells wide the patches of thick and thin growth are.</summary>
        public const int PatchCells = 5;

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

        /// <summary>How wide a shape stands for the height it stands at: a bush spreads, a tussock does not, a herb's rosette lies wider than it is tall.</summary>
        public static double AcrossShare(TuftShape shape) =>
            shape == TuftShape.Shrub ? 1.2 : shape == TuftShape.Frond ? 1.1 : shape == TuftShape.Clump ? 1.0 : shape == TuftShape.Herb ? 1.6 : 0.9;

        /// <summary>The height a shape stands at on its own cover, m, for a companion standing on another's.</summary>
        public static double NaturalHeightM(TuftShape shape) =>
            shape == TuftShape.Shrub ? 0.90 : shape == TuftShape.Frond ? 0.80 : shape == TuftShape.Clump ? 0.70 : shape == TuftShape.Herb ? HerbHeightM : 0.45;

        /// <summary>
        /// The shape that stands between a cover's own, and what share of the tufts it is (M1.6e promise 3): tussocks between the
        /// heath's shrubs and the bracken's fronds, clumps of sedge in the grass, tussocks in the sedge; a swamp's floor grows clumps alone.
        /// </summary>
        public static bool Companion(GroundCover cover, out TuftShape shape, out double share)
        {
            switch (cover)
            {
                case GroundCover.Heath: shape = TuftShape.Tussock; share = 0.30; return true;
                case GroundCover.Bracken: shape = TuftShape.Tussock; share = 0.25; return true;
                case GroundCover.Grass: shape = TuftShape.Clump; share = 0.15; return true;
                case GroundCover.Sedge: shape = TuftShape.Tussock; share = 0.20; return true;
                default: shape = TuftShape.Tussock; share = 0.0; return false;
            }
        }

        /// <summary>Whether a shape stands with water of a depth over its foot: anything on a damp foot, only a sedge's clump in the shallows, nothing in deeper.</summary>
        public static bool Stands(TuftShape shape, double waterDepthM) =>
            waterDepthM < WetFootM || (shape == TuftShape.Clump && waterDepthM <= DeepestTuftM);

        /// <summary>
        /// How thick a cell's growth is against its cover's density, 0.55 to 1.45 with 1 the mean: a smooth hash over the cell grid
        /// at <see cref="PatchCells"/>, so growth comes in patches, thick here and thin there, and never in speckle.
        /// </summary>
        public static double PatchAt(int row, int col)
        {
            int i = FloorDiv(row, PatchCells), j = FloorDiv(col, PatchCells);
            double u = (row - i * PatchCells) / (double)PatchCells, v = (col - j * PatchCells) / (double)PatchCells;
            u = u * u * (3.0 - 2.0 * u);
            v = v * v * (3.0 - 2.0 * v);
            double a = Corner(i, j), b = Corner(i + 1, j), c = Corner(i, j + 1), d = Corner(i + 1, j + 1);
            double noise = (a + (b - a) * u) + ((c + (d - c) * u) - (a + (b - a) * u)) * v;
            return 0.55 + 0.9 * noise;
        }

        private static double Corner(int i, int j) => ((StandLayout.Mix((((ulong)(uint)i << 32) | (uint)j) ^ 0x5A5A5A5A00000000UL) >> 11) % 1024) / 1023.0;

        private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        /// <summary>How many stand on a square metre of a cover in that quarter of the land's wetness: the wettest grows two-thirds more than the driest.</summary>
        public static double PerSquareMetreIn(double perSquareM, int quarter) => perSquareM * (0.75 + 0.17 * Quarter(quarter));

        /// <summary>How tall they stand there: the wettest quarter grows them a third taller than the driest.</summary>
        public static double HeightIn(double heightM, int quarter) => heightM * (0.85 + 0.10 * Quarter(quarter));

        /// <summary>How many tufts stand on a cell: the whole of the density, and one more as often as its fraction, by the cell's own hash.</summary>
        public static int CountOn(int row, int col, double perCell) => CountOn(row, col, perCell, 0);

        /// <summary>The same, salted, so a cell's herbs are counted apart from its tufts.</summary>
        public static int CountOn(int row, int col, double perCell, int salt)
        {
            if (!(perCell > 0.0)) return 0;
            int whole = (int)Math.Floor(perCell);
            double part = perCell - whole;
            ulong h = StandLayout.Mix((((ulong)(uint)row << 32) | (uint)col) ^ ((ulong)StandLayout.Kind.Tuft << 56) ^ ((ulong)(uint)salt << 48));
            if (part > 0.0 && (((h >> 21) % 1024) / 1023.0) < part) whole++;
            return whole > MostPerCell ? MostPerCell : whole;
        }

        /// <summary>The quarter of the land's wetness a code carries, held inside the four there are.</summary>
        public static int Quarter(int quarter) => quarter < 0 ? 0 : quarter >= GroundCovers.Quarters ? GroundCovers.Quarters - 1 : quarter;

        /// <summary>
        /// What grows on a cell by its cover byte and its size: the cover's own tufts, in patches (M1.6e), and the herbs
        /// between them, with the shapes and heights each stands in. A cover that grows nothing has a count of zero.
        /// </summary>
        public static CellTufts OnCell(byte coverCode, double cellM, int row, int col)
        {
            CellTufts cell = default;
            GroundCover kind = GroundCovers.CoverOf(coverCode);
            if (!Grows(kind, out TuftShape shape, out double perSquareM, out double heightM)) return cell;
            int quarter = GroundCovers.QuarterOf(coverCode);
            cell.Quarter = quarter;
            cell.Shape = shape;
            cell.Count = CountOn(row, col, PerSquareMetreIn(perSquareM, quarter) * cellM * cellM * PatchAt(row, col));
            cell.Herbs = CountOn(row, col, HerbPerSquareM * cellM * cellM, 1);
            cell.StandsM = HeightIn(heightM, quarter);
            cell.Companions = Companion(kind, out TuftShape companion, out double share);
            cell.Companion = companion;
            cell.Share = share;
            cell.CompanionStandsM = HeightIn(NaturalHeightM(companion), quarter);
            return cell;
        }

        /// <summary>One more hash of the cell for its k-th tuft: its shape among the cover's, its size, its variant, all from this.</summary>
        public static ulong HashOf(int row, int col, int k) =>
            StandLayout.Mix((((ulong)(uint)row << 32) | (uint)col) ^ ((ulong)StandLayout.Kind.Tuft << 56) ^ (uint)(k + 1));

        /// <summary>The shape the k-th tuft of a cell stands in: a herb past the count, else the companion at its share, else the cover's own.</summary>
        public static TuftShape ShapeOfIndex(in CellTufts cell, int k, ulong hash) =>
            k >= cell.Count ? TuftShape.Herb : cell.Companions && ((hash >> 44) % 1024) / 1023.0 < cell.Share ? cell.Companion : cell.Shape;

        /// <summary>A tuft's own size, as a share of its shape's height here, from its hash.</summary>
        public static double SizeOf(ulong hash) => SmallestSize + (LargestSize - SmallestSize) * (((hash >> 11) % 1024) / 1023.0);

        /// <summary>How tall the k-th tuft stands, m: its shape's height on this cell times its own size.</summary>
        public static double HeightOf(in CellTufts cell, int k, TuftShape own, double size) =>
            (k >= cell.Count ? HerbHeightM : own == cell.Shape ? cell.StandsM : cell.CompanionStandsM) * size;

        /// <summary>Which drawn variant of its shape a tuft is, of however many the client draws.</summary>
        public static int VariantOf(ulong hash, int variants) => variants <= 0 ? 0 : (int)((hash >> 32) % (ulong)variants);

        /// <summary>The shape a plant of the understorey stands in, or null for one that stands as a tree.</summary>
        public static TuftShape? ShapeOf(PlantSpecies species)
        {
            if (species == null) return null;
            if (ReferenceEquals(species, PlantSpecies.Bracken)) return TuftShape.Frond;
            switch (species.Form)
            {
                case PlantForm.Grass: return TuftShape.Tussock;
                case PlantForm.Herb: return TuftShape.Clump;
                case PlantForm.Shrub: return TuftShape.Shrub;
                default: return null;
            }
        }

        /// <summary>
        /// The plant a shape stands for when the cell's own understorey plant is not of that shape, or when only the cover
        /// is known (a client, which holds the cover byte and not the understorey layer): the commonest of its kind here.
        /// A herb stands for no plant.
        /// </summary>
        public static PlantSpecies RepresentativeOf(TuftShape shape)
        {
            switch (shape)
            {
                case TuftShape.Clump: return PlantSpecies.Lomandra;
                case TuftShape.Tussock: return PlantSpecies.KangarooGrass;
                case TuftShape.Frond: return PlantSpecies.Bracken;
                case TuftShape.Shrub: return PlantSpecies.HeathBanksia;
                default: return null;
            }
        }

        /// <summary>The plant a tuft is: the cell's understorey plant when it stands in this shape, else the shape's representative.</summary>
        public static PlantSpecies SpeciesOf(TuftShape shape, PlantSpecies understory) =>
            understory != null && ShapeOf(understory) == shape ? understory : RepresentativeOf(shape);

        /// <summary>A shape in a person's words.</summary>
        public static string NameOf(TuftShape shape)
        {
            switch (shape)
            {
                case TuftShape.Shrub: return "a heath bush";
                case TuftShape.Clump: return "a sedge clump";
                case TuftShape.Frond: return "a bracken frond";
                case TuftShape.Tussock: return "a grass tussock";
                default: return "a herb";
            }
        }

        /// <summary>Whether hands pull a shape up whole: a tussock, a frond or a clump; a bush is woody and a herb is nothing to hold.</summary>
        public static bool Pullable(TuftShape shape) => shape == TuftShape.Tussock || shape == TuftShape.Frond || shape == TuftShape.Clump;
    }
}
