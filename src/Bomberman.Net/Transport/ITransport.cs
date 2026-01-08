using System;
using System.Net;

namespace Bomberman.Net
{
    public interface ITransport : IDisposable
    {
        event Action<byte[], IPEndPoint>? PacketReceived;
        void Send(byte[] data);
        void SendTo(byte[] data, IPEndPoint target);
        void Broadcast(byte[] data);
        void Poll();
    }
}
