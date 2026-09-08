using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.Engine;
using EarthGame.Protocol;

namespace EarthGame.ClientCore
{
    /// <summary>A tile the client now holds: its posts as heights, and where they sit.</summary>
    public sealed class ReceivedTile
    {
        public TileId Id;
        public int Posts;
        public double CellM;
        public double OriginEast;
        public double OriginNorth;
        public uint Crc32;
        /// <summary>Heights in metres, indexed [north, east].</summary>
        public float[,] Heights;
        /// <summary>True when the bytes came from the disk cache rather than the wire.</summary>
        public bool FromCache;
    }

    /// <summary>Where a client keeps the tiles it has received, so a rejoin asks only for what changed (N3).</summary>
    public interface ITileCache
    {
        /// <summary>The checksum of the copy held for a tile, or zero for none.</summary>
        uint KnownCrc(string regionId, TileId id);
        bool TryLoad(string regionId, TileId id, uint crc32, out byte[] bytes);
        void Store(string regionId, TileId id, uint crc32, byte[] bytes);
    }

    /// <summary>A cache that holds nothing: every join streams every tile.</summary>
    public sealed class NoTileCache : ITileCache
    {
        public uint KnownCrc(string regionId, TileId id) => 0;
        public bool TryLoad(string regionId, TileId id, uint crc32, out byte[] bytes)
        {
            bytes = null;
            return false;
        }
        public void Store(string regionId, TileId id, uint crc32, byte[] bytes) { }
    }

    /// <summary>
    /// Tiles on disk under <c>&lt;root&gt;/&lt;region&gt;/&lt;ix&gt;_&lt;iz&gt;.tile</c>: four bytes of checksum then the
    /// encoded bytes, written to a .part and moved into place. A file whose checksum does not match its bytes
    /// is treated as absent.
    /// </summary>
    public sealed class DiskTileCache : ITileCache
    {
        private readonly string _root;

        public DiskTileCache(string root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        private string PathFor(string regionId, TileId id) => Path.Combine(_root, regionId, id.Ix + "_" + id.Iz + ".tile");

        public uint KnownCrc(string regionId, TileId id)
        {
            string path = PathFor(regionId, id);
            if (!File.Exists(path)) return 0;
            try
            {
                byte[] all = File.ReadAllBytes(path);
                if (all.Length < 4) return 0;
                uint crc = BitConverter.ToUInt32(all, 0);
                return Crc32.Compute(all, 4, all.Length - 4) == crc ? crc : 0;
            }
            catch (IOException)
            {
                return 0;
            }
        }

        public bool TryLoad(string regionId, TileId id, uint crc32, out byte[] bytes)
        {
            bytes = null;
            string path = PathFor(regionId, id);
            if (!File.Exists(path)) return false;
            byte[] all;
            try
            {
                all = File.ReadAllBytes(path);
            }
            catch (IOException)
            {
                return false;
            }
            if (all.Length < 4 || BitConverter.ToUInt32(all, 0) != crc32) return false;
            byte[] body = new byte[all.Length - 4];
            Buffer.BlockCopy(all, 4, body, 0, body.Length);
            if (Crc32.Compute(body) != crc32) return false;
            bytes = body;
            return true;
        }

        public void Store(string regionId, TileId id, uint crc32, byte[] bytes)
        {
            string path = PathFor(regionId, id);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] all = new byte[bytes.Length + 4];
            Buffer.BlockCopy(BitConverter.GetBytes(crc32), 0, all, 0, 4);
            Buffer.BlockCopy(bytes, 0, all, 4, bytes.Length);
            string part = path + ".part";
            File.WriteAllBytes(part, all);
            if (File.Exists(path)) File.Delete(path);
            File.Move(part, path);
        }
    }

    /// <summary>
    /// Assembles tiles from headers and chunks, checks them, unpacks them and raises them; loads from the cache
    /// what the server says is unchanged. Chunks arrive reliable and ordered after their header, so a chunk for
    /// a tile without a header is a protocol error the client reports and drops.
    /// </summary>
    public sealed class TileReceiver
    {
        private sealed class Pending
        {
            public TileHeaderMessage Header;
            public byte[] Bytes;
            public int NextChunk;
        }

        private readonly ITileCache _cache;
        private readonly string _regionId;
        private readonly Dictionary<TileId, Pending> _pending = new Dictionary<TileId, Pending>();
        private readonly Dictionary<TileId, ReceivedTile> _held = new Dictionary<TileId, ReceivedTile>();

        public TileReceiver(string regionId, ITileCache cache)
        {
            _regionId = regionId ?? string.Empty;
            _cache = cache ?? new NoTileCache();
        }

        /// <summary>Tiles held, complete and checked.</summary>
        public IReadOnlyDictionary<TileId, ReceivedTile> Held => _held;

        /// <summary>Tiles the server said it had no ground for.</summary>
        public int RefusedCount { get; private set; }

        public long BytesReceived { get; private set; }

        public event Action<ReceivedTile> TileReady;
        public event Action<TileId, string> TileFailed;

        public bool Holds(TileId id) => _held.ContainsKey(id);

        /// <summary>The wants for a set of tiles, carrying the checksums the cache already holds.</summary>
        public TileWant[] WantsFor(IReadOnlyList<TileId> ids)
        {
            TileWant[] wants = new TileWant[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                wants[i].Ix = ids[i].Ix;
                wants[i].Iz = ids[i].Iz;
                wants[i].KnownCrc32 = _cache.KnownCrc(_regionId, ids[i]);
            }
            return wants;
        }

        public void Handle(TileHeaderMessage header)
        {
            TileId id = new TileId(header.Ix, header.Iz);
            if (header.Posts == 0)
            {
                RefusedCount++;
                TileFailed?.Invoke(id, "the server has no ground for this tile");
                return;
            }
            if (header.ByteLength == 0)
            {
                byte[] cached;
                if (_cache.TryLoad(_regionId, id, header.Crc32, out cached))
                {
                    Complete(header, cached, true);
                }
                else
                {
                    TileFailed?.Invoke(id, "the server believes we hold checksum " + header.Crc32 + " and the cache does not");
                }
                return;
            }
            _pending[id] = new Pending { Header = header, Bytes = new byte[header.ByteLength], NextChunk = 0 };
        }

        public void Handle(TileChunkMessage chunk)
        {
            TileId id = new TileId(chunk.Ix, chunk.Iz);
            Pending pending;
            if (!_pending.TryGetValue(id, out pending))
            {
                TileFailed?.Invoke(id, "chunk " + chunk.Index + " arrived without a header");
                return;
            }
            if (chunk.Index != pending.NextChunk)
            {
                _pending.Remove(id);
                TileFailed?.Invoke(id, "chunk " + chunk.Index + " arrived where " + pending.NextChunk + " was expected");
                return;
            }
            int offset = chunk.Index * ProtocolInfo.TileChunkBytes;
            byte[] bytes = chunk.Bytes ?? Array.Empty<byte>();
            if (offset + bytes.Length > pending.Bytes.Length)
            {
                _pending.Remove(id);
                TileFailed?.Invoke(id, "chunks exceed the header's byte length");
                return;
            }
            Buffer.BlockCopy(bytes, 0, pending.Bytes, offset, bytes.Length);
            BytesReceived += bytes.Length;
            pending.NextChunk++;
            if (pending.NextChunk == pending.Header.ChunkCount)
            {
                _pending.Remove(id);
                if (Crc32.Compute(pending.Bytes) != pending.Header.Crc32)
                {
                    TileFailed?.Invoke(id, "checksum mismatch after " + pending.Header.ChunkCount + " chunks");
                    return;
                }
                _cache.Store(_regionId, id, pending.Header.Crc32, pending.Bytes);
                Complete(pending.Header, pending.Bytes, false);
            }
        }

        private void Complete(TileHeaderMessage header, byte[] bytes, bool fromCache)
        {
            TileId id = new TileId(header.Ix, header.Iz);
            float[,] heights;
            try
            {
                heights = TileCodec.Unpack(bytes, header.Posts);
            }
            catch (InvalidDataException ex)
            {
                TileFailed?.Invoke(id, "unpack: " + ex.Message);
                return;
            }
            ReceivedTile tile = new ReceivedTile
            {
                Id = id,
                Posts = header.Posts,
                CellM = header.CellM,
                OriginEast = header.OriginEast,
                OriginNorth = header.OriginNorth,
                Crc32 = header.Crc32,
                Heights = heights,
                FromCache = fromCache,
            };
            _held[id] = tile;
            TileReady?.Invoke(tile);
        }
    }
}
