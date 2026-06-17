using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>
    /// Common interface for all network packets. Every packet knows its <see cref="PacketType"/>
    /// (written as the leading byte) and how to serialize its own payload. Back-ported from the
    /// production Chronos.Net.Packets.IPacket (the Chronos rename lands in Week 5).
    /// </summary>
    public interface IPacket
    {
        /// <summary>The unique type identifier for this packet (the first byte on the wire).</summary>
        PacketType Type { get; }

        /// <summary>Serializes the packet payload to the writer.</summary>
        void Serialize(BinaryWriter writer);
    }
}
