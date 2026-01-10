using System;
using System.Net;

namespace Bomberman.Net
{
    public interface ITransport : IDisposable
    {
        event Action<byte[], IPEndPoint>? PacketReceived;
        
        // Client Side
        void Connect(string ip, int port);
        void SendToConnectedHost(byte[] data);
        
        // Host/Peer Side
        void SendTo(byte[] data, IPEndPoint target);
        
        void Poll();
    }
}
