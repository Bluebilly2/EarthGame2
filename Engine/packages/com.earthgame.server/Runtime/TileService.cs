using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Transport;

namespace EarthGame.Server
{
    /// <summary>
    /// The server's side of layer streaming (M1.B promise 1, extended by M1.4b): a tile of one of the world's
    /// layers encoded once and kept, served on request as a header and a run of reliable chunks. A tile whose
    /// checksum the client already holds is answered with the header alone. A layer this world does not have —
    /// the ground of a server started without region data, the water of a world saved before M1.2 — is answered
    /// with a header of zero posts, which the client logs rather than waits for.
    /// </summary>
    public sealed class TileService
    {
        private readonly Heightfield _terrain;
        private readonly WorldWater _water;
        private readonly RegionRaster _cover;
        private readonly RegionRaster _stand;
        private readonly RegionRaster _loose;
        private readonly RegionRaster _stone;
        private readonly TileGrid _grid;
        private readonly Dictionary<(TileLayer Layer, TileId Id), EncodedTile> _encoded = new Dictionary<(TileLayer, TileId), EncodedTile>();
        private readonly PacketWriter _writer = new PacketWriter(ProtocolInfo.TileChunkBytes + 64);

        public TileService(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            _terrain = world.Terrain;
            _water = world.Water;
            _cover = world.Cover;
            _stand = world.Stand;
            _loose = world.Loose;
            _stone = world.Stone;
            _grid = new TileGrid(world.Region.ExtentM);
        }

        /// <summary>Whether this world can answer for a layer at all.</summary>
        public bool Serves(TileLayer layer)
        {
            if (_terrain == null) return false;
            switch (layer)
            {
                case TileLayer.Ground: return true;
                case TileLayer.GroundCover: return _cover != null;
                case TileLayer.Stand: return _stand != null;
                case TileLayer.Loose: return _loose != null;
                case TileLayer.Stone: return _stone != null;
                case TileLayer.FarStand:
                case TileLayer.FarCount: return TileCodec.FarSpan(_stand) > 0;
                default: return _water != null;
            }
        }

        public TileGrid Grid => _grid;

        /// <summary>Bytes of tile data sent so far, so a host can separate the join stream from the rest (N4).</summary>
        public long BytesServed { get; private set; }

        /// <summary>
        /// Encodes every tile of the region now, so no join pays for it inside a tick: the first corpus run
        /// (2026-09-08) showed the one update that encoded the nine tiles around the wake taking 124 ms against
        /// a 50 ms interval. Returns how many tiles were encoded; a world without terrain has none. Each tile is made from
        /// the world's layers alone, which are only read, so the tiles are made across the machine's cores and put in the cache
        /// in the order they are listed (WG.2b, 2026-09-23: the whole valley's 9,216 took 28 to 42 s on one core at its
        /// dedicated server's start); a tile that cannot be encoded fails the start with its own exception, as before.
        /// </summary>
        public int EncodeAll()
        {
            if (_terrain == null) return 0;
            var wanted = new List<(TileLayer Layer, TileId Id)>();
            foreach (TileLayer layer in TileLayers.All)
            {
                if (!Serves(layer)) continue;
                for (int iz = 0; iz < _grid.TilesPerSide; iz++)
                    for (int ix = 0; ix < _grid.TilesPerSide; ix++)
                        wanted.Add((layer, new TileId(ix, iz)));
            }
            var made = new EncodedTile[wanted.Count];
            try
            {
                Parallel.For(0, wanted.Count, k =>
                {
                    if (!_encoded.ContainsKey(wanted[k])) made[k] = Encode(wanted[k].Id, wanted[k].Layer);
                });
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count > 0)
            {
                ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
            for (int k = 0; k < wanted.Count; k++)
                if (made[k] != null) _encoded[wanted[k]] = made[k];
            return wanted.Count;
        }

        /// <summary>The encoded tile of a layer, made on first request.</summary>
        public EncodedTile Encoded(TileId id, TileLayer layer = TileLayer.Ground)
        {
            if (!_encoded.TryGetValue((layer, id), out EncodedTile tile))
            {
                tile = Encode(id, layer);
                _encoded[(layer, id)] = tile;
            }
            return tile;
        }

        /// <summary>A tile of a layer made from the world's layers, which it only reads; nothing is cached here.</summary>
        private EncodedTile Encode(TileId id, TileLayer layer)
        {
            if (!Serves(layer)) throw new InvalidOperationException("this world has no " + layer + " to serve");
            switch (layer)
            {
                case TileLayer.Ground: return TileCodec.Encode(_terrain, _grid, id);
                case TileLayer.WaterDepth: return TileCodec.EncodeDepth(_water.Surface, _terrain, _grid, id);
                case TileLayer.WaterClass: return TileCodec.EncodeCodes(_water.Classes, TileLayer.WaterClass, _grid, id);
                case TileLayer.GroundCover: return TileCodec.EncodeCodes(_cover, TileLayer.GroundCover, _grid, id);
                case TileLayer.Stand: return TileCodec.EncodeCodes(_stand, TileLayer.Stand, _grid, id);
                case TileLayer.Loose: return TileCodec.EncodeCodes(_loose, TileLayer.Loose, _grid, id);
                case TileLayer.Stone: return TileCodec.EncodeCodes(_stone, TileLayer.Stone, _grid, id);
                case TileLayer.FarStand:
                case TileLayer.FarCount: return TileCodec.EncodeFar(_stand, layer, _grid, id);
                default: throw new ArgumentOutOfRangeException(nameof(layer), "no such layer: " + layer);
            }
        }

        /// <summary>Answers a request: a header per wanted tile, and chunks for those the client does not hold.</summary>
        public void Serve(IConnection connection, TileRequestMessage request)
        {
            if (request.Wants == null) return;
            foreach (TileWant want in request.Wants)
            {
                TileId id = new TileId(want.Ix, want.Iz);
                TileHeaderMessage header;
                header.Ix = id.Ix;
                header.Iz = id.Iz;
                header.Layer = want.Layer;
                if (!Serves(want.Layer) || !_grid.Contains(id))
                {
                    header.Posts = 0;
                    header.CellM = 0f;
                    header.OriginEast = 0.0;
                    header.OriginNorth = 0.0;
                    header.ByteLength = 0;
                    header.Crc32 = 0;
                    header.ChunkCount = 0;
                    Send(connection, header);
                    continue;
                }
                EncodedTile tile = Encoded(id, want.Layer);
                bool unchanged = want.KnownCrc32 != 0 && want.KnownCrc32 == tile.Crc32;
                int chunkCount = unchanged ? 0 : (tile.Bytes.Length + ProtocolInfo.TileChunkBytes - 1) / ProtocolInfo.TileChunkBytes;
                header.Posts = (ushort)tile.Posts;
                header.CellM = (float)tile.CellM;
                header.OriginEast = tile.OriginEast;
                header.OriginNorth = tile.OriginNorth;
                header.ByteLength = unchanged ? 0 : tile.Bytes.Length;
                header.Crc32 = tile.Crc32;
                header.ChunkCount = (ushort)chunkCount;
                Send(connection, header);
                for (int i = 0; i < chunkCount; i++)
                {
                    int offset = i * ProtocolInfo.TileChunkBytes;
                    int length = Math.Min(ProtocolInfo.TileChunkBytes, tile.Bytes.Length - offset);
                    byte[] slice = new byte[length];
                    Buffer.BlockCopy(tile.Bytes, offset, slice, 0, length);
                    TileChunkMessage chunk;
                    chunk.Ix = id.Ix;
                    chunk.Iz = id.Iz;
                    chunk.Layer = want.Layer;
                    chunk.Index = (ushort)i;
                    chunk.Bytes = slice;
                    _writer.Reset();
                    chunk.Write(_writer);
                    connection.Send(_writer.Written, Delivery.Reliable);
                    BytesServed += _writer.Length;
                }
            }
        }

        private void Send(IConnection connection, TileHeaderMessage header)
        {
            _writer.Reset();
            header.Write(_writer);
            connection.Send(_writer.Written, Delivery.Reliable);
            BytesServed += _writer.Length;
        }
    }
}
