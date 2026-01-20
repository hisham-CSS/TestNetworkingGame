using System;
using Chronos.Core;

namespace Chronos.Rollback
{
    /// <summary>
    /// Responsible for detecting discrepancies between local prediction and authoritative remote state.
    /// Checks for input mismatches (History Divergence) and state desynchronization.
    /// </summary>
    public class MispredictionDetector<TInput, TState> 
        where TInput : struct, IInputState<TInput>
        where TState : IGameState
    {
        private readonly IRollbackTelemetry _telemetry;

        public MispredictionDetector(IRollbackTelemetry telemetry)
        {
            _telemetry = telemetry;
        }

        public bool IsInputTooOld(int startFrame, SnapshotStore<TState> snapshots)
        {
            int oldestFrame = snapshots.GetOldestFrame();
            return startFrame < oldestFrame;
        }

        public void DetectInputMisprediction(int pid, int startFrame, TInput[] inputs, int currentFrame, InputRecorder<TInput> recorder, ref int earliestMisprediction)
        {
            // Process all inputs in the packet (Oldest first)
            for (int i = inputs.Length - 1; i >= 0; i--)
            {
                int frame = startFrame - i;
                TInput input = inputs[i];

                if (frame < 0) continue;

                // CHECK FOR MISPREDICTION (INPUTS)
                if (frame < currentFrame)
                {
                    TInput[] usedInputs = recorder.GetFrame(frame);

                    if (usedInputs != null && usedInputs.Length > pid && !input.Equals(usedInputs[pid]))
                    {
                        if (earliestMisprediction == -1 || frame < earliestMisprediction)
                        {
                            earliestMisprediction = frame;
                        }

                        // Correct the history with the authoritative input
                        usedInputs[pid] = input;
                        recorder.UpdateFrame(frame, usedInputs);
                    }
                }
            }
        }

        public void DetectStateDesync(int pid, int frame, int remoteHash, int currentFrame, SnapshotStore<TState> snapshots, ref int earliestMisprediction)
        {
             // We only check Hash for now to be agnostic.
             // If we want Positional checks, TState should implement ISyncCheckable or similar.
             // But Hash is the gold standard for Deterministic Rollback.
             
             if (frame < currentFrame && snapshots.TryGet(frame, out TState? snap))
             {
                 // We need to calculate Hash of local state passed in 'snap'.
                 // Ideally TState has GetHash(). But wait, TState IS the snapshot.
                 // In Bomberman, StateHasher was separate. 
                 // Here, let's assume TState.GetHashCode() is NOT reliable for cross-machine (default C# GetHashCode is memory based usually).
                 // We need IGameState to enforce a deterministic hash.
                 
                 // Ah, IGameState is empty interface. I should add int GetDeterministicHash()?.
                 // Or we rely on external comparison?
                 // For now, let's assume the Hash passed in IS compariable if we could generate it.
                 // But we don't have a generic "Hasher".
                 // Let's modify IGameState to have `int GetDeterministicHash()`.
                 
                 // If IGameState has GetDeterministicHash(), we can check it.
                 
                 if (snap is IDeterministicState deterministicState)
                 {
                     int localHash = deterministicState.CalculateHash();
                     if (localHash != remoteHash)
                     {
                         _telemetry.LogError($"[Sync] CRITICAL DESYNC! Frame {frame} Player {pid}. LocalHash:{localHash} RemoteHash:{remoteHash} -> ROLLBACK");
                         if (earliestMisprediction == -1 || frame < earliestMisprediction)
                             earliestMisprediction = frame;
                     }
                 }
             }
        }
    }
}
