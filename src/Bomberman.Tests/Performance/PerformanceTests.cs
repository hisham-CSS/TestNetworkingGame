using System;
using System.Diagnostics;
using NUnit.Framework;
using Chronos.Rollback;
using Bomberman.Core.Game;
using Bomberman.Core.Input;
using Bomberman.Core;

namespace Bomberman.Tests.Performance
{
    [TestFixture]
    public class PerformanceTests
    {
        [Test]
        [Category("Performance")]
        public void TestRollbackSystem_LongRunning_MemoryAndSpeed()
        {
            // Simulate a 10-minute match (600 FPS * 600 = 36,000 frames)
            int totalFrames = 36000; 
            int players = 4;
            var rollback = new RollbackSystem<InputState, GameStateSnapshot>(0, players, 1.0f/60.0f);
            var sim = new Simulation(12345, players);
            rollback.AttachSimulation(sim);
            rollback.InitializeSimulation(12345, players);
            rollback.SimulateNetworked = true; // Enable buffers

            var sw = new Stopwatch();
            long maxTicks = 0;
            long totalTicks = 0;

            // Measure initial memory
            long startMemory = GC.GetTotalMemory(true);

            for (int i = 0; i < totalFrames; i++)
            {
                // 1. Local Input
                var input = new InputState();
                
                sw.Restart();
                rollback.Step(input);
                sw.Stop();

                totalTicks += sw.ElapsedTicks;
                maxTicks = Math.Max(maxTicks, sw.ElapsedTicks);

                // 2. Simulate Remote Input (causing occasional rollback)
                // Every 60 frames, receive an input from 10 frames ago that mispredicted
                if (i > 20 && i % 60 == 0)
                {
                    int oldFrame = i - 10;
                    // Force miss
                    InputState[] oldInputs = new InputState[players];
                    oldInputs[1] = new InputState { PlaceBomb = true }; // Remote player did something different

                    rollback.HandleRemoteInput(1, oldFrame, oldInputs, 0, 0, 0);
                }
            }

            long endMemory = GC.GetTotalMemory(true);
            double avgMs = (double)totalTicks / totalFrames / TimeSpan.TicksPerMillisecond;
            
            Console.WriteLine($"Total Frames: {totalFrames}");
            Console.WriteLine($"Average Step Time: {avgMs:F4} ms");
            Console.WriteLine($"Max Step Time: {(double)maxTicks / TimeSpan.TicksPerMillisecond:F4} ms");
            Console.WriteLine($"Memory Delta: {(endMemory - startMemory) / 1024.0 / 1024.0:F2} MB");

            // Assertions
            // If buffers are not cleared, memory delta will be significant (e.g. > 10MB for 36k frames of dict overhead)
            // Ideally we want minimal growth.
            // But without the fix, we expect growth. 
            // We can assert that it IS leaking for now to prove it, or just print it.
            
            // Let's enforce a loose constraint to fail if it gets REALLY bad, but mostly this is for observation.
        }
    }
}
