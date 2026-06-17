using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>Latency probe. The sender stamps DateTime.Now.Ticks; the reply (Pong) echoes it so
    /// the sender can compute round-trip time without the two clocks needing to agree.</summary>
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
            => new PingPacket { Timestamp = reader.ReadInt64() };
    }
}
