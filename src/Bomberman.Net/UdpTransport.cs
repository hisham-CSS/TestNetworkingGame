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
                    DisableUdpConnReset(_udpClient);       // Windows: don't throw on ICMP port-unreachable
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

        /// <summary>
        /// Windows-only quirk: after we send a datagram to a closed port (e.g. probing a LAN port range
        /// for hosts), the OS posts an ICMP "port unreachable" and the NEXT Receive on this socket throws
        /// SocketException 10054 ("forcibly closed"). Disabling SIO_UDP_CONNRESET stops that, so one peer
        /// probing empty ports can no longer break its own receive loop. No-op on Linux/macOS.
        /// </summary>
        private static void DisableUdpConnReset(UdpClient client)
        {
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452; // 0x9800000C
                client.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch { /* not supported on this platform; safe to ignore */ }
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
            while (_udpClient != null && _udpClient.Available > 0)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                byte[] data;
                try
                {
                    data = _udpClient.Receive(ref sender);
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
                {
                    continue; // a previous send hit a closed port; ignore and keep draining
                }
                catch (SocketException e)
                {
                    Console.WriteLine($"Socket Error: {e.Message}");
                    break;
                }

                PacketReceived?.Invoke(data, sender);
                if (_udpClient == null) break; // a callback may have disposed us
            }
        }

        public void Dispose()
        {
            _udpClient?.Close();
            _udpClient = null;
        }
    }
}
