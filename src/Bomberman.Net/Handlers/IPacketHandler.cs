using System.Net;
using Bomberman.Net.Packets;

namespace Bomberman.Net.Handlers
{
    public interface IPacketHandler
    {
        bool CanHandle(PacketType type);
        void Handle(byte[] data, IPEndPoint sender, NetworkController controller);
    }
}
