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
        
        /// <summary>
        /// Sends a raw byte array to the previously connected host.
        /// Should only be used after calling <see cref="Connect"/>.
        /// </summary>
        /// <param name="data">The byte array to send.</param>
        void SendToConnectedHost(byte[] data);
        
        // Host/Peer Side
        /// <summary>
        /// Sends a raw byte array to a specific endpoint.
        /// </summary>
        /// <param name="data">The byte array to send.</param>
        /// <param name="target">The destination endpoint.</param>
        void SendTo(byte[] data, IPEndPoint target);
        
        /// <summary>
        /// Polls the underlying socket for incoming data and raises <see cref="PacketReceived"/> events.
        /// This should be called once per frame or update tick.
        /// </summary>
        void Poll();
        
        /// <summary>The local port this transport is bound to.</summary>
        int LocalPort { get; }
    }
}
