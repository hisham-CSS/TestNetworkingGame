using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>Reply to a Ping. Echoes the original timestamp back unchanged.</summary>
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
            => new PongPacket { Timestamp = reader.ReadInt64() };
    }
}
