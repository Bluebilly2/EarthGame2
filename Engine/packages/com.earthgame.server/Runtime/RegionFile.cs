using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.Engine;
using EarthGame.Protocol;

namespace EarthGame.Server
{
    /// <summary>An entity as a region file records it: everything the store needs to restore it.</summary>
    public struct SavedEntity
    {
        public ulong Id;
        public string Key;
        public Double3 Position;
        public float YawDeg;
        public long SpawnTick;
        public bool HasItem;
        public ItemComponent Item;

        public static SavedEntity Of(Entity e)
        {
            SavedEntity s;
            s.Id = e.Id.Value;
            s.Key = e.Definition.Key;
            s.Position = e.Position;
            s.YawDeg = e.YawDeg;
            s.SpawnTick = e.SpawnTick;
            s.HasItem = e.HasItem;
            s.Item = e.Item;
            return s;
        }
    }

    /// <summary>
    /// The region file, format <c>eg2.region</c> version 2 (ARCHITECTURE §6 and §10): one per 512 m cell
    /// (<see cref="RegionCells"/>), holding the entities whose position lies in the cell and, since version 2 (M1.5b),
    /// the layer diffs of the raster cells whose centres lie in it. Little-endian: the magic <c>EG2R</c>, u16 version,
    /// i32 cell x, i32 cell z, f64 cell size, u32 entity count, u32 layer-diff count, u32 CRC-32 of everything that
    /// follows, then per entity: u64 id, the key as a u16 UTF-8 byte length and the bytes, f64 east, up and north, f32
    /// yaw, i64 spawn tick, u8 component mask (1 = item), and for an item u8 resting and f32 fall speed; then per diff:
    /// u8 the layer by its tile byte (5, the loose layer, the only one yet), u16 row and u16 column of the world's
    /// raster, and for the loose layer u16 the sticks taken and u16 the cobbles taken, a bit for each index. Version 1
    /// had no diffs and is still read. The server writes and reads it; save_check.py restates the layout in Python and
    /// reads it too.
    /// </summary>
    public static class RegionFile
    {
        public const string Folder = "regions";
        public const ushort Version = 2;
        public const int HeaderBytes = 4 + 2 + 4 + 4 + 8 + 4 + 4 + 4;
        private static readonly byte[] Magic = { (byte)'E', (byte)'G', (byte)'2', (byte)'R' };

        public static string NameFor(int cellX, int cellZ) => "r." + cellX + "." + cellZ + ".egr";

        public static bool TryParseName(string fileName, out int cellX, out int cellZ)
        {
            cellX = cellZ = 0;
            if (fileName == null || !fileName.StartsWith("r.", StringComparison.Ordinal) || !fileName.EndsWith(".egr", StringComparison.Ordinal)) return false;
            string[] parts = fileName.Substring(2, fileName.Length - 6).Split('.');
            return parts.Length == 2 && int.TryParse(parts[0], out cellX) && int.TryParse(parts[1], out cellZ);
        }

        public static byte[] Encode(int cellX, int cellZ, IReadOnlyList<SavedEntity> entities, IReadOnlyList<LooseTaken.Cell> taken = null)
        {
            int diffs = taken != null ? taken.Count : 0;
            PacketWriter records = new PacketWriter(64 + 64 * entities.Count + 9 * diffs);
            for (int i = 0; i < entities.Count; i++)
            {
                SavedEntity e = entities[i];
                records.WriteUInt64(e.Id);
                records.WriteString(e.Key);
                records.WriteDouble(e.Position.X);
                records.WriteDouble(e.Position.Y);
                records.WriteDouble(e.Position.Z);
                records.WriteSingle(e.YawDeg);
                records.WriteInt64(e.SpawnTick);
                records.WriteByte(e.HasItem ? EntityWire.ComponentItem : (byte)0);
                if (e.HasItem) EntityWire.WriteItem(records, e.Item);
            }
            for (int i = 0; i < diffs; i++)
            {
                LooseTaken.Cell c = taken[i];
                if (c.Row < 0 || c.Row > ushort.MaxValue || c.Col < 0 || c.Col > ushort.MaxValue)
                    throw new ArgumentException("cell (" + c.Row + ", " + c.Col + ") does not fit a region file", nameof(taken));
                records.WriteByte((byte)TileLayer.Loose);
                records.WriteUInt16((ushort)c.Row);
                records.WriteUInt16((ushort)c.Col);
                records.WriteUInt16(c.Sticks);
                records.WriteUInt16(c.Cobbles);
            }
            byte[] body = records.Written.ToArray();
            PacketWriter head = new PacketWriter(HeaderBytes);
            for (int i = 0; i < Magic.Length; i++) head.WriteByte(Magic[i]);
            head.WriteUInt16(Version);
            head.WriteInt32(cellX);
            head.WriteInt32(cellZ);
            head.WriteDouble(RegionCells.CellM);
            head.WriteUInt32((uint)entities.Count);
            head.WriteUInt32((uint)diffs);
            head.WriteUInt32(Crc32.Compute(body));
            byte[] file = new byte[HeaderBytes + body.Length];
            head.Written.CopyTo(file);
            Buffer.BlockCopy(body, 0, file, HeaderBytes, body.Length);
            return file;
        }

        /// <summary>
        /// Reads a region file: its entities, and its layer diffs as the loose layer's takings; refuses a wrong magic,
        /// another version, a wrong CRC, a diff of a layer this build does not know, or a short record, with the reason.
        /// </summary>
        public static List<SavedEntity> Decode(byte[] bytes, out int cellX, out int cellZ, out List<LooseTaken.Cell> taken)
        {
            if (bytes == null || bytes.Length < HeaderBytes) throw new InvalidDataException("a region file is at least " + HeaderBytes + " bytes");
            for (int i = 0; i < Magic.Length; i++)
                if (bytes[i] != Magic[i]) throw new InvalidDataException("not a region file (magic)");
            PacketReader head = new PacketReader(bytes, 4, HeaderBytes - 4);
            ushort version = head.ReadUInt16();
            if (version != Version && version != 1) throw new InvalidDataException("region file version " + version + "; this build reads 1 and " + Version);
            cellX = head.ReadInt32();
            cellZ = head.ReadInt32();
            double cellM = head.ReadDouble();
            if (Math.Abs(cellM - RegionCells.CellM) > 1e-9) throw new InvalidDataException("region file cells are " + cellM + " m; this build keeps " + RegionCells.CellM);
            uint count = head.ReadUInt32();
            uint diffs = head.ReadUInt32();
            if (version == 1 && diffs != 0) throw new InvalidDataException("a version-1 region file carries " + diffs + " layer diffs; version 1 had none");
            uint crc = head.ReadUInt32();
            uint actual = Crc32.Compute(bytes, HeaderBytes, bytes.Length - HeaderBytes);
            if (actual != crc) throw new InvalidDataException("region file CRC " + actual.ToString("x8") + " differs from the stated " + crc.ToString("x8"));
            List<SavedEntity> entities = new List<SavedEntity>((int)count);
            PacketReader r = new PacketReader(bytes, HeaderBytes, bytes.Length - HeaderBytes);
            for (uint i = 0; i < count; i++)
            {
                SavedEntity e;
                e.Id = r.ReadUInt64();
                e.Key = r.ReadString();
                e.Position = new Double3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
                e.YawDeg = r.ReadSingle();
                e.SpawnTick = r.ReadInt64();
                byte components = r.ReadByte();
                if ((components & ~EntityWire.ComponentItem) != 0) throw new InvalidDataException("entity " + e.Id + " carries components this build does not know: " + components);
                e.HasItem = (components & EntityWire.ComponentItem) != 0;
                e.Item = e.HasItem ? EntityWire.ReadItem(r) : default;
                entities.Add(e);
            }
            taken = new List<LooseTaken.Cell>((int)Math.Min(diffs, 4096u));
            for (uint i = 0; i < diffs; i++)
            {
                byte layer = r.ReadByte();
                if (layer != (byte)TileLayer.Loose) throw new InvalidDataException("region file carries a diff of layer " + layer + ", which this build does not know");
                LooseTaken.Cell c;
                c.Row = r.ReadUInt16();
                c.Col = r.ReadUInt16();
                c.Sticks = r.ReadUInt16();
                c.Cobbles = r.ReadUInt16();
                taken.Add(c);
            }
            r.ExpectEnd();
            return entities;
        }
    }

    /// <summary>
    /// A player's resting place, format <c>eg2.player</c> version 4 (binary; versions 2 and 3 are still read, and version
    /// 1, JSON, too): the magic <c>EG2P</c>, u16 version, the name as a u16 UTF-8 byte length and the bytes, f64 east, up
    /// and north, f32 yaw, f32 pitch, u8 flags (1 grounded, 2 wading, 4 crouching, as <see cref="BodyWire"/> packs
    /// them), i64 saved tick; since version 3 (M1.5a) the hand's place as a u8, and what is carried as a u8 count and,
    /// per thing, u8 place, u64 id, the key as a u16 UTF-8 byte length and the bytes, and i64 spawn tick; since version 4
    /// (FP.1) f64 the fraction of body water lost; then u32 CRC-32 of everything before it. Version 1 dropped wading and
    /// stance, which the round trip's digest needs; version 2 had no hands; version 3 no water.
    /// </summary>
    public static class PlayerFile
    {
        public const ushort Version = 4;
        public const string Extension = ".egp";
        private static readonly byte[] Magic = { (byte)'E', (byte)'G', (byte)'2', (byte)'P' };

        public static byte[] Encode(in SavedPlayer p)
        {
            int count = p.Carried != null ? p.Carried.Length : 0;
            PacketWriter w = new PacketWriter(64 + p.Name.Length * 4 + count * 64);
            for (int i = 0; i < Magic.Length; i++) w.WriteByte(Magic[i]);
            w.WriteUInt16(Version);
            w.WriteString(p.Name);
            w.WriteDouble(p.Body.East);
            w.WriteDouble(p.Body.Up);
            w.WriteDouble(p.Body.North);
            w.WriteSingle(p.YawDeg);
            w.WriteSingle(p.PitchDeg);
            w.WriteByte(BodyWire.FlagsOf(p.Body));
            w.WriteInt64(p.SavedTick);
            w.WriteByte(p.Hand);
            w.WriteByte((byte)count);
            for (int i = 0; i < count; i++)
            {
                CarriedThing t = p.Carried[i];
                w.WriteByte(t.Place);
                w.WriteUInt64(t.Id);
                w.WriteString(t.Definition.Key);
                w.WriteInt64(t.SpawnTick);
            }
            w.WriteDouble(p.WaterLoss);
            byte[] body = w.Written.ToArray();
            byte[] file = new byte[body.Length + 4];
            Buffer.BlockCopy(body, 0, file, 0, body.Length);
            uint crc = Crc32.Compute(body);
            file[body.Length] = (byte)crc;
            file[body.Length + 1] = (byte)(crc >> 8);
            file[body.Length + 2] = (byte)(crc >> 16);
            file[body.Length + 3] = (byte)(crc >> 24);
            return file;
        }

        public static SavedPlayer Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12) throw new InvalidDataException("a player file is longer than this");
            for (int i = 0; i < Magic.Length; i++)
                if (bytes[i] != Magic[i]) throw new InvalidDataException("not a player file (magic)");
            int bodyLength = bytes.Length - 4;
            uint stated = (uint)(bytes[bodyLength] | (bytes[bodyLength + 1] << 8) | (bytes[bodyLength + 2] << 16) | (bytes[bodyLength + 3] << 24));
            uint actual = Crc32.Compute(bytes, 0, bodyLength);
            if (stated != actual) throw new InvalidDataException("player file CRC " + actual.ToString("x8") + " differs from the stated " + stated.ToString("x8"));
            PacketReader r = new PacketReader(bytes, 4, bodyLength - 4);
            ushort version = r.ReadUInt16();
            if (version != Version && version != 3 && version != 2) throw new InvalidDataException("player file version " + version + "; this build reads 2, 3 and " + Version);
            SavedPlayer p = default;
            p.Name = r.ReadString();
            p.Body = MoverState.AtRest(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
            p.YawDeg = r.ReadSingle();
            p.PitchDeg = r.ReadSingle();
            BodyWire.ApplyFlags(r.ReadByte(), ref p.Body);
            p.SavedTick = r.ReadInt64();
            if (version >= 3)
            {
                p.Hand = r.ReadByte();
                int count = r.ReadByte();
                if (p.Hand > Hands.Places || count > Hands.Places)
                    throw new InvalidDataException("player " + p.Name + " carries " + count + " things with the hand at place " + p.Hand + "; the hands have " + Hands.Places + " places");
                p.Carried = new CarriedThing[count];
                bool[] filled = new bool[Hands.Places + 1];
                for (int i = 0; i < count; i++)
                {
                    byte place = r.ReadByte();
                    ulong id = r.ReadUInt64();
                    string key = r.ReadString();
                    long spawnTick = r.ReadInt64();
                    if (place < 1 || place > Hands.Places || filled[place])
                        throw new InvalidDataException("player " + p.Name + " carries a thing in place " + place + ", which is no place or holds another");
                    filled[place] = true;
                    if (!DefinitionCatalogue.TryByKey(key, out Definition definition))
                        throw new InvalidDataException("player " + p.Name + " carries a '" + key + "', which this build does not know");
                    p.Carried[i] = new CarriedThing { Id = id, Definition = definition, SpawnTick = spawnTick, Place = place };
                }
            }
            if (version >= 4)
            {
                p.WaterLoss = r.ReadDouble();
                if (!(p.WaterLoss >= 0.0 && p.WaterLoss <= 1.0))
                    throw new InvalidDataException("player " + p.Name + " has lost " + p.WaterLoss + " of their water, which is no fraction");
            }
            r.ExpectEnd();
            return p;
        }
    }
}
