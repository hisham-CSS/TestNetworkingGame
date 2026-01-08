using NUnit.Framework;
using System.Collections.Generic;
using Bomberman.Core;
using Bomberman.Core.Game;
using Bomberman.Core.Rollback;
using Bomberman.Net;

namespace Bomberman.Tests
{
    public class ReplayTests
    {
        [Test]
        public void Simulation_IsDeterministic_WithSameInputs()
        {
            // Arrange
            int seed = 12345;
            int framesToSimulate = 100;
            InputState[] inputs = GenerateRandomInputs(framesToSimulate, seed);

            // Act - Run 1
            var sim1 = new Simulation(seed, 2);
            // SpawnPlayers handled in constructor
            List<int> hashes1 = RunSimulation(sim1, inputs, framesToSimulate);

            // Act - Run 2
            var sim2 = new Simulation(seed, 2);
            List<int> hashes2 = RunSimulation(sim2, inputs, framesToSimulate);

            // Assert
            Assert.That(hashes1.Count, Is.EqualTo(hashes2.Count));
            for(int i=0; i<hashes1.Count; i++)
            {
                Assert.That(hashes1[i], Is.EqualTo(hashes2[i]), $"Mismatch at Frame {i}");
            }
        }

        private List<int> RunSimulation(Simulation sim, InputState[] inputs, int frames)
        {
            var hashes = new List<int>();
            float dt = 1.0f / 60.0f;
            for(int i=0; i<frames; i++)
            {
                // Create inputs for 2 players
                var frameInputs = new InputState[2]; 
                // We'll reuse the same random input for P0 and P1 for simplicity
                frameInputs[0] = inputs[i]; 
                frameInputs[1] = inputs[i]; 

                sim.Update(frameInputs, dt);
                hashes.Add(StateHasher.Hash(sim.World));
            }
            return hashes;
        }

        private InputState[] GenerateRandomInputs(int count, int seed)
        {
            var rng = new System.Random(seed);
            var result = new InputState[count];
            for(int i=0; i<count; i++)
            {
                int x = rng.Next(-1, 2);
                int y = rng.Next(-1, 2);
                bool bomb = rng.NextDouble() > 0.9;
                result[i] = new InputState 
                { 
                    Movement = new IntVector2(x,y), 
                    PlaceBomb = bomb,
                    BombTarget = new Microsoft.Xna.Framework.Point(0,0) // Simplification
                };
            }
            return result;
        }
    }
}
