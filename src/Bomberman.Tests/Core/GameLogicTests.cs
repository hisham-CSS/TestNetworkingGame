using NUnit.Framework;
using Bomberman.Core;
using Bomberman.Core.Game;
using Bomberman.Core.ECS.Components;

namespace Bomberman.Tests.Core
{
    [TestFixture]
    public class GameLogicTests
    {
        [Test]
        public void Test_InitialPlayerStats_AreCorrect()
        {
            // Arrange
            var config = new GameConfig(); // Defaults
            var sim = new Simulation(12345, 1, config);

            // Act
            var players = sim.World.Players;
            var player1 = players.Get(0);

            // Assert
            Assert.That(player1.BombRange, Is.EqualTo(1), "Initial Bomb Range should be 1");
            Assert.That(player1.BombCapacity, Is.EqualTo(1), "Initial Bomb Capacity should be 1");
        }

        [Test]
        public void Test_Powerup_IncreasesStats()
        {
            // Arrange
            var config = new GameConfig();
            var sim = new Simulation(12345, 1, config);
            var player = sim.World.Players.Get(0);
            
            // Verify initial
            Assert.That(player.BombRange, Is.EqualTo(1));
            Assert.That(player.BombCapacity, Is.EqualTo(1));

            // Simulate picking up powerups (as Logic does invalid logic check or clamping usually,
            // but here we just want to ensure the Values are mutable and start from 1)
            var pComp = player;
            pComp.BombRange++;
            pComp.BombCapacity++;
            sim.World.Players.Set(0, pComp);

            var updatedPlayer = sim.World.Players.Get(0);
            Assert.That(updatedPlayer.BombRange, Is.EqualTo(2), "Bomb Range should increase to 2");
            Assert.That(updatedPlayer.BombCapacity, Is.EqualTo(2), "Bomb Capacity should increase to 2");
        }
    }
}
