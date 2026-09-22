using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>The layers a cell's change can be of (BF.3): a byte in the region file and the wire, never renumbered; the loose layer keeps its tile byte.</summary>
    public enum ChangeLayer : byte
    {
        Loose = 5,
        Tuft = 9,
        Trunk = 10,
        Ground = 11,
    }

    /// <summary>What has happened to the trunk on a cell: the bits of <see cref="CellChange.TrunkFlags"/>.</summary>
    public static class TrunkChange
    {
        public const byte BarkTaken = 1;
        public const byte LimbsTaken = 2;
        public const byte Felled = 4;
        public const byte All = BarkTaken | LimbsTaken | Felled;
    }

    /// <summary>What has happened to the ground of a cell: the bits of <see cref="CellChange.GroundFlags"/>.</summary>
    public static class GroundChange
    {
        public const byte Cleared = 1;
        public const byte All = Cleared;
    }

    /// <summary>
    /// One cell's changes, every layer at once (BF.3): the loose things taken (bits by index, as M1.5b's), the tufts taken
    /// (bits by index, sixteen a cell at most), the trunk's flags and the cut's progress, the ground's flags and how deep
    /// it is dug. A zero layer is one nothing happened to and is not written.
    /// </summary>
    public struct CellChange
    {
        public const byte LooseLayer = 1, TuftLayer = 2, TrunkLayer = 4, GroundLayer = 8, AllLayers = 15;

        public int Row;
        public int Col;
        public ushort Sticks;
        public ushort Cobbles;
        public ushort Tufts;
        public byte TrunkFlags;
        public byte TrunkCut;
        public byte GroundFlags;
        public byte DugCm;

        public bool HasLoose => (Sticks | Cobbles) != 0;
        public bool HasTuft => Tufts != 0;
        public bool HasTrunk => (TrunkFlags | TrunkCut) != 0;
        public bool HasGround => (GroundFlags | DugCm) != 0;
        public bool HasAny => HasLoose || HasTuft || HasTrunk || HasGround;

        /// <summary>The layers this cell's change has anything in, as the wire's mask.</summary>
        public byte Layers => (byte)((HasLoose ? LooseLayer : 0) | (HasTuft ? TuftLayer : 0) | (HasTrunk ? TrunkLayer : 0) | (HasGround ? GroundLayer : 0));

        /// <summary>The trunk's part of the change.</summary>
        public (byte Flags, byte Cut) Trunk => (TrunkFlags, TrunkCut);

        /// <summary>The ground's part of the change.</summary>
        public (byte Flags, byte DugCm) Ground => (GroundFlags, DugCm);

        /// <summary>The loose layer's part, as <see cref="LooseTaken"/> holds it.</summary>
        public LooseTaken.Cell Loose => new LooseTaken.Cell { Row = Row, Col = Col, Sticks = Sticks, Cobbles = Cobbles };

        /// <summary>Whether every number is one a cell could hold: no flag this build does not know, no tuft bit past the sixteen.</summary>
        public bool IsPossible(out string why)
        {
            if (Row < 0 || Col < 0) { why = "no cell (" + Row + ", " + Col + ")"; return false; }
            if ((TrunkFlags & ~TrunkChange.All) != 0) { why = "a trunk flag this build does not know: " + TrunkFlags; return false; }
            if ((GroundFlags & ~GroundChange.All) != 0) { why = "a ground flag this build does not know: " + GroundFlags; return false; }
            if ((Sticks >> LooseCodes.MaxEach) != 0 || (Cobbles >> LooseCodes.MaxEach) != 0) { why = "a taking past the " + LooseCodes.MaxEach + " things a code counts"; return false; }
            why = null;
            return true;
        }
    }

    /// <summary>
    /// What has changed in the generated world (BF.3, 2026-09-22): kept beside the layers, which never change, as
    /// changes by cell — the loose things taken (<see cref="LooseTaken"/>, which this owns and keeps as its view since
    /// M1.5b), the tufts taken, the trunks' yield and their cut, the ground cleared and dug. A taking is bits set and never
    /// cleared; a measure keeps the larger. Told to every client as one message, saved in the region files by layer with
    /// each diff's length, named by the digest, and restored into the world it was taken from. The server's world keeps
    /// one, and so does each client.
    /// </summary>
    public sealed class WorldChanges
    {
        /// <summary>The most tufts a cell's bits hold.</summary>
        public const int MostTufts = 16;

        private readonly Dictionary<long, CellChange> _cells = new Dictionary<long, CellChange>();

        /// <summary>The loose layer's takings, the view M1.5b's code reads and writes.</summary>
        public LooseTaken Loose { get; }

        public WorldChanges(LooseTaken loose)
        {
            Loose = loose ?? throw new ArgumentNullException(nameof(loose));
        }

        /// <summary>How many cells hold a change of the tuft, trunk or ground layers.</summary>
        public int Count => _cells.Count;

        private static long Key(int row, int col) => LooseTaken.Key(row, col);

        /// <summary>A cell's changes of every layer, the loose layer's bits included; false when nothing has happened to it.</summary>
        public bool TryGet(int row, int col, out CellChange cell)
        {
            bool any = _cells.TryGetValue(Key(row, col), out cell);
            if (Loose.TryGet(row, col, out LooseTaken.Cell loose))
            {
                cell.Row = row;
                cell.Col = col;
                cell.Sticks = loose.Sticks;
                cell.Cobbles = loose.Cobbles;
                any = true;
            }
            return any;
        }

        public bool IsTuftTaken(int row, int col, int index) =>
            index >= 0 && index < MostTufts && _cells.TryGetValue(Key(row, col), out CellChange c) && (c.Tufts & (1 << index)) != 0;

        /// <summary>Marks a tuft taken; false when it already was, or when no cell holds a tuft at that index.</summary>
        public bool TakeTuft(int row, int col, int index)
        {
            if (row < 0 || col < 0 || index < 0 || index >= MostTufts || IsTuftTaken(row, col, index)) return false;
            CellChange c = Own(row, col);
            c.Tufts |= (ushort)(1 << index);
            _cells[Key(row, col)] = c;
            return true;
        }

        public (byte Flags, byte Cut) TrunkOf(int row, int col) => _cells.TryGetValue(Key(row, col), out CellChange c) ? c.Trunk : ((byte)0, (byte)0);

        /// <summary>Sets the trunk's flags: bark taken, limbs taken, felled. Never cleared.</summary>
        public void MarkTrunk(int row, int col, byte flags)
        {
            if ((flags & ~TrunkChange.All) != 0) throw new ArgumentOutOfRangeException(nameof(flags), "a trunk flag this build does not know: " + flags);
            CellChange c = Own(row, col);
            c.TrunkFlags |= flags;
            _cells[Key(row, col)] = c;
        }

        /// <summary>The cut's progress on a trunk, 0 to 255; a cut is never undone, so a lesser value changes nothing.</summary>
        public void SetTrunkCut(int row, int col, byte progress)
        {
            CellChange c = Own(row, col);
            if (progress <= c.TrunkCut) return;
            c.TrunkCut = progress;
            _cells[Key(row, col)] = c;
        }

        public (byte Flags, byte DugCm) GroundOf(int row, int col) => _cells.TryGetValue(Key(row, col), out CellChange c) ? c.Ground : ((byte)0, (byte)0);

        /// <summary>Marks a cell's ground cleared of its understorey.</summary>
        public void Clear(int row, int col)
        {
            CellChange c = Own(row, col);
            c.GroundFlags |= GroundChange.Cleared;
            _cells[Key(row, col)] = c;
        }

        /// <summary>How deep a cell is dug, cm; a hole is not refilled, so a lesser depth changes nothing.</summary>
        public void Dig(int row, int col, byte depthCm)
        {
            CellChange c = Own(row, col);
            if (depthCm <= c.DugCm) return;
            c.DugCm = depthCm;
            _cells[Key(row, col)] = c;
        }

        private CellChange Own(int row, int col)
        {
            if (row < 0 || col < 0) throw new ArgumentOutOfRangeException(nameof(row), "no cell (" + row + ", " + col + ")");
            if (!_cells.TryGetValue(Key(row, col), out CellChange c))
            {
                c = default;
                c.Row = row;
                c.Col = col;
            }
            return c;
        }

        /// <summary>
        /// A cell's changes as a save or the server states them, added to what is held: takings by the bit, measures by the
        /// larger, the loose layer's part into <see cref="Loose"/>. A cell that could not be, or a flag this build does not
        /// know, is refused.
        /// </summary>
        public void Merge(CellChange cell)
        {
            if (!cell.IsPossible(out string why)) throw new ArgumentException("cell (" + cell.Row + ", " + cell.Col + "): " + why, nameof(cell));
            if (cell.HasLoose) Loose.Merge(cell.Loose);
            if (!cell.HasTuft && !cell.HasTrunk && !cell.HasGround) return;
            CellChange c = Own(cell.Row, cell.Col);
            c.Tufts |= cell.Tufts;
            c.TrunkFlags |= cell.TrunkFlags;
            if (cell.TrunkCut > c.TrunkCut) c.TrunkCut = cell.TrunkCut;
            c.GroundFlags |= cell.GroundFlags;
            if (cell.DugCm > c.DugCm) c.DugCm = cell.DugCm;
            _cells[Key(cell.Row, cell.Col)] = c;
        }

        /// <summary>Every cell with a change of the tuft, trunk or ground layers, by row and then column; the loose layer's bits are on each where it has them.</summary>
        public List<CellChange> Cells()
        {
            List<CellChange> cells = new List<CellChange>(_cells.Count);
            foreach (CellChange c in _cells.Values)
            {
                CellChange cell = c;
                if (Loose.TryGet(c.Row, c.Col, out LooseTaken.Cell loose))
                {
                    cell.Sticks = loose.Sticks;
                    cell.Cobbles = loose.Cobbles;
                }
                cells.Add(cell);
            }
            cells.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Col.CompareTo(b.Col));
            return cells;
        }

        /// <summary>Every cell anything has happened to, of any layer, by row and then column: what a joiner is told.</summary>
        public List<CellChange> AllCells()
        {
            Dictionary<long, CellChange> all = new Dictionary<long, CellChange>(_cells);
            foreach (LooseTaken.Cell loose in Loose.Cells())
            {
                long key = Key(loose.Row, loose.Col);
                if (!all.TryGetValue(key, out CellChange c))
                {
                    c = default;
                    c.Row = loose.Row;
                    c.Col = loose.Col;
                }
                c.Sticks = loose.Sticks;
                c.Cobbles = loose.Cobbles;
                all[key] = c;
            }
            List<CellChange> cells = new List<CellChange>(all.Values);
            cells.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Col.CompareTo(b.Col));
            return cells;
        }
    }
}
