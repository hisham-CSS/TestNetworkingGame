using Bomberman.Core;
using Bomberman.Net.Protocol;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Bomberman.Tests.Net
{
    /// <summary>
    /// Verifies the binary protocol: every packet that goes out must come back identical. These are
    /// the round-trip guarantees lockstep relies on - a corrupted input packet desyncs both peers.
    /// </summary>
    public class ProtocolTests
    {
        [Test]
        public void InputPacket_SerializeDeserialize_RoundTrips()
        {
            int playerId = 1;
            int startFrame = 100;
            int posX = 50, posY = 60, hash = 9999;

            var a = new InputState { Movement = new Vector2(1, 0), PlaceBomb = true };
            var b = new InputState { Movement = new Vector2(0, -1), PlaceBomb = false };
            InputState[] history = { a, b };

            byte[] packet = NetworkProtocol<InputState>.CreateInputPacket(playerId, startFrame, history, posX, posY, hash);

            Assert.That(packet, Is.Not.Null);
            Assert.That(packet.Length, Is.GreaterThan(0));

            var (outPid, outFrame, outHistory, outX, outY, outHash) =
                NetworkProtocol<InputState>.ReadInputPacket(packet);

            Assert.That(outPid, Is.EqualTo(playerId));
            Assert.That(outFrame, Is.EqualTo(startFrame));
            Assert.That(outX, Is.EqualTo(posX));
            Assert.That(outY, Is.EqualTo(posY));
            Assert.That(outHash, Is.EqualTo(hash));
            Assert.That(outHistory.Length, Is.EqualTo(history.Length));
            Assert.That(outHistory[0].Movement, Is.EqualTo(a.Movement));
            Assert.That(outHistory[0].PlaceBomb, Is.EqualTo(a.PlaceBomb));
            Assert.That(outHistory[1].Movement, Is.EqualTo(b.Movement));
            Assert.That(outHistory[1].PlaceBomb, Is.EqualTo(b.PlaceBomb));
        }

        [Test]
        public void Welcome_CarriesAssignedIdSeedAndPlayerCount()
        {
            byte[] packet = NetworkProtocol<InputState>.CreateWelcome(assignedId: 3, seed: 54321, totalPlayers: 4);
            var (id, seed, total) = NetworkProtocol<InputState>.ReadWelcome(packet);
            Assert.That(id, Is.EqualTo(3));
            Assert.That(seed, Is.EqualTo(54321));
            Assert.That(total, Is.EqualTo(4));
        }

        [Test]
        public void StartGame_RoundTrips()
        {
            byte[] packet = NetworkProtocol<InputState>.CreateStartGame(seed: 777, totalPlayers: 2);
            var (seed, total) = NetworkProtocol<InputState>.ReadStartGame(packet);
            Assert.That(seed, Is.EqualTo(777));
            Assert.That(total, Is.EqualTo(2));
        }

        [Test]
        public void LobbyReady_RoundTrips()
        {
            byte[] packet = NetworkProtocol<InputState>.CreateLobbyReady(playerId: 1, isReady: true);
            var (pid, ready) = NetworkProtocol<InputState>.ReadLobbyReady(packet);
            Assert.That(pid, Is.EqualTo(1));
            Assert.That(ready, Is.True);
        }

        [Test]
        public void Ping_RoundTripsTimestamp()
        {
            long ts = 1234567890123L;
            byte[] packet = NetworkProtocol<InputState>.CreatePing(ts);
            Assert.That(NetworkProtocol<InputState>.ReadPing(packet), Is.EqualTo(ts));
        }

        [Test]
        public void ReadType_ReturnsLeadingByteAsPacketType()
        {
            byte[] packet = NetworkProtocol<InputState>.CreateWelcome(1, 2, 3);
            Assert.That(NetworkProtocol<InputState>.ReadType(packet),
                        Is.EqualTo(Bomberman.Net.Packets.PacketType.Welcome));
        }
    }
}
