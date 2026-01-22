using NUnit.Framework;
using Chronos.Net;
using Chronos.Net.Protocol;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System;

namespace Bomberman.Tests.Net
{
    [TestFixture]
    public class RelayTransportTests
    {
        private Mock<IUdpClient> _mockUdp;
        private RelayTransport _transport;
        private string _relayIp = "127.0.0.1";
        private int _relayPort = 7777;
        private ushort _sessionId = 1234;
        private byte _playerId = 10;

        [SetUp]
        public void Setup()
        {
            _mockUdp = new Mock<IUdpClient>();
            // Local endpoint setup
            var localEP = new IPEndPoint(IPAddress.Loopback, 50000);
            _mockUdp.SetupGet(x => x.LocalEndPoint).Returns(localEP);

            _transport = new RelayTransport(_relayIp, _relayPort, _sessionId, _playerId, _mockUdp.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _transport?.Dispose();
        }

        [Test]
        public void Constructor_SendsJoinSessionPacket()
        {
            // Verify that upon construction, a packet is sent to the relay server
            _mockUdp.Verify(x => x.Send(It.IsAny<byte[]>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void SendTo_WrapsPacketInRelayHeader()
        {
            byte[] gameData = new byte[] { 0xAA, 0xBB };
            _transport.SendTo(gameData, new IPEndPoint(IPAddress.Loopback, 1234)); // Pass valid dummy endpoint

            _mockUdp.Verify(x => x.Send(It.Is<byte[]>(data => VerifyRelayPacket(data, RelayPacketType.RelayPacket, _sessionId, _playerId)), It.IsAny<int>()), Times.AtLeastOnce);
        }

        [Test]
        public void Poll_ReceiveAck_SetsIsConnected()
        {
            // Prepare Ack Packet
            var header = new RelayHeader { PacketType = RelayPacketType.JoinSessionAck, SessionId = _sessionId, SourcePlayerId = _playerId };
            byte[] packet = SerializeHeader(header);

            // Mock Receive
            var remoteEP = new IPEndPoint(IPAddress.Parse(_relayIp), _relayPort);
            
            _mockUdp.SetupSequence(x => x.Available)
                .Returns(1)
                .Returns(0);

            // Use delegate to set ref parameter and return packet
            _mockUdp.Setup(x => x.Receive(ref It.Ref<IPEndPoint>.IsAny))
                .Returns((ref IPEndPoint ep) => 
                { 
                    ep = remoteEP; 
                    return packet; 
                });

            bool connectedTriggered = false;
            _transport.OnConnected += () => connectedTriggered = true;

            _transport.Poll();

            Assert.That(_transport.IsConnected, Is.True);
            Assert.That(connectedTriggered, Is.True);
        }

        [Test]
        public void Close_SendsLeaveSessionPacket()
        {
            _transport.Close();
            // Should send LeaveSession (Type 2) verification
             _mockUdp.Verify(x => x.Send(It.Is<byte[]>(data => VerifyRelayPacket(data, RelayPacketType.LeaveSession, _sessionId, _playerId)), It.IsAny<int>()), Times.Once);
        }

        private bool VerifyRelayPacket(byte[] data, RelayPacketType expectedType, ushort expectedSession, byte expectedPlayer)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            var header = RelayHeader.Deserialize(reader);
            
            return header.PacketType == expectedType && 
                   header.SessionId == expectedSession && 
                   header.SourcePlayerId == expectedPlayer;
        }

        private byte[] SerializeHeader(RelayHeader header)
        {
             using var ms = new MemoryStream();
             using var writer = new BinaryWriter(ms);
             header.Serialize(writer);
             return ms.ToArray();
        }
    }
}
