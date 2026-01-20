using System.IO;

namespace Chronos.Net.Packets
{
    public struct PingPacket : IPacket
    {
        public PacketType Type => PacketType.Ping;
        public long Timestamp { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Timestamp);
        }

        public static PingPacket Deserialize(BinaryReader reader)
        {
            return new PingPacket { Timestamp = reader.ReadInt64() };
        }
    }
}
