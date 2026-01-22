using System;
using System.Net;
using System.Net.Sockets;

namespace Chronos.Net
{
    /// <summary>
    /// Default implementation of IUdpClient wrapping System.Net.Sockets.UdpClient.
    /// </summary>
    public class UdpClientWrapper : IUdpClient
    {
        private readonly UdpClient _client;

        public UdpClientWrapper()
        {
            _client = new UdpClient();
        }

        public UdpClientWrapper(int port)
        {
            _client = new UdpClient(port);
        }

        public int Available => _client.Available;
        public Socket Client => _client.Client;
        public EndPoint? LocalEndPoint => _client.Client.LocalEndPoint;

        public void Connect(IPEndPoint endPoint) => _client.Connect(endPoint);

        public int Send(byte[] dgram, int bytes) => _client.Send(dgram, bytes);

        public byte[] Receive(ref IPEndPoint remoteEP) => _client.Receive(ref remoteEP);

        public void Close() => _client.Close();

        public void Dispose() => _client.Dispose();
    }
}
