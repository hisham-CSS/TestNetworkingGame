using System.IO;

namespace Bomberman.Net.Packets
{
    public interface IPacket
    {
        PacketType Type { get; }
        void Serialize(BinaryWriter writer);
    }
}
