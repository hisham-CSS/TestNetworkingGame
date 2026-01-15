using System;
using Bomberman.Core;
using Bomberman.Core.ECS.Components;
using Bomberman.Core.Game;
using Bomberman.Core.Input;

namespace Bomberman.Rollback
{
    /// <summary>
    /// Responsible for detecting discrepancies between local prediction and authoritative remote state.
    /// Checks for input mismatches (History Divergence) and state desynchronization.
    /// </summary>
    public class MispredictionDetector
    {
        private readonly IRollbackTelemetry _telemetry;

        public MispredictionDetector(IRollbackTelemetry telemetry)
        {
            _telemetry = telemetry;
        }

        /// <summary>
        /// Checks if the start frame of an incoming packet is older than the oldest snapshot we possess.
        /// If so, we cannot effectively rollback or verify history.
        /// </summary>
        public bool IsInputTooOld(int startFrame, SnapshotStore snapshots)
        {
            int oldestFrame = snapshots.GetOldestFrame();
            // Original logic: if (startFrame < oldestFrameWeHave)
            return startFrame < oldestFrame;
        }

        /// <summary>
        /// Checks if the provided inputs differ from what we have recorded/predicted.
        /// Updates the recorder with new inputs if they are valid.
        /// </summary>
        public void DetectInputMisprediction(int pid, int startFrame, InputState[] inputs, int currentFrame, InputRecorder recorder, ref int earliestMisprediction)
        {
            // Process all inputs in the packet (Oldest first)
            for (int i = inputs.Length - 1; i >= 0; i--)
            {
                int frame = startFrame - i;
                InputState input = inputs[i];

                if (frame < 0) continue;

                // CHECK FOR MISPREDICTION (INPUTS)
                if (frame < currentFrame)
                {
                    InputState[] usedInputs = recorder.GetFrame(frame);

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

        /// <summary>
        /// Compares the local state against a received remote state hash (StateSync/Checksum).
        /// If a desync is detected (Position threshold or Hash mismatch), triggers a rollback.
        /// </summary>
        public void DetectStateDesync(int pid, int frame, IntVector2 remotePos, int remoteHash, int currentFrame, SnapshotStore snapshots, ref int earliestMisprediction)
        {
             if (frame < currentFrame && snapshots.TryGet(frame, out GameStateSnapshot? snap))
             {
                 var players = snap.GetState<PlayerComponent>();
                 var transforms = snap.GetState<TransformComponent>();

                 // Find Player Entity
                 int pIndex = -1;
                 for(int i=0; i<players.components.Count; i++)
                 {
                     if (players.components[i].PlayerId == pid) 
                     {
                         pIndex = i; 
                         break; 
                     }
                 }

                 if (pIndex != -1)
                 {
                     Entity pEntity = players.entities[pIndex];
                     int tIndex = -1;
                     for(int k=0; k<transforms.entities.Count; k++)
                     {
                         if (transforms.entities[k].Index == pEntity.Index) { tIndex = k; break; }
                     }

                     if (tIndex != -1)
                     {
                         IntVector2 localPos = transforms.components[tIndex].Position;
                         
                         // Distance check
                         long distSq = (long)(localPos.X - remotePos.X) * (localPos.X - remotePos.X) + 
                                       (long)(localPos.Y - remotePos.Y) * (localPos.Y - remotePos.Y);
                         long threshold = 400 * 400; // 4 pixels

                         if (distSq > threshold) 
                         {
                             _telemetry.Log($"[Sync] Correction! Frame {frame} Player {pid}. Local:{localPos} Remote:{remotePos}");
                             
                             // We don't "fix" the snapshot here because it's past? 
                             // Logic says: Update the snapshot so if we *don't* rollback (e.g. later frame?), we are closer?
                             // But actually, seeing a desync usually triggers a rollback to this frame.
                             // Original logic set position:
                             var tf = transforms.components[tIndex];
                             tf.Position = remotePos;
                             transforms.components[tIndex] = tf; 
                             
                             if (earliestMisprediction == -1 || frame < earliestMisprediction)
                                 earliestMisprediction = frame;
                         }
                     }
                 }

                 int localHash = StateHasher.Hash(snap);
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
