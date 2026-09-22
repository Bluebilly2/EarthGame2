using System;
using System.Collections.Generic;
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
        /// a 50 ms interval. Returns how many tiles were encoded; a world without terrain has none.
        /// </summary>
        public int EncodeAll()
        {
            if (_terrain == null) return 0;
            int count = 0;
            foreach (TileLayer layer in TileLayers.All)
            {
                if (!Serves(layer)) continue;
                for (int iz = 0; iz < _grid.TilesPerSide; iz++)
                    for (int ix = 0; ix < _grid.TilesPerSide; ix++)
                    {
                        Encoded(new TileId(ix, iz), layer);
                        count++;
                    }
            }
            return count;
        }

        /// <summary>The encoded tile of a layer, made on first request.</summary>
        public EncodedTile Encoded(TileId id, TileLayer layer = TileLayer.Ground)
        {
            if (!_encoded.TryGetValue((layer, id), out EncodedTile tile))
            {
                if (!Serves(layer)) throw new InvalidOperationException("this world has no " + layer + " to serve");
                switch (layer)
                {
                    case TileLayer.Ground: tile = TileCodec.Encode(_terrain, _grid, id); break;
                    case TileLayer.WaterDepth: tile = TileCodec.EncodeDepth(_water.Surface, _terrain, _grid, id); break;
                    case TileLayer.WaterClass: tile = TileCodec.EncodeCodes(_water.Classes, TileLayer.WaterClass, _grid, id); break;
                    case TileLayer.GroundCover: tile = TileCodec.EncodeCodes(_cover, TileLayer.GroundCover, _grid, id); break;
                    case TileLayer.Stand: tile = TileCodec.EncodeCodes(_stand, TileLayer.Stand, _grid, id); break;
                    case TileLayer.Loose: tile = TileCodec.EncodeCodes(_loose, TileLayer.Loose, _grid, id); break;
                    case TileLayer.Stone: tile = TileCodec.EncodeCodes(_stone, TileLayer.Stone, _grid, id); break;
                    case TileLayer.FarStand:
                    case TileLayer.FarCount: tile = TileCodec.EncodeFar(_stand, layer, _grid, id); break;
                    default: throw new ArgumentOutOfRangeException(nameof(layer), "no such layer: " + layer);
                }
                _encoded[(layer, id)] = tile;
            }
            return tile;
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
