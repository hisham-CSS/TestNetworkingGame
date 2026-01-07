using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bomberman
{
    public class NetworkManager
    {
        private UdpClient _udpClient;
        private IPEndPoint? _remoteEndPoint; // Usage: For Client, this is the Host.
        private List<IPEndPoint> _connectedClients = new List<IPEndPoint>(); // Usage: For Host, list of verified clients.
        private int _localPort;
        public int LocalPort => _localPort;

        public event Action<byte[], IPEndPoint>? OnPacketReceived;

        public NetworkManager(int localPort)
        {
            _localPort = localPort;
            _udpClient = new UdpClient(localPort);
            _udpClient.EnableBroadcast = true;
            _udpClient.Client.Blocking = false; // Non-blocking mode
        }

        public void Connect(string ip, int port)
        {
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public void AddClient(IPEndPoint client)
        {
            if (!_connectedClients.Contains(client))
            {
                _connectedClients.Add(client);
                Console.WriteLine($"[Network] Client Added: {client}");
            }
        }

        public void Send(byte[] data)
        {
            if (_remoteEndPoint == null) return;
            SendTo(data, _remoteEndPoint);
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

        public void Broadcast(byte[] data)
        {
            foreach(var client in _connectedClients)
            {
                SendTo(data, client);
            }
        }
        
        public void BroadcastToPort(byte[] data, int port)
        {
            SendTo(data, new IPEndPoint(IPAddress.Broadcast, port));
        }

        public void Update()
        {
            try
            {
                while (_udpClient.Available > 0)
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref sender);

                    // Auto-detect remote (Simple 1v1 legacy support, or initial handshake)
                    if (_remoteEndPoint == null && _connectedClients.Count == 0)
                    {
                        // _remoteEndPoint = sender; 
                        // Don't auto-assign in lobby mode, let Protocol handle it.
                    }

                    OnPacketReceived?.Invoke(data, sender);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Socket Error: {e.Message}");
            }
        }

        public void Close()
        {
            _udpClient?.Close();
        }
    }
}
