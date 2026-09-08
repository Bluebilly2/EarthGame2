using System;
using System.Collections.Generic;
using EarthGame.Engine;
using EarthGame.Protocol;
using EarthGame.Transport;

namespace EarthGame.Server
{
    /// <summary>
    /// The server's side of layer streaming (M1.B, promise 1): a tile of the world's heightfield encoded once and
    /// kept, served on request as a header and a run of reliable chunks. A tile whose checksum the client already
    /// holds is answered with the header alone. Without a terrain the service answers every request with a
    /// header of zero posts, which the client logs rather than waits for.
    /// </summary>
    public sealed class TileService
    {
        private readonly Heightfield _terrain;
        private readonly TileGrid _grid;
        private readonly Dictionary<TileId, EncodedTile> _encoded = new Dictionary<TileId, EncodedTile>();
        private readonly PacketWriter _writer = new PacketWriter(ProtocolInfo.TileChunkBytes + 64);

        public TileService(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            _terrain = world.Terrain;
            _grid = new TileGrid(world.Region.ExtentM);
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
            for (int iz = 0; iz < _grid.TilesPerSide; iz++)
                for (int ix = 0; ix < _grid.TilesPerSide; ix++)
                {
                    Encoded(new TileId(ix, iz));
                    count++;
                }
            return count;
        }

        /// <summary>The encoded tile, made on first request.</summary>
        public EncodedTile Encoded(TileId id)
        {
            EncodedTile tile;
            if (!_encoded.TryGetValue(id, out tile))
            {
                tile = TileCodec.Encode(_terrain, _grid, id);
                _encoded[id] = tile;
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
                if (_terrain == null || !_grid.Contains(id))
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
                EncodedTile tile = Encoded(id);
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
