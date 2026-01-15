using System;
using System.Collections.Generic;
using Bomberman.Core;
using Bomberman.Core.Game;
using Bomberman.Core.Input;

namespace Bomberman.Rollback
{
    /// <summary>
    /// Orchestrates the process of restoring a game state from a snapshot and re-simulating frames
    /// up to the current time, applying corrected inputs.
    /// </summary>
    public class ResimulationRunner
    {
        private readonly IRollbackTelemetry _telemetry;
        private readonly GameConfig _gameConfig;

        public ResimulationRunner(GameConfig gameConfig, IRollbackTelemetry telemetry)
        {
            _gameConfig = gameConfig;
            _telemetry = telemetry;
        }

        /// <summary>
        /// Restores the game world to a valid snapshot before the misprediction, then fast-forwards
        /// the simulation to the current frame using the corrected input buffers.
        /// </summary>
        /// <param name="currentFrame">The game loop's current target frame.</param>
        /// <param name="mispredictedFrame">The earliest frame where a disagreement occurred.</param>
        /// <param name="simulation">The game simulation engine.</param>
        /// <param name="snapshotStore">Source of valid snapshots.</param>
        public void PerformRollback(
            int currentFrame, 
            int mispredictedFrame, 
            Simulation simulation, 
            SnapshotStore snapshotStore,
            Dictionary<int, InputState> localInputBuffer,
            Dictionary<int, Dictionary<int, InputState>> remoteInputBuffer,
            Dictionary<int, InputState> lastConfirmedRemoteInputs,
            InputRecorder recorder,
            int localPlayerId,
            int totalPlayers,
            bool isRecording)
        {
            _telemetry.LogWarning($"ROLLBACK from frame {currentFrame} to {mispredictedFrame}");
            _telemetry.RecordRollback(currentFrame, mispredictedFrame);

            if (!snapshotStore.TryGet(mispredictedFrame - 1, out GameStateSnapshot? snapshot))
            {
                _telemetry.LogError($"!!! CRITICAL: Cannot rollback, no snapshot for frame {mispredictedFrame - 1}");
                return;
            }
            
            if (simulation == null) return;

            snapshot.Restore(simulation.World);

            for (int frame = mispredictedFrame; frame < currentFrame; frame++)
            {
                InputState[] inputs = new InputState[totalPlayers];
                
                if (localInputBuffer.ContainsKey(frame)) inputs[localPlayerId] = localInputBuffer[frame];

                for (int i = 0; i < totalPlayers; i++)
                {
                    if (i == localPlayerId) continue;
                    if (remoteInputBuffer.ContainsKey(frame) && remoteInputBuffer[frame].ContainsKey(i))
                    {
                        inputs[i] = remoteInputBuffer[frame][i]; 
                    }
                    else
                    {
                        // Prediction Logic
                        if (lastConfirmedRemoteInputs.TryGetValue(i, out var lastInput))
                        {
                            inputs[i] = lastInput;
                        }
                        else
                        {
                            inputs[i] = new InputState();
                        }
                    }
                }

                simulation.Update(inputs, (float)_gameConfig.FixedTimeStep);

                snapshotStore.Save(frame, simulation.World);

                if (isRecording) recorder.UpdateFrame(frame, inputs);
            }
        }
    }
}
