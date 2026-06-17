using System;
using System.Net;

namespace Bomberman.Net
{
    /// <summary>
    /// Abstraction for the network transport layer (send/receive byte arrays). Defining this seam
    /// now lets Bomberman.Core depend on an interface, not a concrete socket — so the engine stays
    /// framework-agnostic. The concrete UdpTransport (and the protocol/lobby that use it) arrive in
    /// Week 3. Back-ported from the production Chronos.Net.ITransport.
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>Raised when a packet is received.</summary>
        event Action<byte[], IPEndPoint>? PacketReceived;

        void Connect(string ip, int port);
        void SendToConnectedHost(byte[] data);
        void SendTo(byte[] data, IPEndPoint target);
        void Poll();
        int LocalPort { get; }
    }
}
