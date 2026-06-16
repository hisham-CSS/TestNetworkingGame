using NUnit.Framework;
using Bomberman;
using Microsoft.Xna.Framework;

namespace Bomberman.Tests
{
    /// <summary>
    /// Learning Activity 1 required tests. These exercise the deterministic Simulation directly,
    /// with no graphics or window, which is possible precisely because the Logic layer
    /// (Simulation/World/components) has no dependency on the View layer.
    /// </summary>
    [TestFixture]
    public class GameLogicTests
    {
        private const float Dt = 1f / 60f;

        // One player, no movement, no bomb — used to advance the sim a single tick.
        private static InputState[] NoInput() =>
            new[] { new InputState { Movement = Vector2.Zero, PlaceBomb = false } };

        // Drop a powerup of the given type directly on top of the player at spawn (tile (1,1) => 32,32).
        private static void SpawnPowerupOnPlayer(Simulation sim, PowerupComponent.PowerupType type)
        {
            var e = sim.World.CreateEntity();
            sim.World.Powerups.Add(e, new PowerupComponent { Type = type });
            sim.World.Transforms.Add(e, new TransformComponent
            {
                Position = new Vector2(32, 32), // player spawns here; rects overlap
                Size = new Vector2(16, 16)
            });
        }

        [Test]
        public void Test_InitialPlayerStats_AreCorrect()
        {
            // Arrange
            var sim = new Simulation(12345); // seeded, single player

            // Act
            var player1 = sim.World.Players.Get(0);

            // Assert
            Assert.That(player1.BombRange, Is.EqualTo(1), "Initial Bomb Range should be 1");
            Assert.That(player1.BombCapacity, Is.EqualTo(1), "Initial Bomb Capacity should be 1");
        }

        [Test]
        public void Test_Powerup_IncreasesStats()
        {
            // Arrange
            var sim = new Simulation(12345);
            Assert.That(sim.World.Players.Get(0).BombRange, Is.EqualTo(1));
            Assert.That(sim.World.Players.Get(0).BombCapacity, Is.EqualTo(1));

            // Act 1: pick up a Range powerup by running one simulation tick.
            SpawnPowerupOnPlayer(sim, PowerupComponent.PowerupType.Range);
            sim.Update(NoInput(), Dt);

            var afterRange = sim.World.Players.Get(0);
            Assert.That(afterRange.BombRange, Is.EqualTo(2), "Bomb Range should increase to 2 after Range powerup");
            Assert.That(afterRange.BombCapacity, Is.EqualTo(1), "Capacity should be unchanged by a Range powerup");

            // Act 2: pick up a Capacity powerup.
            SpawnPowerupOnPlayer(sim, PowerupComponent.PowerupType.Capacity);
            sim.Update(NoInput(), Dt);

            var afterCapacity = sim.World.Players.Get(0);
            Assert.That(afterCapacity.BombCapacity, Is.EqualTo(2), "Bomb Capacity should increase to 2 after Capacity powerup");
            Assert.That(afterCapacity.BombRange, Is.EqualTo(2), "Range should remain 2");
        }
    }
}
