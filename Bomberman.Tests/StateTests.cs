using NUnit.Framework;
using Bomberman;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Bomberman.Tests
{
    public class StateTests
    {
        [Test]
        public void StateHasher_Deterministic()
        {
            // Arrange
            var world1 = new World();
            var world2 = new World();

            // Add identical entities
            Entity pid = world1.CreateEntity();
            world1.Players.Add(pid, new PlayerComponent { PlayerId = 0 });
            world1.Transforms.Add(pid, new TransformComponent { Position = new Vector2(100, 100) });

            Entity pid2 = world2.CreateEntity();
            world2.Players.Add(pid2, new PlayerComponent { PlayerId = 0 });
            world2.Transforms.Add(pid2, new TransformComponent { Position = new Vector2(100, 100) });

            // Act
            int hash1 = StateHasher.Hash(world1);
            int hash2 = StateHasher.Hash(world2);

            // Assert
            Assert.That(hash1, Is.EqualTo(hash2));
        }

        [Test]
        public void StateHasher_ChangesOnPositionDifference()
        {
             // Arrange
            var world1 = new World();
            var world2 = new World();

            Entity pid = world1.CreateEntity();
            world1.Players.Add(pid, new PlayerComponent { PlayerId = 0 });
            world1.Transforms.Add(pid, new TransformComponent { Position = new Vector2(100, 100) });

            Entity pid2 = world2.CreateEntity();
            world2.Players.Add(pid2, new PlayerComponent { PlayerId = 0 });
            world2.Transforms.Add(pid2, new TransformComponent { Position = new Vector2(100, 101) }); // Slight diff

            // Act
            int hash1 = StateHasher.Hash(world1);
            int hash2 = StateHasher.Hash(world2);

            // Assert
            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void GameStateSnapshot_Restore_Correctly()
        {
            // Arrange
            var world = new World();
            Entity e = world.CreateEntity();
            world.Transforms.Add(e, new TransformComponent { Position = new Vector2(50, 50) });
            
            // Create Snapshot of state A
            var snapshotA = new GameStateSnapshot(1, world);

            // Modify state to B
            var t = world.Transforms.Get(e);
            t.Position = new Vector2(100, 100);
            world.Transforms.Remove(e);
            world.Transforms.Add(e, t);
            
            // Restore A
            snapshotA.Restore(world);

            // Assert
            Assert.That(world.Transforms.Get(e).Position.X, Is.EqualTo(50));
            Assert.That(world.Transforms.Get(e).Position.Y, Is.EqualTo(50));
        }
    }
}
