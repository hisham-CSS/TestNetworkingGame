using System.Collections.Generic;
using Chronos.Core;

namespace Chronos.Rollback
{
    /// <summary>
    /// Orchestrates the process of restoring a game state from a snapshot and re-simulating frames
    /// up to the current time, applying corrected inputs.
    /// </summary>
    public class ResimulationRunner<TInput, TState> 
        where TInput : struct, IInputState<TInput>
        where TState : IGameState
    {
        private readonly IRollbackTelemetry _telemetry;
        private readonly float _fixedTimeStep;

        public ResimulationRunner(float fixedTimeStep, IRollbackTelemetry telemetry)
        {
            _fixedTimeStep = fixedTimeStep;
            _telemetry = telemetry;
        }

        public void PerformRollback(
            int currentFrame, 
            int mispredictedFrame, 
            IGameSimulation<TInput, TState> simulation, 
            SnapshotStore<TState> snapshotStore,
            Dictionary<int, TInput> localInputBuffer,
            Dictionary<int, Dictionary<int, TInput>> remoteInputBuffer,
            Dictionary<int, TInput> lastConfirmedRemoteInputs,
            InputRecorder<TInput> recorder,
            int localPlayerId,
            int totalPlayers,
            bool isRecording)
        {
            _telemetry.LogWarning($"[Chronos] ROLLBACK from frame {currentFrame} to {mispredictedFrame}");
            _telemetry.RecordRollback(currentFrame, mispredictedFrame);

            if (!snapshotStore.TryGet(mispredictedFrame - 1, out var snapshot))
            {
                _telemetry.LogError($"!!! CRITICAL: Cannot rollback, no snapshot for frame {mispredictedFrame - 1}");
                return;
            }
            
            simulation.RestoreState(snapshot);

            for (int frame = mispredictedFrame; frame < currentFrame; frame++)
            {
                TInput[] inputs = new TInput[totalPlayers];
                
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
                            inputs[i] = new TInput(); // Default
                        }
                    }
                }

                simulation.Update(inputs, _fixedTimeStep);

                snapshotStore.Save(frame, simulation.CaptureState());

                if (isRecording) recorder.UpdateFrame(frame, inputs);
            }
        }
    }
}
