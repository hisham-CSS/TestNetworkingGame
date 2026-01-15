using System;
using System.Collections.Generic;
using System.Net;
using Bomberman.Net;

namespace Bomberman.Tests.Mocks
{
    public class MockTransport : ITransport
    {
        public Queue<(byte[] Data, IPEndPoint Endpoint)> SentPackets { get; } = new();
        public Queue<(byte[] Data, IPEndPoint Endpoint)> IncomingPackets { get; } = new();
        
        public event Action<byte[], IPEndPoint>? PacketReceived;

        public bool IsRunning { get; private set; }
        public int LocalPort { get; set; } = 3000;

        // Track connection
        public string? ConnectedHost { get; private set; }
        public int ConnectedPort { get; private set; }

        public void Start(int port)
        {
            IsRunning = true;
            LocalPort = port;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Connect(string ip, int port)
        {
            ConnectedHost = ip;
            ConnectedPort = port;
        }

        public void SendToConnectedHost(byte[] data)
        {
            if (ConnectedHost != null)
            {
               SendTo(data, new IPEndPoint(IPAddress.Parse(ConnectedHost), ConnectedPort));
            }
        }

        public void SendTo(byte[] data, IPEndPoint target)
        {
            SentPackets.Enqueue((data, target));
        }

        public void Poll()
        {
             // Process incoming queue and fire event
             while (IncomingPackets.Count > 0)
             {
                 var packet = IncomingPackets.Dequeue();
                 PacketReceived?.Invoke(packet.Data, packet.Endpoint);
             }
        }

        // Helper to simulate receiving a packet
        public void SimulateReceive(byte[] data, IPEndPoint sender)
        {
            IncomingPackets.Enqueue((data, sender));
        }
    }
}
