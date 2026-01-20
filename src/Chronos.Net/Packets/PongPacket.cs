using System.IO;

namespace Chronos.Net.Packets
{
    public struct PongPacket : IPacket
    {
        public PacketType Type => PacketType.Pong;
        public long Timestamp { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Timestamp);
        }

        public static PongPacket Deserialize(BinaryReader reader)
        {
            return new PongPacket { Timestamp = reader.ReadInt64() };
        }
    }
}
