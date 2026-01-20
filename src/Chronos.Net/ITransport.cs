using System;
using System.Net;

namespace Chronos.Net
{
    /// <summary>
    /// Abstraction for the network transport layer.
    /// Supports sending and receiving byte arrays.
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>Event raised when a packet is received.</summary>
        event Action<byte[], IPEndPoint>? PacketReceived;
        
        // Client Side
        /// <summary>Stores the target host address for checking connection status/sending.</summary>
        void Connect(string ip, int port);
        
        /// <summary>Sends data to the pre-configured host.</summary>
        void SendToConnectedHost(byte[] data);
        
        // Host/Peer Side
        /// <summary>Sends data to a specific endpoint.</summary>
        void SendTo(byte[] data, IPEndPoint target);
        
        /// <summary>Polls the socket for incoming data.</summary>
        void Poll();
        
        /// <summary>The local port this transport is bound to.</summary>
        int LocalPort { get; }
    }
}
