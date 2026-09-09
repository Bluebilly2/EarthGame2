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
    /// The region file, format <c>eg2.region</c> version 1 (ARCHITECTURE §6 and §10): one per 512 m cell
    /// (<see cref="RegionCells"/>), holding the entities whose position lies in the cell. Little-endian: the magic
    /// <c>EG2R</c>, u16 version, i32 cell x, i32 cell z, f64 cell size, u32 entity count, u32 layer-diff count
    /// (zero until the diffs land), u32 CRC-32 of the records that follow, then per entity: u64 id, the key as a
    /// u16 UTF-8 byte length and the bytes, f64 east, up and north, f32 yaw, i64 spawn tick, u8 component mask
    /// (1 = item), and for an item u8 resting and f32 fall speed. The server writes and reads it; save_check.py
    /// restates the layout in Python and reads it too.
    /// </summary>
    public static class RegionFile
    {
        public const string Folder = "regions";
        public const ushort Version = 1;
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

        public static byte[] Encode(int cellX, int cellZ, IReadOnlyList<SavedEntity> entities)
        {
            PacketWriter records = new PacketWriter(64 + 64 * entities.Count);
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
            byte[] body = records.Written.ToArray();
            PacketWriter head = new PacketWriter(HeaderBytes);
            for (int i = 0; i < Magic.Length; i++) head.WriteByte(Magic[i]);
            head.WriteUInt16(Version);
            head.WriteInt32(cellX);
            head.WriteInt32(cellZ);
            head.WriteDouble(RegionCells.CellM);
            head.WriteUInt32((uint)entities.Count);
            head.WriteUInt32(0);
            head.WriteUInt32(Crc32.Compute(body));
            byte[] file = new byte[HeaderBytes + body.Length];
            head.Written.CopyTo(file);
            Buffer.BlockCopy(body, 0, file, HeaderBytes, body.Length);
            return file;
        }

        /// <summary>Reads a region file; refuses a wrong magic, another version, a wrong CRC or a short record with the reason.</summary>
        public static List<SavedEntity> Decode(byte[] bytes, out int cellX, out int cellZ)
        {
            if (bytes == null || bytes.Length < HeaderBytes) throw new InvalidDataException("a region file is at least " + HeaderBytes + " bytes");
            for (int i = 0; i < Magic.Length; i++)
                if (bytes[i] != Magic[i]) throw new InvalidDataException("not a region file (magic)");
            PacketReader head = new PacketReader(bytes, 4, HeaderBytes - 4);
            ushort version = head.ReadUInt16();
            if (version != Version) throw new InvalidDataException("region file version " + version + "; this build reads " + Version);
            cellX = head.ReadInt32();
            cellZ = head.ReadInt32();
            double cellM = head.ReadDouble();
            if (Math.Abs(cellM - RegionCells.CellM) > 1e-9) throw new InvalidDataException("region file cells are " + cellM + " m; this build keeps " + RegionCells.CellM);
            uint count = head.ReadUInt32();
            uint diffs = head.ReadUInt32();
            if (diffs != 0) throw new InvalidDataException("region file carries " + diffs + " layer diffs; this build reads none");
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
            r.ExpectEnd();
            return entities;
        }
    }

    /// <summary>
    /// A player's resting place, format <c>eg2.player</c> version 2 (binary; version 1 was JSON and is still read):
    /// the magic <c>EG2P</c>, u16 version, the name as a u16 UTF-8 byte length and the bytes, f64 east, up and
    /// north, f32 yaw, f32 pitch, u8 flags (1 grounded, 2 wading, 4 crouching, as <see cref="BodyWire"/> packs
    /// them), i64 saved tick, then u32 CRC-32 of everything before it. Version 1 dropped wading and stance, which
    /// the round trip's digest needs; that is why the format moved.
    /// </summary>
    public static class PlayerFile
    {
        public const ushort Version = 2;
        public const string Extension = ".egp";
        private static readonly byte[] Magic = { (byte)'E', (byte)'G', (byte)'2', (byte)'P' };

        public static byte[] Encode(in SavedPlayer p)
        {
            PacketWriter w = new PacketWriter(64 + p.Name.Length * 4);
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
            if (version != Version) throw new InvalidDataException("player file version " + version + "; this build reads " + Version);
            SavedPlayer p;
            p.Name = r.ReadString();
            p.Body = MoverState.AtRest(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
            p.YawDeg = r.ReadSingle();
            p.PitchDeg = r.ReadSingle();
            BodyWire.ApplyFlags(r.ReadByte(), ref p.Body);
            p.SavedTick = r.ReadInt64();
            r.ExpectEnd();
            return p;
        }
    }
}
