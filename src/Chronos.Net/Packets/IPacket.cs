using System.IO;

namespace Chronos.Net.Packets
{
    /// <summary>
    /// Common interface for all network packets.
    /// </summary>
    public interface IPacket
    {
        /// <summary>Gets the unique type identifier for this packet.</summary>
        PacketType Type { get; }

        /// <summary>Serializes the packet payload to the writer.</summary>
        void Serialize(BinaryWriter writer);
    }
}
