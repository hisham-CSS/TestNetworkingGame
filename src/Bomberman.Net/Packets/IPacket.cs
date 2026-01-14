using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>
    /// Base interface for all network packets.
    /// </summary>
    public interface IPacket
    {
        PacketType Type { get; }
        /// <summary>
        /// Writes the packet data to the binary writer.
        /// The Type byte is typically written before calling this.
        /// </summary>
        void Serialize(BinaryWriter writer);
    }
}
