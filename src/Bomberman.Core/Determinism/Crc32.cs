namespace Bomberman.Core
{
    /// <summary>
    /// Standard CRC-32 (IEEE 802.3, polynomial 0xEDB88247 reflected). A table-driven checksum over a
    /// byte buffer. We use it in Week 4 as a point of comparison with the structural Jenkins hash:
    ///
    ///   Jenkins one-at-a-time  - hashes the world's FIELDS directly; no serialization step; very fast.
    ///   CRC32                  - hashes the SERIALIZED BYTES; catches any byte-level difference;
    ///                            classic for file/packet integrity; still not cryptographic.
    ///   SHA-256 (mentioned)    - cryptographically strong, ~100x slower; overkill for trusted-peer
    ///                            desync checks where we only need to NOTICE divergence, not resist
    ///                            a deliberate forgery.
    ///
    /// For lockstep desync detection a fast non-cryptographic checksum (Jenkins or CRC32) is the
    /// right tool: tiny, deterministic, and sensitive to one-bit differences.
    /// </summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            const uint poly = 0xEDB88320u;
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int b = 0; b < 8; b++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : crc >> 1;
                table[i] = crc;
            }
            return table;
        }

        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
