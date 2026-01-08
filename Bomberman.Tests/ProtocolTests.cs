using NUnit.Framework;

using System.Collections.Generic;
using Bomberman.Core;
using Bomberman.Net;

namespace Bomberman.Tests
{
    public class ProtocolTests
    {
        [Test]
        public void CreateInputPacket_SerializeDeserialize_Correctly()
        {
            // Arrange
            int playerId = 1;
            int currentFrame = 100;
            IntVector2 position = new IntVector2(50, 60);
            int hash = 9999;
            
            var input1 = new InputState { Movement = new IntVector2(1, 0), PlaceBomb = true, BombTarget = new IntVector2(5, 5) };
            var input2 = new InputState { Movement = new IntVector2(0, -1), PlaceBomb = false, BombTarget = new IntVector2(0, 0) };
            InputState[] history = new InputState[] { input1, input2 };

            // Act
            byte[] packet = NetworkProtocol.CreateInputPacket(playerId, currentFrame, history, position, hash);
            
            // Assert
            Assert.That(packet, Is.Not.Null);
            Assert.That(packet.Length, Is.GreaterThan(0));

            // Decode
            var (outPid, outFrame, outHistory, outPos, outHash) = NetworkProtocol.ReadInputPacket(packet);

            Assert.That(outPid, Is.EqualTo(playerId));
            Assert.That(outFrame, Is.EqualTo(currentFrame));
            Assert.That(outPos, Is.EqualTo(position));
            Assert.That(outHash, Is.EqualTo(hash));
            Assert.That(outHistory.Length, Is.EqualTo(history.Length));
            
            Assert.That(outHistory[0].Movement, Is.EqualTo(input1.Movement));
            Assert.That(outHistory[0].PlaceBomb, Is.EqualTo(input1.PlaceBomb));
            Assert.That(outHistory[0].BombTarget, Is.EqualTo(input1.BombTarget));

            Assert.That(outHistory[1].Movement, Is.EqualTo(input2.Movement));
            Assert.That(outHistory[1].PlaceBomb, Is.EqualTo(input2.PlaceBomb));
        }

        [Test]
        public void CreateWelcome_SerializeDeserialize_Correctly()
        {
            int assignedId = 3;
            int seed = 54321;
            int totalPlayers = 4;

            byte[] packet = NetworkProtocol.CreateWelcome(assignedId, seed, totalPlayers);
            
            var (outId, outSeed, outTotal) = NetworkProtocol.ReadWelcome(packet);

            Assert.That(outId, Is.EqualTo(assignedId));
            Assert.That(outSeed, Is.EqualTo(seed));
            Assert.That(outTotal, Is.EqualTo(totalPlayers));
        }

        [Test]
        public void CreateStartGame_SerializeDeserialize_Correctly()
        {
            int seed = 111;
            int totalPlayers = 2;

            byte[] packet = NetworkProtocol.CreateStartGame(seed, totalPlayers);
            
            var (outSeed, outTotal) = NetworkProtocol.ReadStartGame(packet);

            Assert.That(outSeed, Is.EqualTo(seed));
            Assert.That(outTotal, Is.EqualTo(totalPlayers));
        }
    }
}
