using System;
using System.Net;
using System.Net.Sockets;

namespace Chronos.Net
{
    /// <summary>
    /// Abstraction for UdpClient to facilitate unit testing.
    /// </summary>
    public interface IUdpClient : IDisposable
    {
        /// <summary>Establishes a default remote host.</summary>
        void Connect(IPEndPoint endPoint);
        /// <summary>Sends a UDP datagram.</summary>
        int Send(byte[] dgram, int bytes);
        /// <summary>Receives a UDP datagram and inspects the remote endpoint.</summary>
        byte[] Receive(ref IPEndPoint remoteEP);
        /// <summary>Closes the UDP connection.</summary>
        void Close();
        /// <summary>Gets the number of bytes available to be read.</summary>
        int Available { get; }
        /// <summary>Gets the underlying Socket.</summary>
        Socket Client { get; }
        /// <summary>Gets the local endpoint that the socket is bound to.</summary>
        EndPoint? LocalEndPoint { get; }
    }
}
