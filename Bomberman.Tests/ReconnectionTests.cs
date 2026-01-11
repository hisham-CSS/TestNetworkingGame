using NUnit.Framework;
using Bomberman.Core;
using Bomberman.Core.Game;
using Bomberman.Core.Rollback;
using System.Text.Json;

namespace Bomberman.Tests
{
    [TestFixture]
    public class ReconnectionTests
    {
        [Test]
        public void SerializeAndRestore_PreservesFrameAndRng()
        {
            // Setup a World with some state
            var world = new World();
            int rngSeed = 12345;
            uint rngResult = 0;
            
            // Advance RNG
            var rng = new DeterministicRandom(rngSeed);
            rng.Next();
            rngResult = rng.State;
            
            int frame = 100;
            uint nextEntityId = 5;
            world.NextEntityId = nextEntityId;

            // Serialize
            byte[] data = GameStateSnapshot.SerializeWorld(frame, world, rng.State);

            // Create new World
            var newWorld = new World();
            var newRng = new DeterministicRandom(99999); // Wrong seed

            // Restore
            int restoredFrame = GameStateSnapshot.RestoreFromBytes(newWorld, newRng, data);

            // Verify
            Assert.That(restoredFrame, Is.EqualTo(frame));
            Assert.That(newWorld.NextEntityId, Is.EqualTo(nextEntityId));
            Assert.That(newRng.State, Is.EqualTo(rngResult));
            
            // Verify next RNG call matches
            Assert.That(newRng.Next(), Is.EqualTo(rng.Next()));
        }

        [Test]
        public void SerializeAndRestore_PreservesEntities()
        {
            var world = new World();
            var e1 = world.CreateEntity();
            world.Transforms.Add(e1, new TransformComponent { Position = new IntVector2(10, 10), Size = new IntVector2(32, 32) });
            world.Players.Add(e1, new PlayerComponent { PlayerId = 1, Alive = true });

            byte[] data = GameStateSnapshot.SerializeWorld(50, world, 0);

            var newWorld = new World();
            GameStateSnapshot.RestoreFromBytes(newWorld, null, data);

            Assert.That(newWorld.Transforms.Count, Is.EqualTo(1));
            Assert.That(newWorld.Players.Count, Is.EqualTo(1));
            
            var t = newWorld.Transforms.Get(0);
            Assert.That(t.Position.X, Is.EqualTo(10));
            
            var p = newWorld.Players.Get(0);
            Assert.That(p.PlayerId, Is.EqualTo(1));
            
            // Verify Entity ID is preserved (CRITICAL)
            var restoredEntity = newWorld.Players.GetEntity(0);
            Assert.That(restoredEntity.Index, Is.EqualTo(e1.Index), "Entity Index must match original");
        }
        [Test]
        public void RestoreFromBytes_WithGarbageData_ThrowsException()
        {
            var world = new World();
            byte[] garbage = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            
            // Should throw, not crash the process (JsonException or Generic)
            Assert.Throws(Is.InstanceOf<Exception>(), () => 
            {
                GameStateSnapshot.RestoreFromBytes(world, null, garbage);
            });
        }

        [Test]
        public void RestoreFromBytes_WithEmptyData_ThrowsException()
        {
            var world = new World();
            byte[] empty = Array.Empty<byte>();

            Assert.Throws(Is.InstanceOf<Exception>(), () => 
            {
                GameStateSnapshot.RestoreFromBytes(world, null, empty);
            });
        }
    }
}
