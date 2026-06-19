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
            // TODO (LA3 - Rollback and resimulation): rewind to before the wrong frame, replay to now.
            //  1. Restore the snapshot from the frame BEFORE the misprediction:
            //       if (!snapshotStore.TryGet(mispredictedFrame - 1, out TState snapshot)) return;  // bail safely
            //       simulation.RestoreState(snapshot);
            //  2. Replay each frame from mispredictedFrame up to (but not including) currentFrame:
            //       - Build TInput[totalPlayers]: local input from localInputBuffer[frame];
            //         each remote input from remoteInputBuffer[frame][i] if known, else predict
            //         (lastConfirmedRemoteInputs[i] if present, else new TInput()).
            //       - simulation.Update(inputs, _fixedTimeStep);
            //       - snapshotStore.Save(frame, simulation.CaptureState());
            //       - if (isRecording) recorder.UpdateFrame(frame, inputs);
            throw new System.NotImplementedException("LA3: implement PerformRollback");
        }
    }
}
