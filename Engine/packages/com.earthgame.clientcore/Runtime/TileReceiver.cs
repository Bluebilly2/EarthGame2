using System;
using System.Collections.Generic;
using System.IO;
using EarthGame.Engine;
using EarthGame.Protocol;

namespace EarthGame.ClientCore
{
    /// <summary>A tile of one layer the client now holds, and where its posts sit.</summary>
    public sealed class ReceivedTile
    {
        public TileId Id;
        public TileLayer Layer;
        public int Posts;
        public double CellM;
        public double OriginEast;
        public double OriginNorth;
        public uint Crc32;
        /// <summary>Metres, indexed [north, east]: the ground, or the water's depth over it. Null for a layer of codes.</summary>
        public float[,] Heights;
        /// <summary>Codes, indexed [north, east], for a layer of one byte a post. Null for a layer of metres and for a two-byte layer.</summary>
        public byte[,] Codes;
        /// <summary>
        /// Codes, indexed [north, east], for a layer of two bytes a post (<see cref="TileLayers.CodeBytes"/>: the stand and the
        /// far stand, since WG.2c). Null for every other layer.
        /// </summary>
        public ushort[,] WideCodes;
        /// <summary>True when the bytes came from the disk cache rather than the wire.</summary>
        public bool FromCache;

        /// <summary>Whether the tile carries codes, of either width.</summary>
        public bool HasCodes => Codes != null || WideCodes != null;

        /// <summary>The code at a post, of either width; the tile must carry codes (<see cref="HasCodes"/>).</summary>
        public int CodeAt(int z, int x) => WideCodes != null ? WideCodes[z, x] : Codes[z, x];
    }

    /// <summary>Where a client keeps the tiles it has received, so a rejoin asks only for what changed (N3).</summary>
    public interface ITileCache
    {
        /// <summary>The checksum of the copy held for a tile of a layer, or zero for none.</summary>
        uint KnownCrc(string regionId, TileLayer layer, TileId id);
        bool TryLoad(string regionId, TileLayer layer, TileId id, uint crc32, out byte[] bytes);
        void Store(string regionId, TileLayer layer, TileId id, uint crc32, byte[] bytes);
    }

    /// <summary>A cache that holds nothing: every join streams every tile.</summary>
    public sealed class NoTileCache : ITileCache
    {
        public uint KnownCrc(string regionId, TileLayer layer, TileId id) => 0;
        public bool TryLoad(string regionId, TileLayer layer, TileId id, uint crc32, out byte[] bytes)
        {
            bytes = null;
            return false;
        }
        public void Store(string regionId, TileLayer layer, TileId id, uint crc32, byte[] bytes) { }
    }

    /// <summary>
    /// Tiles on disk under <c>&lt;root&gt;/&lt;region&gt;/&lt;layer&gt;/&lt;ix&gt;_&lt;iz&gt;.tile</c>: four bytes of checksum
    /// then the encoded bytes, written to a .part and moved into place. A file whose checksum does not match its
    /// bytes is treated as absent. The layer is a folder of its own, so a ground tile cached before M1.4b is
    /// still a ground tile and the water arrives beside it.
    /// </summary>
    public sealed class DiskTileCache : ITileCache
    {
        private readonly string _root;

        public DiskTileCache(string root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        private string PathFor(string regionId, TileLayer layer, TileId id)
            => Path.Combine(_root, regionId, TileLayers.FolderOf(layer), id.Ix + "_" + id.Iz + ".tile");

        public uint KnownCrc(string regionId, TileLayer layer, TileId id)
        {
            string path = PathFor(regionId, layer, id);
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

        public bool TryLoad(string regionId, TileLayer layer, TileId id, uint crc32, out byte[] bytes)
        {
            bytes = null;
            string path = PathFor(regionId, layer, id);
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

        public void Store(string regionId, TileLayer layer, TileId id, uint crc32, byte[] bytes)
        {
            string path = PathFor(regionId, layer, id);
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
        private readonly Dictionary<(TileLayer Layer, TileId Id), Pending> _pending = new Dictionary<(TileLayer, TileId), Pending>();
        private readonly Dictionary<(TileLayer Layer, TileId Id), ReceivedTile> _held = new Dictionary<(TileLayer, TileId), ReceivedTile>();
        private readonly Dictionary<TileId, ReceivedTile> _ground = new Dictionary<TileId, ReceivedTile>();

        public TileReceiver(string regionId, ITileCache cache)
        {
            _regionId = regionId ?? string.Empty;
            _cache = cache ?? new NoTileCache();
        }

        /// <summary>Ground tiles held, complete and checked; the layer the client stands on.</summary>
        public IReadOnlyDictionary<TileId, ReceivedTile> Held => _ground;

        /// <summary>How many tiles of a layer are held.</summary>
        public int CountOf(TileLayer layer)
        {
            int n = 0;
            foreach (var pair in _held) if (pair.Key.Layer == layer) n++;
            return n;
        }

        /// <summary>
        /// Lets go of the tiles farthest from a centre until each layer is inside <see cref="MaxTilesPerLayer"/>,
        /// keeping the centre's own tile and the eight around it whatever happens. Returns how many were let go.
        /// A tile let go is still on disk, so walking back to it costs a header and a read rather than the wire.
        /// </summary>
        public int Trim(TileGrid grid, TileId centre)
        {
            if (grid == null || _held.Count == 0) return 0;
            int dropped = 0;
            foreach (TileLayer layer in TileLayers.All)
            {
                // The far layers are the whole region's, and are kept wherever the founder walks (M1.6d).
                if (TileLayers.IsFar(layer)) continue;
                List<TileId> ids = new List<TileId>();
                foreach (var pair in _held) if (pair.Key.Layer == layer) ids.Add(pair.Key.Id);
                if (ids.Count <= MaxTilesPerLayer) continue;
                // Farthest first, by the square of the distance in tiles; ties by name, so two clients agree.
                ids.Sort((a, b) =>
                {
                    int byRange = Range(b, centre).CompareTo(Range(a, centre));
                    return byRange != 0 ? byRange : string.CompareOrdinal(b.ToString(), a.ToString());
                });
                for (int i = 0; i < ids.Count && ids.Count - dropped > MaxTilesPerLayer; i++)
                {
                    TileId id = ids[i];
                    if (Range(id, centre) <= 2) continue;   // the centre and its eight neighbours
                    _held.Remove((layer, id));
                    if (layer == TileLayer.Ground) _ground.Remove(id);
                    dropped++;
                    TileDropped?.Invoke(layer, id);
                }
            }
            return dropped;
        }

        private static int Range(TileId a, TileId b)
        {
            int dx = a.Ix - b.Ix, dz = a.Iz - b.Iz;
            return dx * dx + dz * dz;
        }

        /// <summary>A held tile of a layer, or null.</summary>
        public ReceivedTile Holding(TileLayer layer, TileId id) => _held.TryGetValue((layer, id), out ReceivedTile tile) ? tile : null;

        /// <summary>Tiles the server said it had no ground for.</summary>
        public int RefusedCount { get; private set; }

        public long BytesReceived { get; private set; }

        public event Action<ReceivedTile> TileReady;
        public event Action<TileLayer, TileId, string> TileFailed;
        /// <summary>A tile let go to stay inside the bound; the client forgets asking for it and may ask again.</summary>
        public event Action<TileLayer, TileId> TileDropped;

        /// <summary>
        /// Most tiles of one layer a client keeps (M1.4b promise 6). A kilometre tile of the Bherwerre ground is
        /// about 250 kB in memory as posts, so twenty-five of three layers is the order of twenty megabytes and
        /// covers the founder's tile with two rings around it. The tile under the founder and its eight
        /// neighbours are never let go, whatever the bound says.
        /// </summary>
        public int MaxTilesPerLayer { get; set; } = 25;

        public bool Holds(TileId id) => _ground.ContainsKey(id);

        /// <summary>The wants for a set of tiles of one layer, carrying the checksums the cache already holds.</summary>
        public TileWant[] WantsFor(TileLayer layer, IReadOnlyList<TileId> ids)
        {
            TileWant[] wants = new TileWant[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                wants[i].Ix = ids[i].Ix;
                wants[i].Iz = ids[i].Iz;
                wants[i].Layer = layer;
                wants[i].KnownCrc32 = _cache.KnownCrc(_regionId, layer, ids[i]);
            }
            return wants;
        }

        public void Handle(TileHeaderMessage header)
        {
            TileId id = new TileId(header.Ix, header.Iz);
            if (header.Posts == 0)
            {
                RefusedCount++;
                TileFailed?.Invoke(header.Layer, id, "the server has no " + header.Layer + " for this tile");
                return;
            }
            if (header.ByteLength == 0)
            {
                byte[] cached;
                if (_cache.TryLoad(_regionId, header.Layer, id, header.Crc32, out cached))
                {
                    Complete(header, cached, true);
                }
                else
                {
                    TileFailed?.Invoke(header.Layer, id, "the server believes we hold checksum " + header.Crc32 + " and the cache does not");
                }
                return;
            }
            _pending[(header.Layer, id)] = new Pending { Header = header, Bytes = new byte[header.ByteLength], NextChunk = 0 };
        }

        public void Handle(TileChunkMessage chunk)
        {
            TileId id = new TileId(chunk.Ix, chunk.Iz);
            Pending pending;
            if (!_pending.TryGetValue((chunk.Layer, id), out pending))
            {
                TileFailed?.Invoke(chunk.Layer, id, "chunk " + chunk.Index + " arrived without a header");
                return;
            }
            if (chunk.Index != pending.NextChunk)
            {
                _pending.Remove((chunk.Layer, id));
                TileFailed?.Invoke(chunk.Layer, id, "chunk " + chunk.Index + " arrived where " + pending.NextChunk + " was expected");
                return;
            }
            int offset = chunk.Index * ProtocolInfo.TileChunkBytes;
            byte[] bytes = chunk.Bytes ?? Array.Empty<byte>();
            if (offset + bytes.Length > pending.Bytes.Length)
            {
                _pending.Remove((chunk.Layer, id));
                TileFailed?.Invoke(chunk.Layer, id, "chunks exceed the header's byte length");
                return;
            }
            Buffer.BlockCopy(bytes, 0, pending.Bytes, offset, bytes.Length);
            BytesReceived += bytes.Length;
            pending.NextChunk++;
            if (pending.NextChunk == pending.Header.ChunkCount)
            {
                _pending.Remove((chunk.Layer, id));
                if (Crc32.Compute(pending.Bytes) != pending.Header.Crc32)
                {
                    TileFailed?.Invoke(chunk.Layer, id, "checksum mismatch after " + pending.Header.ChunkCount + " chunks");
                    return;
                }
                _cache.Store(_regionId, chunk.Layer, id, pending.Header.Crc32, pending.Bytes);
                Complete(pending.Header, pending.Bytes, false);
            }
        }

        private void Complete(TileHeaderMessage header, byte[] bytes, bool fromCache)
        {
            TileId id = new TileId(header.Ix, header.Iz);
            float[,] heights = null;
            byte[,] codes = null;
            ushort[,] wide = null;
            try
            {
                if (!TileLayers.CarriesCodes(header.Layer)) heights = TileCodec.Unpack(bytes, header.Posts);
                else if (TileLayers.CodeBytes(header.Layer) == 2) wide = TileCodec.UnpackWideCodes(bytes, header.Posts);
                else codes = TileCodec.UnpackCodes(bytes, header.Posts);
            }
            catch (InvalidDataException ex)
            {
                TileFailed?.Invoke(header.Layer, id, "unpack: " + ex.Message);
                return;
            }
            ReceivedTile tile = new ReceivedTile
            {
                Id = id,
                Layer = header.Layer,
                Posts = header.Posts,
                CellM = header.CellM,
                OriginEast = header.OriginEast,
                OriginNorth = header.OriginNorth,
                Crc32 = header.Crc32,
                Heights = heights,
                Codes = codes,
                WideCodes = wide,
                FromCache = fromCache,
            };
            _held[(header.Layer, id)] = tile;
            if (header.Layer == TileLayer.Ground) _ground[id] = tile;
            TileReady?.Invoke(tile);
        }
    }
}
