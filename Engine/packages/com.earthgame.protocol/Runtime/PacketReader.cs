using System;
using System.Buffers.Binary;
using System.Text;

namespace EarthGame.Protocol
{
    /// <summary>
    /// Reads a message written by <see cref="PacketWriter"/>. Every read is bounds-checked and a short or
    /// malformed packet raises <see cref="ProtocolException"/>, which the receiver turns into a refusal of that
    /// peer — a bad packet is never allowed to become a crash or a partial state change.
    /// </summary>
    public sealed class PacketReader
    {
        private readonly byte[] _buffer;
        private readonly int _end;
        private int _position;

        public PacketReader(byte[] buffer) : this(buffer, 0, buffer == null ? 0 : buffer.Length) { }

        public PacketReader(byte[] buffer, int offset, int count)
        {
            _buffer = buffer ?? Array.Empty<byte>();
            if (offset < 0 || count < 0 || offset + count > _buffer.Length)
                throw new ProtocolException("reader window outside the buffer");
            _position = offset;
            _end = offset + count;
        }

        /// <summary>Bytes not yet read.</summary>
        public int Remaining => _end - _position;

        public byte ReadByte()
        {
            Need(1);
            return _buffer[_position++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public ushort ReadUInt16()
        {
            Need(2);
            ushort v = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(_buffer, _position, 2));
            _position += 2;
            return v;
        }

        public int ReadInt32()
        {
            Need(4);
            int v = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(_buffer, _position, 4));
            _position += 4;
            return v;
        }

        public uint ReadUInt32()
        {
            Need(4);
            uint v = BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(_buffer, _position, 4));
            _position += 4;
            return v;
        }

        public long ReadInt64()
        {
            Need(8);
            long v = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(_buffer, _position, 8));
            _position += 8;
            return v;
        }

        public ulong ReadUInt64()
        {
            Need(8);
            ulong v = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(_buffer, _position, 8));
            _position += 8;
            return v;
        }

        public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

        public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

        public string ReadString()
        {
            int byteCount = ReadUInt16();
            if (byteCount == 0) return string.Empty;
            Need(byteCount);
            string s = Encoding.UTF8.GetString(_buffer, _position, byteCount);
            _position += byteCount;
            return s;
        }

        public byte[] ReadBytes()
        {
            int count = ReadUInt16();
            Need(count);
            byte[] block = new byte[count];
            Buffer.BlockCopy(_buffer, _position, block, 0, count);
            _position += count;
            return block;
        }

        /// <summary>Asserts the message was consumed exactly: trailing bytes mean a layout mismatch.</summary>
        public void ExpectEnd()
        {
            if (_position != _end)
                throw new ProtocolException("message has " + (_end - _position) + " unread trailing bytes");
        }

        private void Need(int count)
        {
            if (_end - _position < count)
                throw new ProtocolException("message truncated: needed " + count + " bytes, " + (_end - _position) + " left");
        }
    }
}
