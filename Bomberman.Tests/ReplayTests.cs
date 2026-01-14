using NUnit.Framework;
using System;
using Bomberman.Core.Input;
using System.Collections.Generic;
using Bomberman.Core;
using Bomberman.Core.Game;
using Bomberman.Core.Rollback;
using Bomberman.Net;

namespace Bomberman.Tests
{
    /// <summary>
    /// Tests for deterministic simulation behavior and replay system verification.
    /// Ensures that the same inputs produce the same game state across multiple runs.
    /// </summary>
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
                    BombTarget = new IntVector2(0,0) // Simplification
                };
            }
            return result;
        }
        [Test]
        public void Simulation_DetectsGameOver_WhenPlayerDies()
        {
            // Arrange
            int seed = 555;
            var sim = new Simulation(seed, 2);
            
            // P0 starts at 1,1 (Top Left)
            // P1 starts at 13,11 (Bottom Right)
            // We want P0 to place a bomb and die.
            
            // Frame 0: P0 places bomb at 1,1 (where they are)
            var inputBomb = new InputState { PlaceBomb = true, BombTarget = new IntVector2(1, 1) };
            var inputIdle = new InputState();
            
            sim.Update(new InputState[] { inputBomb, inputIdle }, 0.016f); // 16ms
            
            // Check bomb spawned
            Assert.That(sim.World.Bombs.Count, Is.GreaterThan(0));

            // Run for 4 seconds (approx 240 frames)
            // Bomb should explode around 3s
            for(int i=0; i<240; i++)
            {
                sim.Update(new InputState[] { inputIdle, inputIdle }, 0.016f);
            }
            
            // Assert
            // P0 should be dead (Alive = false)
            // P1 should be winner
            
            bool p0Alive = false;
            var players = sim.World.Players;
            for(int i=0; i<players.Count; i++)
            {
                if (players.Get(i).PlayerId == 0)
                {
                    p0Alive = players.Get(i).Alive;
                }
            }
            
            Assert.That(p0Alive, Is.False, "Player 0 should have died from their own bomb");
            Assert.That(sim.WinnerId, Is.EqualTo(1), "Player 1 should be the winner");
            Assert.That(sim.IsGameOver, Is.True, "Game should be over");
        }

        [Test]
        public void Simulation_HandlesPlayerDisconnect_Gracefully()
        {
            // Scenario: Player 1 stops sending inputs (Disconnects).
            // The system (or InputRecorder playback) should feed default inputs.
            // verifying that the simulation doesn't crash/hang.

            int seed = 999;
            var sim = new Simulation(seed, 2);
            
            // Phase 1: Normal Play (Both move)
            var moveRight = new InputState { Movement = new IntVector2(1, 0) };
            
            for(int i=0; i<60; i++)
            {
                sim.Update(new InputState[] { moveRight, moveRight }, 0.016f);
            }

            // Phase 2: Player 1 Disconnects (Feeds default input)
            // Player 0 continues
            var p1Default = new InputState(); // No move, No bomb

            for(int i=0; i<60; i++)
            {
                // Assert no exception thrown
                Assert.DoesNotThrow(() => sim.Update(new InputState[] { moveRight, p1Default }, 0.016f));
            }

            // Verify P1 is still "Alive" but stationary (assuming they didn't die)
            // Actually they might be alive.
            var p1 = sim.World.Players.Get(sim.World.Players.GetEntities()[1]);
            Assert.That(p1.Alive, Is.True);
        }

        [Test]
        public void Replay_SignalsFinished_WhenInputsRunOut()
        {
            // Scenario: Replay recorded 100 frames. 
            // We verify that after 100 frames, IsReplayFinished becomes true.
            int seed = 12345;
            int totalPlayers = 1;
            
            // 1. Create dummy replay file (or use Recorder directly)
            string tempReplay = "test_replay_end.json";
            var recorder = new InputRecorder();
            for(int i=0; i<10; i++) 
            {
                 recorder.RecordFrame(new InputState[] { new InputState() });
            }
            recorder.Save(tempReplay, seed, totalPlayers);

            // 2. Initialize System
            var rollback = new RollbackSystem(0, totalPlayers);
            rollback.InitializeFromReplay(tempReplay); 

            // 3. Step through frames
            for(int i=0; i<10; i++)
            {
                Assert.That(rollback.IsReplayFinished, Is.False, $"Should not be finished at frame {i}");
                rollback.Step(new InputState()); // Local input ignored in replay mode
            }

            // 4. Next Step should trigger finish
            rollback.Step(new InputState());
            
            Assert.That(rollback.IsReplayFinished, Is.True, "Should be finished after running out of frames");
            
            // Cleanup
            if(System.IO.File.Exists(tempReplay)) System.IO.File.Delete(tempReplay);
        }
    }
}
