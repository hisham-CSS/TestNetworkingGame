using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>
    /// Week 4 desync detection. A peer periodically announces "at frame F my state hash was H" (plus a
    /// cheap position proxy for diagnostics). The receiver compares H against its own stored hash for
    /// frame F; a mismatch means the simulations have diverged. This is GGPO-style periodic checksum
    /// exchange, kept as its own packet so the Week 3 InputPacket format is untouched.
    /// </summary>
    public struct ChecksumPacket : IPacket
    {
        public PacketType Type => PacketType.Checksum;
        public int Frame { get; set; }
        public int Hash { get; set; }
        public int PosX { get; set; }
        public int PosY { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Frame);
            writer.Write(Hash);
            writer.Write(PosX);
            writer.Write(PosY);
        }

        public static ChecksumPacket Deserialize(BinaryReader reader)
            => new ChecksumPacket
            {
                Frame = reader.ReadInt32(),
                Hash = reader.ReadInt32(),
                PosX = reader.ReadInt32(),
                PosY = reader.ReadInt32()
            };
    }
}
