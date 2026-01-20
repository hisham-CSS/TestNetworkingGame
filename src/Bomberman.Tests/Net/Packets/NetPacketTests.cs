using System.IO;
using Bomberman.Core;
using Bomberman.Core.Input;
using Chronos.Net.Packets;
using NUnit.Framework;

namespace Bomberman.Tests.Net.Packets
{
    [TestFixture]
    public class NetPacketTests
    {
        [Test]
        public void InputPacket_RoundTrip_PreservesData()
        {
            var original = new InputPacket<InputState>
            {
                PlayerId = 1,
                StartFrame = 100,
                PosX = 5,
                PosY = 5,
                StateHash = 999,
                Inputs = new InputState[]
                {
                    new InputState { Movement = new IntVector2(1, 0), PlaceBomb = true, BombTarget = new IntVector2(2, 2) }
                }
            };

            var deserialized = SerializeDeserialize(original, InputPacket<InputState>.Deserialize);

            Assert.That(deserialized.PlayerId, Is.EqualTo(original.PlayerId));
            Assert.That(deserialized.StartFrame, Is.EqualTo(original.StartFrame));
            Assert.That(deserialized.Inputs.Length, Is.EqualTo(1));
            Assert.That(deserialized.Inputs[0].PlaceBomb, Is.True);
        }

        [Test]
        public void InputPacket_MaliciousCount_ReturnsEmpty()
        {
            // Simulate a packet header claiming 1000 input states but providing no data
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write(1); // PlayerId
            writer.Write(100); // StartFrame
            writer.Write(1000); // Claims 1000 inputs
            // No more data written

            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            
            var packet = InputPacket<InputState>.Deserialize(reader);
            
            // Should return empty inputs due to hardening check
            Assert.That(packet.Inputs, Is.Empty);
        }

        [Test]
        public void LobbyUpdatePacket_RoundTrip_PreservesData()
        {
            var original = new LobbyUpdatePacket
            {
                ConnectedCount = 2,
                TotalPlayers = 4,
                SlotMask = 3
            };

            var deserialized = SerializeDeserialize(original, LobbyUpdatePacket.Deserialize);

            Assert.That(deserialized.ConnectedCount, Is.EqualTo(2));
            Assert.That(deserialized.TotalPlayers, Is.EqualTo(4));
            Assert.That(deserialized.SlotMask, Is.EqualTo(3));
        }

        [Test]
        public void LobbyUpdatePacket_OldData_HandlesMissingSlotMask()
        {
            // Simulate old packet format missing SlotMask
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write(2); // ConnectedCount
            writer.Write(4); // TotalPlayers
            // SlotMask missing

            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            
            var packet = LobbyUpdatePacket.Deserialize(reader);
            
            Assert.That(packet.ConnectedCount, Is.EqualTo(2));
            Assert.That(packet.SlotMask, Is.EqualTo(0)); // Default int value
        }

        [Test]
        public void StateSyncPacket_RoundTrip_PreservesData()
        {
            var original = new StateSyncPacket
            {
                Data = new byte[] { 0xAA, 0xBB, 0xCC }
            };

            var deserialized = SerializeDeserialize(original, StateSyncPacket.Deserialize);

            Assert.That(deserialized.Data, Is.EqualTo(original.Data));
        }

        private T SerializeDeserialize<T>(T packet, System.Func<BinaryReader, T> deserializer) where T : IPacket
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            packet.Serialize(writer);
            
            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            reader.ReadByte(); // Consume PacketType
            return deserializer(reader);
        }
    }
}
