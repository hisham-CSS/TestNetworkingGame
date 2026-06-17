using NUnit.Framework;
using Bomberman.Core;
using Microsoft.Xna.Framework;

namespace Bomberman.Tests
{
    [TestFixture]
    public class GameLogicTests
    {
        private const float Dt = 1f / 60f;
        private static InputState[] NoInput() =>
            new[] { new InputState { Movement = Vector2.Zero, PlaceBomb = false } };

        private static void SpawnPowerupOnPlayer(Simulation sim, PowerupComponent.PowerupType type)
        {
            var e = sim.World.CreateEntity();
            sim.World.Powerups.Add(e, new PowerupComponent { Type = type });
            sim.World.Transforms.Add(e, new TransformComponent { Position = new Vector2(32, 32), Size = new Vector2(16, 16) });
        }

        [Test]
        public void Test_InitialPlayerStats_AreCorrect()
        {
            var sim = new Simulation(12345);
            var player1 = sim.World.Players.Get(0);
            Assert.That(player1.BombRange, Is.EqualTo(1), "Initial Bomb Range should be 1");
            Assert.That(player1.BombCapacity, Is.EqualTo(1), "Initial Bomb Capacity should be 1");
        }

        [Test]
        public void Test_Powerup_IncreasesStats()
        {
            var sim = new Simulation(12345);
            SpawnPowerupOnPlayer(sim, PowerupComponent.PowerupType.Range);
            sim.Update(NoInput(), Dt);
            var afterRange = sim.World.Players.Get(0);
            Assert.That(afterRange.BombRange, Is.EqualTo(2), "Range powerup should raise range to 2");
            Assert.That(afterRange.BombCapacity, Is.EqualTo(1));

            SpawnPowerupOnPlayer(sim, PowerupComponent.PowerupType.Capacity);
            sim.Update(NoInput(), Dt);
            var afterCap = sim.World.Players.Get(0);
            Assert.That(afterCap.BombCapacity, Is.EqualTo(2), "Capacity powerup should raise capacity to 2");
            Assert.That(afterCap.BombRange, Is.EqualTo(2));
        }
    }
}
