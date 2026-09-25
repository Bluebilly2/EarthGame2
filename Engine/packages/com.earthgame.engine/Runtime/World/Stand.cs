using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>
    /// The code a cell of the world's stand layer carries (M1.6a): zero where no trunk stands; else which tall plant stands
    /// there, by its number in the plant catalogue (<see cref="PlantSpecies.NumberOf"/>, the overstory's own number), in the
    /// high byte, and how tall it is, in steps of <see cref="HeightStepM"/>, in the low.
    ///
    /// <para>A trunk is a fact of the world rather than an entity (ARCHITECTURE §5, 2026-09-10). The world places its
    /// trees when it is created and keeps them as a layer, which travels in code tiles as the ground's cover does, and
    /// a tree becomes an entity only when something changes it. At the stand's densities a tree an entity would be
    /// tens of thousands of entities round every player, each sent at the join with no budget; a tree two bytes is a
    /// few kilobytes a tile.</para>
    ///
    /// <para>Two bytes since WG.2c (2026-09-25). From M1.6a the code was one byte: the plant in its top three bits, by its
    /// place among the tall plants, and the height in its low five. Seven plants were all it could name, where the Kangaroo
    /// Valley's own trees make ten, and 31 steps ended at 38.75 m, short of the blackbutt's 40, so the tallest blackbutts were
    /// stored and spaced at 38.75. The five plants it named are the catalogue's first five, so an old code's plant number is
    /// already the catalogue's and its steps are these: <see cref="FromOneByte"/> is exact, and a world made before reads its
    /// stand through <see cref="CodeAt"/> with its file left as it was made.</para>
    /// </summary>
    public static class StandCodes
    {
        /// <summary>One step of the height a code carries, m.</summary>
        public const double HeightStepM = 1.25;

        /// <summary>The low byte: the height, in steps; 255 of them reach 318.75 m, past any tree.</summary>
        public const int HeightMask = 0xFF;

        /// <summary>The high byte: the plant's number in the catalogue.</summary>
        public const int SpeciesShift = 8;

        private static readonly PlantSpecies[] TallPlants = BuildTall();

        /// <summary>Each catalogue number's place in <see cref="Tall"/>, -1 for zero and for a plant that is not tall.</summary>
        private static readonly int[] TallByNumber = BuildTallByNumber();

        /// <summary>
        /// The plants that stand as trees, in the catalogue's order. The client keeps its forms and meshes by the place here
        /// (<see cref="TallIndexOf"/>); a code names a plant by its catalogue number, not by this place.
        /// </summary>
        public static IReadOnlyList<PlantSpecies> Tall => TallPlants;

        /// <summary>Whether a plant stands as a tree: the tall forms, which the canopy is drawn from.</summary>
        public static bool IsTall(PlantSpecies species) => species != null && (species.Form == PlantForm.Tree || species.Form == PlantForm.SmallTree);

        /// <summary>The code for a tree of this species and height; the height is carried to the nearest step, at least one.</summary>
        public static ushort Pack(PlantSpecies species, double heightM)
        {
            if (!IsTall(species) || PlantSpecies.NumberOf(species) == 0)
                throw new ArgumentException((species == null ? "nothing" : species.Name) + " does not stand as a tree", nameof(species));
            int step = (int)Math.Round(heightM / HeightStepM);
            if (step < 1) step = 1;
            if (step > HeightMask) step = HeightMask;
            return (ushort)((PlantSpecies.NumberOf(species) << SpeciesShift) | step);
        }

        /// <summary>The tall plant a code names, or null where none stands or the code names no tall plant this build knows.</summary>
        public static PlantSpecies SpeciesOf(ushort code)
        {
            int index = TallIndexOf(code);
            return index >= 0 ? TallPlants[index] : null;
        }

        /// <summary>The place in <see cref="Tall"/> of the plant a code names, or -1 where none stands or it names no tall plant.</summary>
        public static int TallIndexOf(ushort code) => TallByNumber[code >> SpeciesShift];

        /// <summary>The height a code carries, m; zero for a cell where nothing stands.</summary>
        public static double HeightOf(ushort code) => code == 0 ? 0.0 : (code & HeightMask) * HeightStepM;

        /// <summary>
        /// A code of the one-byte layout (M1.6a to WG.2c) in this one: the plant number in its top three bits is the catalogue's
        /// already, and its five bits of height are the same steps, so every one of the 256 converts exactly and a code that
        /// was zero stays zero.
        /// </summary>
        public static ushort FromOneByte(byte code) => (ushort)(((code >> 5) << SpeciesShift) | (code & 0x1F));

        /// <summary>Whether a world's stand layer holds the one-byte layout: a world made before WG.2c (2026-09-25).</summary>
        public static bool IsOneByte(RegionRaster stand) => stand != null && stand.Dtype == "u8";

        /// <summary>
        /// The code at a cell of a world's stand layer, in this layout whichever the world was made with: a layer of one byte a
        /// cell is converted as it is read (<see cref="FromOneByte"/>), and its file stays as it was made.
        /// </summary>
        public static ushort CodeAt(RegionRaster stand, int row, int col)
        {
            uint raw = stand.Code(row, col);
            if (IsOneByte(stand)) return FromOneByte((byte)raw);
            if (raw > ushort.MaxValue)
                throw new InvalidDataException("the stand layer holds the code " + raw + " at row " + row + " col " + col + ", which neither layout carries");
            return (ushort)raw;
        }

        /// <summary>The legend a stand layer's sidecar carries, so a reader needs nothing but the file.</summary>
        public static string Legend()
        {
            StringBuilder sb = new StringBuilder("zero where no trunk stands; else the high byte is the tall plant by its number in the plant catalogue, the overstory's own (");
            for (int i = 0; i < TallPlants.Length; i++)
                sb.Append(i > 0 ? ", " : "").Append(PlantSpecies.NumberOf(TallPlants[i])).Append('=').Append(TallPlants[i].Name);
            sb.Append("), and the low byte its height in steps of ")
              .Append(HeightStepM.ToString("0.00", CultureInfo.InvariantCulture)).Append(" m");
            return sb.ToString();
        }

        private static PlantSpecies[] BuildTall()
        {
            List<PlantSpecies> tall = new List<PlantSpecies>();
            foreach (PlantSpecies species in PlantSpecies.All)
                if (IsTall(species)) tall.Add(species);
            return tall.ToArray();
        }

        private static int[] BuildTallByNumber()
        {
            int[] byNumber = new int[1 << (16 - SpeciesShift)];
            for (int n = 0; n < byNumber.Length; n++) byNumber[n] = Array.IndexOf(TallPlants, PlantSpecies.ByNumber(n));
            return byNumber;
        }
    }

    /// <summary>
    /// The byte a cell of the world's loose layer carries (M1.6a): how many sticks lie on it, in the low four bits, and
    /// how many cobbles, in the high four. Which stone a cobble is, is the stone layer's at the same cell.
    /// </summary>
    public static class LooseCodes
    {
        /// <summary>The most of either a cell's code can count.</summary>
        public const int MaxEach = 15;

        public static byte Pack(int sticks, int cobbles) => (byte)(Clamp(sticks) | (Clamp(cobbles) << 4));

        public static int SticksOf(byte code) => code & 0x0F;

        public static int CobblesOf(byte code) => code >> 4;

        /// <summary>How many things of a kind lie on a cell by its code: its sticks or its cobbles, and nothing of any other kind.</summary>
        public static int CountOf(byte code, StandLayout.Kind kind) =>
            kind == StandLayout.Kind.Stick ? SticksOf(code) : kind == StandLayout.Kind.Cobble ? CobblesOf(code) : 0;

        /// <summary>The legend a loose layer's sidecar carries.</summary>
        public static string Legend() => "the low four bits are how many sticks lie on the cell and the high four how many cobbles, of the stone layer's stone, each to " + MaxEach;

        private static int Clamp(int count) => count < 0 ? 0 : count > MaxEach ? MaxEach : count;
    }

    /// <summary>
    /// Where exactly inside its cell a generated thing lies (M1.6a): the trunk that stands on a cell, and the k-th stick
    /// or cobble lying on it. Whole numbers only — a hash of the cell's row and column, the kind and the index — so the
    /// server that made the world and a client drawing it put each thing at the same centimetre however each runtime
    /// rounds a double, and a later verb can name the second stick of a cell.
    /// </summary>
    public static class StandLayout
    {
        public enum Kind : byte
        {
            Trunk = 1,
            Stick = 2,
            Cobble = 3,
            /// <summary>A tuft of the understorey (M1.6c): drawn by the client from the cover, never taken, and no taking names one.</summary>
            Tuft = 4,
        }

        /// <summary>How many shapes a lying thing can be drawn in (BF.1): the client draws this many variants of a stick and a cobble, and a thing's look names one.</summary>
        public const int Looks = 6;

        /// <summary>
        /// The shape a thing lying at this address is drawn in, 0 to <see cref="Looks"/> less one (BF.1; the rule the client's
        /// preparation has used since M1.6a): a hash of the cell, the kind and the index. A thing taken up keeps it as its look.
        /// </summary>
        public static int LookOf(int row, int col, Kind kind, int index) =>
            (int)(Mix((((ulong)(uint)row << 32) | (uint)col) ^ (((((ulong)kind) << 8) | (uint)(index + 1)) << 40)) % (ulong)Looks);

        /// <summary>
        /// A thing's offset from its cell's centre, whole centimetres east and north, each inside the cell, and its yaw,
        /// whole degrees clockwise from north.
        /// </summary>
        public static void Place(int row, int col, Kind kind, int index, int cellCm, out int eastCm, out int northCm, out int yawDeg)
        {
            if (cellCm <= 0) throw new ArgumentOutOfRangeException(nameof(cellCm), "a cell is " + cellCm + " cm");
            ulong h = Mix(((ulong)(uint)row << 32) | (uint)col);
            h = Mix(h ^ ((ulong)kind << 56) ^ (uint)index);
            eastCm = (int)(h % (ulong)cellCm) - cellCm / 2;
            northCm = (int)((h >> 21) % (ulong)cellCm) - cellCm / 2;
            yawDeg = (int)((h >> 42) % 360UL);
        }

        /// <summary>
        /// A cell's centre in local metres, by the raster's own rule (ARCHITECTURE §10): column 0 at the west edge, row 0
        /// at the north. A thing's place is this moved by <see cref="Place"/>; a streamed tile's post for the cell stands
        /// here too.
        /// </summary>
        public static void CellCentre(int row, int col, double cellM, double extentM, out double east, out double north)
        {
            double half = extentM * 0.5;
            east = col * cellM - half;
            north = half - row * cellM;
        }

        /// <summary>splitmix64's finaliser: every bit of what goes in reaches every bit of what comes out.</summary>
        public static ulong Mix(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    /// <summary>
    /// One thing lying in the world's loose layer, by its place (M1.5b): its cell's row and column, whether a stick or a
    /// cobble, and which of the cell's things of that kind, counting from 0. Both ends find where it lies from this alone
    /// (<see cref="StandLayout"/>), so a founder can take the second stick of a cell and the server knows which is meant.
    /// </summary>
    public readonly struct LyingThing : IEquatable<LyingThing>
    {
        public readonly int Row;
        public readonly int Col;
        public readonly StandLayout.Kind Kind;
        public readonly int Index;

        public LyingThing(int row, int col, StandLayout.Kind kind, int index)
        {
            Row = row;
            Col = col;
            Kind = kind;
            Index = index;
        }

        public bool Equals(LyingThing other) => Row == other.Row && Col == other.Col && Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is LyingThing other && Equals(other);
        public override int GetHashCode() => (int)StandLayout.Mix(((ulong)(uint)Row << 32 | (uint)Col) ^ ((ulong)Kind << 56) ^ (uint)Index);
        public override string ToString() => Kind.ToString().ToLowerInvariant() + " " + Index + " of cell (" + Row + ", " + Col + ")";
    }
}
