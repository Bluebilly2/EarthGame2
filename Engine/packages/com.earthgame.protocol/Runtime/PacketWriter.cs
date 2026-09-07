using System;
using System.Buffers.Binary;
using System.Text;

namespace EarthGame.Protocol
{
    /// <summary>
    /// Writes a message into a growable byte buffer, little-endian, no padding, no reflection. The reader is
    /// the exact mirror. Every message's layout is therefore the order of its Write calls, and a test
    /// round-trips every message type so the two cannot drift apart. Reused between messages via
    /// <see cref="Reset"/> so the hot path allocates nothing.
    /// </summary>
    public sealed class PacketWriter
    {
        private byte[] _buffer;
        private int _length;

        public PacketWriter(int initialCapacity = 256)
        {
            _buffer = new byte[initialCapacity < 16 ? 16 : initialCapacity];
            _length = 0;
        }

        /// <summary>Bytes written so far.</summary>
        public int Length => _length;

        /// <summary>The written bytes, without copying. Valid until the next write or reset.</summary>
        public ReadOnlySpan<byte> Written => new ReadOnlySpan<byte>(_buffer, 0, _length);

        /// <summary>A copy of the written bytes.</summary>
        public byte[] ToArray()
        {
            byte[] copy = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, copy, 0, _length);
            return copy;
        }

        /// <summary>Forgets everything written; keeps the buffer.</summary>
        public void Reset() => _length = 0;

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUInt16(ushort value)
        {
            Ensure(2);
            BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(_buffer, _length, 2), value);
            _length += 2;
        }

        public void WriteInt32(int value)
        {
            Ensure(4);
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(_buffer, _length, 4), value);
            _length += 4;
        }

        public void WriteUInt32(uint value)
        {
            Ensure(4);
            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(_buffer, _length, 4), value);
            _length += 4;
        }

        public void WriteInt64(long value)
        {
            Ensure(8);
            BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(_buffer, _length, 8), value);
            _length += 8;
        }

        public void WriteUInt64(ulong value)
        {
            Ensure(8);
            BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(_buffer, _length, 8), value);
            _length += 8;
        }

        public void WriteSingle(float value) => WriteInt32(BitConverter.SingleToInt32Bits(value));

        public void WriteDouble(double value) => WriteInt64(BitConverter.DoubleToInt64Bits(value));

        /// <summary>
        /// UTF-8 with a 16-bit byte-length prefix. Null is written as empty; the reader cannot tell the two
        /// apart, which is deliberate — the wire carries values, not the absence of values.
        /// </summary>
        public void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteUInt16(0);
                return;
            }
            int byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount > ProtocolInfo.MaxStringBytes)
                throw new ProtocolException("string of " + byteCount + " UTF-8 bytes exceeds MaxStringBytes " + ProtocolInfo.MaxStringBytes);
            WriteUInt16((ushort)byteCount);
            Ensure(byteCount);
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _length);
            _length += byteCount;
        }

        /// <summary>Raw bytes with a 16-bit length prefix.</summary>
        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length > ushort.MaxValue)
                throw new ProtocolException("byte block of " + bytes.Length + " exceeds the 16-bit length prefix");
            WriteUInt16((ushort)bytes.Length);
            Ensure(bytes.Length);
            bytes.CopyTo(new Span<byte>(_buffer, _length, bytes.Length));
            _length += bytes.Length;
        }

        private void Ensure(int extra)
        {
            int needed = _length + extra;
            if (needed <= _buffer.Length) return;
            if (needed > ProtocolInfo.MaxMessageBytes)
                throw new ProtocolException("message would exceed MaxMessageBytes " + ProtocolInfo.MaxMessageBytes);
            int size = _buffer.Length * 2;
            while (size < needed) size *= 2;
            if (size > ProtocolInfo.MaxMessageBytes) size = ProtocolInfo.MaxMessageBytes;
            byte[] grown = new byte[size];
            Buffer.BlockCopy(_buffer, 0, grown, 0, _length);
            _buffer = grown;
        }
    }
}
