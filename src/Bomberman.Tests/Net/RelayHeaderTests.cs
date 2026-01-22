using NUnit.Framework;
using Chronos.Net.Protocol;
using System.IO;

namespace Bomberman.Tests.Net
{
    [TestFixture]
    public class RelayHeaderTests
    {
        [Test]
        public void Serialize_Deserialize_JoinSession_PreservesData()
        {
            var original = new RelayHeader
            {
                PacketType = RelayPacketType.JoinSession,
                SessionId = 1234,
                SourcePlayerId = 5
            };

            var deserialized = SerializeDeserialize(original);

            Assert.That(deserialized.PacketType, Is.EqualTo(RelayPacketType.JoinSession));
            Assert.That(deserialized.SessionId, Is.EqualTo(1234));
            Assert.That(deserialized.SourcePlayerId, Is.EqualTo(5));
        }

        [Test]
        public void Serialize_Deserialize_RelayPacket_PreservesData()
        {
            var original = new RelayHeader
            {
                PacketType = RelayPacketType.RelayPacket,
                SessionId = 5555,
                SourcePlayerId = 0
            };

            var deserialized = SerializeDeserialize(original);

            Assert.That(deserialized.PacketType, Is.EqualTo(RelayPacketType.RelayPacket));
            Assert.That(deserialized.SessionId, Is.EqualTo(5555));
            Assert.That(deserialized.SourcePlayerId, Is.EqualTo(0));
        }

        [Test]
        public void Serialize_Deserialize_JoinSessionAck_PreservesData()
        {
             var original = new RelayHeader
            {
                PacketType = RelayPacketType.JoinSessionAck,
                SessionId = 9999,
                SourcePlayerId = 255
            };

            var deserialized = SerializeDeserialize(original);

            Assert.That(deserialized.PacketType, Is.EqualTo(RelayPacketType.JoinSessionAck));
            Assert.That(deserialized.SessionId, Is.EqualTo(9999));
            Assert.That(deserialized.SourcePlayerId, Is.EqualTo(255));
        }

        private RelayHeader SerializeDeserialize(RelayHeader original)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            original.Serialize(writer);
            
            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            
            return RelayHeader.Deserialize(reader);
        }
    }
}
