using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Bomberman.Core;

namespace Bomberman.Net
{
    /// <summary>
    /// UDP implementation of the transport layer.
    /// Handles socket binding, sending, and non-blocking receiving.
    /// </summary>
    public class UdpTransport : ITransport
    {
        private UdpClient _udpClient;
        private IPEndPoint? _connectedHost; 
        private int _localPort;
        public int LocalPort => _localPort;

        public event Action<byte[], IPEndPoint>? PacketReceived;

        /// <summary>
        /// Initializes the UDP transport, attempting to bind to the preferred port.
        /// Retries on subsequent ports if the preferred one is busy.
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
                    _udpClient.EnableBroadcast = true;
                    _udpClient.Client.Blocking = false;
                    Console.WriteLine($"[Network] Bound to port {port}");
                    return;
                }
                catch (SocketException)
                {
                    Console.WriteLine($"[Network] Port {port} matches existing usage, trying next...");
                }
            }
            throw new Exception($"Failed to bind to any port starting from {preferredPort} after {MaxRetries} attempts.");
        }

        public void Connect(string ip, int port)
        {
            _connectedHost = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public void SendToConnectedHost(byte[] data)
        {
            if (_connectedHost == null) return;
            SendTo(data, _connectedHost);
        }

        public void SendTo(byte[] data, IPEndPoint target)
        {
            try
            {
                _udpClient.Send(data, data.Length, target);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Send Error: {e.Message}");
            }
        }

        /// <summary>
        /// Checks for pending data on the socket and invokes PacketReceived.
        /// </summary>
        public void Poll()
        {
            try
            {
                while (_udpClient != null && _udpClient.Available > 0)
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref sender);

                    PacketReceived?.Invoke(data, sender);
                    
                    // Check again in case callback closed us
                    if (_udpClient == null) break;
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Socket Error: {e.Message}");
            }
            catch (ObjectDisposedException)
            {
                // Socket was closed during event handling (e.g. Disconnect -> Close)
                return;
            }
        }

        public void Close()
        {
            _udpClient?.Close();
            _udpClient = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
