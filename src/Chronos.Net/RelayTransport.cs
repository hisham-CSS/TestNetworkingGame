using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Chronos.Net.Protocol;

namespace Chronos.Net
{
    public class RelayTransport : ITransport
    {
        private UdpClient _udpClient;
        private IPEndPoint _relayServerEndpoint;
        private ushort _sessionId;
        private byte _localPlayerId;
        
        public event Action<byte[], IPEndPoint>? PacketReceived;

        public int LocalPort => ((IPEndPoint)_udpClient.Client.LocalEndPoint!).Port;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relayIp">The public IP of the relay server.</param>
        /// <param name="relayPort">The port of the relay server (usually 7777).</param>
        /// <param name="sessionId">The game session ID.</param>
        /// <param name="localPlayerId">
        /// For Host: 0. 
        /// For Joining Clients: Random byte (10-250) initially, then assigned ID after handshake? 
        /// Actually, for the simplified architecture, let's just use the assigned PlayerID or a Random ID for specific phases.
        /// </param>
        public RelayTransport(string relayIp, int relayPort, ushort sessionId, byte localPlayerId)
        {
            _relayServerEndpoint = new IPEndPoint(IPAddress.Parse(relayIp), relayPort);
            _sessionId = sessionId;
            _localPlayerId = localPlayerId;
            
            _udpClient = new UdpClient();
            _udpClient.Connect(_relayServerEndpoint);
            
            // Send Join Packet immediately
            SendControlPacket(RelayPacketType.JoinSession);
        }

        private void SendControlPacket(RelayPacketType type)
        {
            var header = new RelayHeader
            {
                PacketType = type,
                SessionId = _sessionId,
                SourcePlayerId = _localPlayerId
            };
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            header.Serialize(writer);
            
            byte[] packet = ms.ToArray();
            _udpClient.Send(packet, packet.Length);
        }

        public void Connect(string ip, int port)
        {
            // No-Op for RelayTransport as we are already connected to the Relay Server.
            // In a direct UDP transport, this sets the target. Here, everything goes to Relay.
        }

        public void SendToConnectedHost(byte[] data)
        {
            // Send to Host via Relay
            SendTo(data, null!); // Target is irrelevant, Relay broadcasts to others in session
        }

        public void SendTo(byte[] data, IPEndPoint target)
        {
            // Wrap the game packet
            var header = new RelayHeader
            {
                PacketType = RelayPacketType.RelayPacket,
                SessionId = _sessionId,
                SourcePlayerId = _localPlayerId
            };

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            header.Serialize(writer);
            writer.Write(data); 

            byte[] fullPacket = ms.ToArray();
            try 
            {
                _udpClient.Send(fullPacket, fullPacket.Length);
            }
            catch (Exception) { }
        }

        public event Action? OnConnected;
        public bool IsConnected { get; private set; } = false;

        public void Poll()
        {
            try
            {
                while (_udpClient.Available > 0)
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref sender);

                    if (!sender.Equals(_relayServerEndpoint)) continue;

                    using var ms = new MemoryStream(data);
                    using var reader = new BinaryReader(ms);
                    
                    var header = RelayHeader.Deserialize(reader);
                    
                    if (header.PacketType == RelayPacketType.RelayPacket)
                    {
                        int payloadLength = (int)(ms.Length - ms.Position);
                        byte[] payload = reader.ReadBytes(payloadLength);

                        IPEndPoint virtualSender = new IPEndPoint(IPAddress.Loopback, header.SourcePlayerId);
                        
                        PacketReceived?.Invoke(payload, virtualSender);
                    }
                    else if (header.PacketType == RelayPacketType.JoinSessionAck)
                    {
                         if (!IsConnected)
                         {
                             IsConnected = true;
                             OnConnected?.Invoke();
                         }
                    }
                }
            }
            catch (Exception) { }
        }


        public void SetPlayerId(byte playerId)
        {
            _localPlayerId = playerId;
        }

        public void Close()
        {
            try { SendControlPacket(RelayPacketType.LeaveSession); } catch {}
            _udpClient.Close();
        }

        public void Dispose()
        {
            Close();
        }
    }
}
