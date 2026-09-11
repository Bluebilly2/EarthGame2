using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>
    /// What has been taken up of what lies loose (M1.5b): for each cell something was taken from, which of its sticks and
    /// which of its cobbles, by index. The loose layer a world was made with never changes; this is kept beside it, saved
    /// as the region files' layer diffs, and told to every client, so a thing taken is gone for good and the thing beside
    /// it keeps its place, which a lowered count could not promise. The server's world keeps one, and so does each client.
    /// </summary>
    public sealed class LooseTaken
    {
        /// <summary>One cell's takings: a bit for each index taken, the sticks and the cobbles apart.</summary>
        public struct Cell
        {
            public int Row;
            public int Col;
            public ushort Sticks;
            public ushort Cobbles;
        }

        private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();

        /// <summary>How many cells something has been taken from.</summary>
        public int Count => _cells.Count;

        public static long Key(int row, int col) => ((long)row << 32) | (uint)col;

        public bool TryGet(int row, int col, out Cell cell) => _cells.TryGetValue(Key(row, col), out cell);

        /// <summary>The bits of a kind on a cell's takings: its sticks' or its cobbles'.</summary>
        public static ushort MaskOf(in Cell cell, StandLayout.Kind kind) =>
            kind == StandLayout.Kind.Stick ? cell.Sticks : kind == StandLayout.Kind.Cobble ? cell.Cobbles : (ushort)0;

        public bool IsTaken(LyingThing thing) =>
            Holdable(thing) && _cells.TryGetValue(Key(thing.Row, thing.Col), out Cell cell) && (MaskOf(cell, thing.Kind) & (1 << thing.Index)) != 0;

        /// <summary>Marks a thing taken; false when it already was, or when it is no thing a cell could hold.</summary>
        public bool Take(LyingThing thing)
        {
            if (!Holdable(thing) || IsTaken(thing)) return false;
            long key = Key(thing.Row, thing.Col);
            _cells.TryGetValue(key, out Cell cell);
            cell.Row = thing.Row;
            cell.Col = thing.Col;
            ushort bit = (ushort)(1 << thing.Index);
            if (thing.Kind == StandLayout.Kind.Stick) cell.Sticks |= bit;
            else cell.Cobbles |= bit;
            _cells[key] = cell;
            return true;
        }

        /// <summary>
        /// A cell's takings as a save or the server states them, added to what is held: nothing taken is ever put back.
        /// A bit past the most a code counts is refused.
        /// </summary>
        public void Merge(Cell cell)
        {
            if (cell.Row < 0 || cell.Col < 0) throw new ArgumentException("no cell (" + cell.Row + ", " + cell.Col + ")", nameof(cell));
            if ((cell.Sticks >> LooseCodes.MaxEach) != 0 || (cell.Cobbles >> LooseCodes.MaxEach) != 0)
                throw new ArgumentException("cell (" + cell.Row + ", " + cell.Col + ") has a taking past the " + LooseCodes.MaxEach + " things a code counts", nameof(cell));
            if (cell.Sticks == 0 && cell.Cobbles == 0) return;
            long key = Key(cell.Row, cell.Col);
            if (_cells.TryGetValue(key, out Cell held))
            {
                cell.Sticks |= held.Sticks;
                cell.Cobbles |= held.Cobbles;
            }
            _cells[key] = cell;
        }

        /// <summary>Every cell something was taken from, by row and then column: the order the digest and the save walk.</summary>
        public List<Cell> Cells()
        {
            List<Cell> cells = new List<Cell>(_cells.Values);
            cells.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Col.CompareTo(b.Col));
            return cells;
        }

        /// <summary>Whether a thing is one a cell's code can count: a stick or a cobble, at an index below the most it holds.</summary>
        private static bool Holdable(LyingThing thing) =>
            (thing.Kind == StandLayout.Kind.Stick || thing.Kind == StandLayout.Kind.Cobble)
            && thing.Index >= 0 && thing.Index < LooseCodes.MaxEach && thing.Row >= 0 && thing.Col >= 0;
    }

    /// <summary>Where a thing of the loose layer lies and what it is (M1.5b), as the server holds the world.</summary>
    public static class LyingThings
    {
        /// <summary>
        /// Where a thing lies when the world's loose layer holds it and it has not been taken: its cell's centre moved by
        /// the layout, on the ground the server holds.
        /// </summary>
        public static bool TryFind(WorldState world, LyingThing thing, out Double3 at)
        {
            at = default;
            RegionRaster loose = world.Loose;
            if (loose == null || thing.Row < 0 || thing.Col < 0 || thing.Row >= loose.Height || thing.Col >= loose.Width) return false;
            if (thing.Index < 0 || thing.Index >= LooseCodes.CountOf((byte)loose.Code(thing.Row, thing.Col), thing.Kind)) return false;
            if (world.Taken.IsTaken(thing)) return false;
            StandLayout.CellCentre(thing.Row, thing.Col, loose.CellM, loose.ExtentM, out double east, out double north);
            StandLayout.Place(thing.Row, thing.Col, thing.Kind, thing.Index, (int)Math.Round(loose.CellM * 100.0), out int eastCm, out int northCm, out _);
            east += eastCm / 100.0;
            north += northCm / 100.0;
            at = new Double3(east, world.GroundAt(east, north), north);
            return true;
        }

        /// <summary>What a thing becomes when it is taken up: a stick a stick, and a cobble one of the stone its cell's stone layer names, the plain cobble where none is named.</summary>
        public static Definition DefinitionOf(WorldState world, LyingThing thing)
        {
            if (thing.Kind == StandLayout.Kind.Stick) return DefinitionCatalogue.Stick;
            RegionRaster stone = world.Stone;
            if (stone == null || thing.Row < 0 || thing.Col < 0 || thing.Row >= stone.Height || thing.Col >= stone.Width) return DefinitionCatalogue.Cobble;
            uint code = stone.Code(thing.Row, thing.Col);
            return code >= 1 && code <= StoneType.All.Count ? DefinitionCatalogue.CobbleOf(StoneType.All[(int)code - 1]) : DefinitionCatalogue.Cobble;
        }
    }
}
