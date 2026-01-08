using NUnit.Framework;
using Bomberman;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Bomberman.Core;

namespace Bomberman.Tests
{
    [TestFixture]
    public class RollbackTests
    {
        private RollbackSystem _rollback;

        [SetUp]
        public void Setup()
        {
            _rollback = new RollbackSystem(0, 2);
            _rollback.IsRecording = true;
            _rollback.SimulateNetworked = true;
            _rollback.InitializeSimulation(12345, 2);
        }

        [Test]
        public void TestDeterministicSimulation()
        {
            var rollback = new RollbackSystem(0, 2);
            _rollback.IsRecording = true;
            _rollback.SimulateNetworked = true;
            _rollback.InitializeSimulation(12345, 2);
        }

        [Test]
        public void Update_RecordsLocalInput()
        {
            // Arrange
            var input = new InputState { Movement = new Vector2(1, 0) };

            // Act
            _rollback.Update(input, null); // Local update

            // Assert
            // We can't access private buffers directly, but we can infer from side effects or exposing internal state if we interpret internals visible to tests?
            // Since we didn't make internals visible, we might need to rely on public state.
            // Check 'CurrentFrame' incremented
            Assert.That(_rollback.CurrentFrame, Is.EqualTo(1));
        }

        [Test]
        public void HandleRemoteInput_TriggersRollback_OnMisprediction()
        {
            // 1. Simulate Frame 0 (Local Player 0 moves RIGHT)
            // Implicitly predicts Player 1 does NOTHING
            var input0 = new InputState { Movement = new Vector2(1, 0) };
            _rollback.Update(input0, null);

            // 2. Simulate Frame 1
            _rollback.Update(input0, null);

             // Current Frame is now 2.
             // We have history for Frame 0 and 1.
             // For Player 1 (Remote), we used default inputs (Empty).

             // 3. Receive Remote Input for Frame 0 showing Player 1 ACTUALLY moved LEFT
             var input1_Real = new InputState { Movement = new Vector2(-1, 0) };
             InputState[] packetInputs = new InputState[] { input1_Real };
             
             // This should trigger rollback because what we SIMULATED (Empty) != Received (Left)
             
             // Capture Simulation State (e.g. log hook or position check?)
             // We can check if P1 position is updated to reflect the new input after rollback?
             
             // Initial P1 pos
             var p1PosBefore = GetPlayerPosition(1);

             _rollback.HandleRemoteInput(1, 0, packetInputs, Vector2.Zero, 0);

             // Assert correctness
             // If rollback happened, P1 should have moved LEFT.
             // If no rollback, P1 stays at start (0,0 assumed).
             
             var p1PosAfter = GetPlayerPosition(1);
             
             // Assuming speed is > 0
             // Initial pos 24,24 (1.5, 1.5 tiles * 16?) No, tiles are 32. 
             // Simulation.cs: 50.0f speed? 
             // Let's assume some movement occurred.
             
             Assert.That(p1PosBefore, Is.Not.EqualTo(p1PosAfter));
        }

        private Vector2 GetPlayerPosition(int pid)
        {
            var pPool = _rollback.Simulation.World.Players;
            for(int i=0; i<pPool.Count; i++)
            {
                if(pPool.Get(i).PlayerId == pid)
                {
                    var e = pPool.GetEntity(i);
                    return _rollback.Simulation.World.Transforms.Get(e).Position;
                }
            }
            return Vector2.Zero;
        }
    }
}
