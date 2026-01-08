using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.IO;



using Bomberman.Core;

namespace Bomberman.Core.Rollback
{
    public class RollbackSystem
    {
        public Simulation? Simulation { get; private set; }
        public int CurrentFrame => _currentFrame;
        public bool IsRecording { get; set; }
        public bool IsReplaying { get; set; }
        public bool SimulateNetworked { get; set; }

        private InputRecorder _recorder;
        private Dictionary<int, GameStateSnapshot> _snapshotBuffer = new Dictionary<int, GameStateSnapshot>();
        private Dictionary<int, Dictionary<int, InputState>> _remoteInputBuffer = new Dictionary<int, Dictionary<int, InputState>>(); // Frame -> PlayerId -> Input
        private Dictionary<int, InputState> _localInputBuffer = new Dictionary<int, InputState>(); // Frame -> LocalInput

        // Prediction State
        private Dictionary<int, InputState> _lastConfirmedRemoteInputs = new Dictionary<int, InputState>();
        private Dictionary<int, int> _lastConfirmedRemoteFrame = new Dictionary<int, int>();
        
        private const int MaxSnapshotFrames = 60 * 5; // 5 Seconds
        private const int MaxPredictionFrames = 15; // 250ms
        private const double FixedTimeStep = 1.0 / 60.0;
        
        private int _totalPlayers;
        private int _localPlayerId;
        private int _replayFrame = 0;

        public RollbackSystem(int localPlayerId, int totalPlayers)
        {
            _localPlayerId = localPlayerId;
            _totalPlayers = totalPlayers;
            _recorder = new InputRecorder();
        }

        public void InitializeSimulation(int seed, int totalPlayers)
        {
            Simulation = new Simulation(seed, totalPlayers);
            // Save initial state (Frame -1) so we can rollback TO Frame 0
            _snapshotBuffer[-1] = new GameStateSnapshot(-1, Simulation.World);
            // Logger hookup can be done externally or passed in
        }

        public void LoadReplay(string path)
        {
            IsReplaying = true;
            _recorder.Load(path);
            _replayFrame = 0;
            Console.WriteLine("Replay Loaded.");
        }

        public void SaveReplay(string path)
        {
             _recorder.Save(path);
        }
        
        public void Reset()
        {
            _currentFrame = 0;
            _snapshotBuffer.Clear();
            _remoteInputBuffer.Clear();
            _localInputBuffer.Clear();
            _lastConfirmedRemoteInputs.Clear();
            _lastConfirmedRemoteFrame.Clear();
            _recorder.Reset();
        }

        private int _currentFrame = 0;

        public void AddRemoteInput(int pid, int frame, InputState input)
        {
            if (!_remoteInputBuffer.ContainsKey(frame))
            {
                _remoteInputBuffer[frame] = new Dictionary<int, InputState>();
            }
            _remoteInputBuffer[frame][pid] = input;
        }

        public bool TryBuildOutgoingBundle(out OutgoingInputBundle bundle)
        {
            bundle = default;
            // We want to send the input for the frame we JUST simulated (or the latest available).
            // Since Step() increments _currentFrame, the latest complete frame is _currentFrame - 1.
            int frameToSend = _currentFrame - 1;
            
            if (frameToSend < 0) return false;

            if (_localInputBuffer.ContainsKey(frameToSend))
            {
                 int redundancy = 8;
                 var history = new List<InputState>();
                 history.Add(_localInputBuffer[frameToSend]); // Frame To Send Input

                 for (int i = 1; i < redundancy; i++)
                 {
                     int histFrame = frameToSend - i;
                     if (histFrame >= 0 && _localInputBuffer.ContainsKey(histFrame))
                     {
                         history.Add(_localInputBuffer[histFrame]);
                     }
                     else break;
                 }

                 // Get Local Pos
                 IntVector2 currentPos = new IntVector2(0, 0); // Use specific default
                 if (Simulation != null)
                 {
                     var playerPool = Simulation.World.Players;
                     for (int i = 0; i < playerPool.Count; i++)
                     {
                         if (playerPool.Get(i).PlayerId == _localPlayerId)
                         {
                             var entity = playerPool.GetEntity(i);
                             if (Simulation.World.Transforms.Has(entity))
                             {
                                 currentPos = Simulation.World.Transforms.Get(entity).Position;
                             }
                             break;
                         }
                     }
                 }

                 int localHash = Simulation != null ? StateHasher.Hash(Simulation.World) : 0;
                 
                 bundle = new OutgoingInputBundle
                 {
                     PlayerId = _localPlayerId,
                     Frame = frameToSend,
                     RedundantHistory = history.ToArray(),
                     LocalPosition = currentPos,
                     LocalStateHash = localHash
                 };
                 return true;
            }
            return false;
        }

        // Replaced Update -> Step
        public void Step(InputState localInput)
        {
            // ... (Replay logic - handle separately or keep basic check) ...
            if (IsReplaying)
            {
                 InputState[] replayInputs = _recorder.GetFrame(_replayFrame);
                 if (replayInputs == null || replayInputs.Length == 0) replayInputs = new InputState[1];
                 if (Simulation != null) Simulation.Update(replayInputs, (float)FixedTimeStep);
                 _replayFrame++;
                 return;
            }

            // Store Local Input
            _localInputBuffer[_currentFrame] = localInput;

            bool isNetworked = SimulateNetworked; // Or controlled externally

            if (isNetworked)
            {
                // FRAME PACING / THROTTLING logic?
                // For now, removing direct dependency. The NetworkController should check throttling before calling Step?
                // Or we keep throttling logic here but rely on external "LastConfirmedFrame" inputs.
                
                int minConfirmedFrame = _currentFrame;
                for (int i = 0; i < _totalPlayers; i++)
                {
                    if (i == _localPlayerId) continue;
                    if (_lastConfirmedRemoteFrame.TryGetValue(i, out int lastFrame))
                    {
                        if (lastFrame < minConfirmedFrame) minConfirmedFrame = lastFrame;
                    }
                    else
                    {
                        minConfirmedFrame = 0; 
                    }
                }

                if (_currentFrame > minConfirmedFrame + MaxPredictionFrames)
                {
                    // Throttling
                    return; 
                }

                // Input Sending logic MOVED to TryBuildOutgoingBundle which caller triggers.
                
                // 2. Construct Input Array for THIS frame using PREDICTION
                InputState[] inputs = new InputState[_totalPlayers];
                inputs[_localPlayerId] = localInput;

                for (int i = 0; i < _totalPlayers; i++)
                {
                    if (i == _localPlayerId) continue;

                    if (_remoteInputBuffer.ContainsKey(_currentFrame) && _remoteInputBuffer[_currentFrame].ContainsKey(i))
                    {
                        inputs[i] = _remoteInputBuffer[_currentFrame][i];
                    }
                    else
                    {
                        inputs[i] = PredictInputForPlayer(i);
                    }
                }
                
                // 3. Record what we used
                if (IsRecording) _recorder.RecordFrame(inputs);

                if (Simulation != null) 
                {
                    Simulation.Update(inputs, (float)FixedTimeStep);

                    // 5. Save Snapshot
                    _snapshotBuffer[_currentFrame] = new GameStateSnapshot(_currentFrame, Simulation.World);
                    if (_snapshotBuffer.ContainsKey(_currentFrame - MaxSnapshotFrames)) 
                    {
                        _snapshotBuffer.Remove(_currentFrame - MaxSnapshotFrames);
                    }
                }
            }
            else
            {
                 // Local Single Player
                InputState[] inputs = new InputState[] { localInput };
                if (IsRecording) _recorder.RecordFrame(inputs);
                if (Simulation != null) Simulation.Update(inputs, (float)FixedTimeStep);
            }
            
            _currentFrame++; // Increment AFTER Step
        }

         private InputState PredictInputForPlayer(int playerId)
        {
            if (_lastConfirmedRemoteInputs.TryGetValue(playerId, out var lastInput))
            {
                return lastInput;
            }
            return new InputState(); 
        }

        public void HandleRemoteInput(int pid, int startFrame, InputState[] inputs, IntVector2 remotePos, int remoteHash)
        {
             int earliestMisprediction = -1;

            // Process all inputs in the packet (Oldest first)
            for (int i = inputs.Length - 1; i >= 0; i--)
            {
                int frame = startFrame - i;
                InputState input = inputs[i];

                if (frame < 0) continue;

                AddRemoteInput(pid, frame, input);

                // CHECK FOR MISPREDICTION (INPUTS)
                if (frame < _currentFrame)
                {
                        InputState[] usedInputs = _recorder.GetFrame(frame);
                        
                        if (usedInputs != null && usedInputs.Length > pid && !input.Equals(usedInputs[pid]))
                        {
                            if (earliestMisprediction == -1 || frame < earliestMisprediction)
                            {
                                earliestMisprediction = frame;
                            }
                            
                            usedInputs[pid] = input; 
                            _recorder.UpdateFrame(frame, usedInputs);
                        }
                }
            }

            // CHECK FOR DESYNC (STATE HASH & POSITION)
            if (startFrame < _currentFrame && _snapshotBuffer.ContainsKey(startFrame))
            {
                    // Correction Logic: Check if Player Position matches remote (rough check)
                    // We need to look up the player in the snapshot by ID (pid)
                    var snap = _snapshotBuffer[startFrame];
                    var players = snap.GetState<PlayerComponent>();
                    var transforms = snap.GetState<TransformComponent>();
                    
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
                            // Distance check using squared distance
                            // 4 pixels = 400 subpixel units
                            int curX = localPos.X;
                            int curY = localPos.Y;
                            int remX = remotePos.X;
                            int remY = remotePos.Y;
                            long distSq = (long)(curX - remX) * (curX - remX) + (long)(curY - remY) * (curY - remY);
                            long threshold = 400 * 400;

                            if (distSq > threshold) 
                            {
                                Console.WriteLine($"[Sync] Correction! Frame {startFrame} Player {pid}. Local:{localPos} Remote:{remotePos}");
                                var tf = transforms.components[tIndex];
                                tf.Position = remotePos;
                                transforms.components[tIndex] = tf; 
                                
                                if (earliestMisprediction == -1 || startFrame < earliestMisprediction)
                                    earliestMisprediction = startFrame;
                            }
                        }
                    }

                    int localHash = StateHasher.Hash(snap);
                    if (localHash != remoteHash)
                    {
                        Console.WriteLine($"[Sync] CRITICAL DESYNC! Frame {startFrame} Player {pid}. LocalHash:{localHash} RemoteHash:{remoteHash} -> ROLLBACK");
                        if (earliestMisprediction == -1 || startFrame < earliestMisprediction)
                            earliestMisprediction = startFrame;
                    }
            }

            // Update latest known input for prediction
            if (inputs.Length > 0)
            {
                _lastConfirmedRemoteInputs[pid] = inputs[0];
                _lastConfirmedRemoteFrame[pid] = startFrame;
            }

            // Trigger Rollback if needed
            if (earliestMisprediction != -1)
            {
                PerformRollback(earliestMisprediction);
            }
        }

        private void PerformRollback(int mispredictedFrame)
        {
            Console.WriteLine($"ROLLBACK from frame {_currentFrame} to {mispredictedFrame}");

            if (!_snapshotBuffer.TryGetValue(mispredictedFrame - 1, out GameStateSnapshot? snapshot))
            {
                Console.WriteLine($"!!! CRITICAL: Cannot rollback, no snapshot for frame {mispredictedFrame - 1}");
                return;
            }
            
            if (Simulation == null) return;

            snapshot.Restore(Simulation.World);

            for (int frame = mispredictedFrame; frame < _currentFrame; frame++)
            {
                InputState[] inputs = new InputState[_totalPlayers];
                
                if (_localInputBuffer.ContainsKey(frame)) inputs[_localPlayerId] = _localInputBuffer[frame];

                for (int i = 0; i < _totalPlayers; i++)
                {
                    if (i == _localPlayerId) continue;
                    if (_remoteInputBuffer.ContainsKey(frame) && _remoteInputBuffer[frame].ContainsKey(i))
                    {
                        inputs[i] = _remoteInputBuffer[frame][i]; 
                    }
                    else
                    {
                        inputs[i] = PredictInputForPlayer(i); 
                    }
                }

                Simulation.Update(inputs, (float)FixedTimeStep);

                _snapshotBuffer[frame] = new GameStateSnapshot(frame, Simulation.World);

                if (IsRecording) _recorder.UpdateFrame(frame, inputs);
            }
        }
    }
}
