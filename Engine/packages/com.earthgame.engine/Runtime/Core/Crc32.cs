namespace EarthGame.Engine
{
    /// <summary>
    /// CRC-32 (IEEE 802.3, the polynomial zip and PNG use), for a receiver to know that the bytes it assembled are
    /// the bytes that were sent. Not a security measure; a corruption and layout check. Table built once.
    /// </summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(byte[] bytes) => Compute(bytes, 0, bytes == null ? 0 : bytes.Length);

        public static uint Compute(byte[] bytes, int offset, int count)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = offset; i < offset + count; i++)
                crc = Table[(crc ^ bytes[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
