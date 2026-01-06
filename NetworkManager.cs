using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bomberman
{
    public class NetworkManager
    {
        private UdpClient _udpClient;
        private IPEndPoint? _remoteEndPoint;
        private int _localPort;

        public event Action<byte[]>? OnPacketReceived;

        public NetworkManager(int localPort)
        {
            _localPort = localPort;
            _udpClient = new UdpClient(localPort);
            _udpClient.Client.Blocking = false; // Non-blocking mode
        }

        public void Connect(string ip, int port)
        {
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public void Send(byte[] data)
        {
            if (_remoteEndPoint == null) return;
            try
            {
                _udpClient.Send(data, data.Length, _remoteEndPoint);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Send Error: {e.Message}");
            }
        }

        public void Update()
        {
            try
            {
                while (_udpClient.Available > 0)
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref sender);

                    // If we haven't officially connected efficiently (e.g. host waiting for join),
                    // we might want to accept this sender as the remote.
                    // For now, let's assume explicit Connect() or strict topology for simplicity,
                    // OR auto-connect on first packet for Host.
                    if (_remoteEndPoint == null)
                    {
                        _remoteEndPoint = sender;
                        Console.WriteLine($"[Network] Connected to remote: {sender}");
                    }

                    OnPacketReceived?.Invoke(data);
                }
            }
            catch (SocketException e)
            {
                // Ignore "WouldBlock" errors if any, though Available check avoids most
                Console.WriteLine($"Socket Error: {e.Message}");
            }
        }

        public void Close()
        {
            _udpClient?.Close();
        }
    }
}
