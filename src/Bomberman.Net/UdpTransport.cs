using System;
using System.Net;
using System.Net.Sockets;

namespace Bomberman.Net
{
    /// <summary>
    /// UDP implementation of <see cref="ITransport"/>. Binds a non-blocking socket, sends datagrams,
    /// and raises <see cref="PacketReceived"/> for each one polled. This is the concrete socket that
    /// the Week 2 ITransport seam was carved out for. Back-ported from Chronos.Net.UdpTransport.
    /// </summary>
    public class UdpTransport : ITransport
    {
        private UdpClient? _udpClient;
        private IPEndPoint? _connectedHost;
        private int _localPort;
        public int LocalPort => _localPort;

        public event Action<byte[], IPEndPoint>? PacketReceived;

        /// <summary>
        /// Binds to the preferred port, retrying the next few ports if it is busy. Retrying matters
        /// for tests and for running two peers on one machine (host 5000, client grabs 5001...).
        /// </summary>
        public UdpTransport(int preferredPort)
        {
            const int MaxRetries = 10;
            for (int i = 0; i < MaxRetries; i++)
            {
                int port = preferredPort + i;
                try
                {
                    _udpClient = new UdpClient(port);
                    _localPort = port;
                    _udpClient.EnableBroadcast = true;     // needed for LAN discovery
                    _udpClient.Client.Blocking = false;    // never block the game loop on recv
                    Console.WriteLine($"[Bomberman.Net] Bound to port {port}");
                    return;
                }
                catch (SocketException)
                {
                    Console.WriteLine($"[Bomberman.Net] Port {port} busy, trying next...");
                }
            }
            throw new Exception($"Failed to bind to any port starting from {preferredPort} after {MaxRetries} attempts.");
        }

        /// <inheritdoc/>
        public void Connect(string ip, int port)
        {
            _connectedHost = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        /// <inheritdoc/>
        public void SendToConnectedHost(byte[] data)
        {
            if (_connectedHost == null) return;
            SendTo(data, _connectedHost);
        }

        /// <inheritdoc/>
        public void SendTo(byte[] data, IPEndPoint target)
        {
            try
            {
                _udpClient?.Send(data, data.Length, target);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Send Error: {e.Message}");
            }
        }

        /// <summary>Drains all pending datagrams, raising PacketReceived for each. Non-blocking:
        /// when the socket is empty, Available is 0 and we return immediately.</summary>
        public void Poll()
        {
            try
            {
                while (_udpClient != null && _udpClient.Available > 0)
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref sender);

                    PacketReceived?.Invoke(data, sender);

                    if (_udpClient == null) break; // a callback may have disposed us
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Socket Error: {e.Message}");
            }
        }

        public void Dispose()
        {
            _udpClient?.Close();
            _udpClient = null;
        }
    }
}
