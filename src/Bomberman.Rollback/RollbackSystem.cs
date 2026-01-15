using System;
using Bomberman.Core.Input;
using Bomberman.Core.ECS.Components;
using Bomberman.Core.Game;
using System.Collections.Generic;
using System.IO;
using Bomberman.Core;

namespace Bomberman.Rollback
{
    /// <summary>
    /// The core Rollback Networking system.
    /// Manages state snapshots, input prediction, and time synchronization.
    /// Executes the simulation step-by-step, re-running frames when mispredictions occur.
    /// </summary>
    public class RollbackSystem
    {
        public Simulation? Simulation { get; private set; }
        public int CurrentFrame => _currentFrame;
        
        /// <summary>If true, inputs are recorded to the standard recorder.</summary>
        public bool IsRecording { get; set; }
        
        /// <summary>If true, we are replaying a past session.</summary>
        public bool IsReplaying { get; set; }
        
        public bool IsReplayFinished { get; private set; }
        
        /// <summary>Enables prediction logic using available inputs.</summary>
        public bool SimulateNetworked { get; set; }

        private InputRecorder _recorder;
        private SnapshotStore _snapshotStore;
        private MispredictionDetector _detector;
        private ResimulationRunner _runner;
        private Dictionary<int, Dictionary<int, InputState>> _remoteInputBuffer = new Dictionary<int, Dictionary<int, InputState>>(); // Frame -> PlayerId -> Input
        private Dictionary<int, InputState> _localInputBuffer = new Dictionary<int, InputState>(); // Frame -> LocalInput

        // Prediction State
        private Dictionary<int, InputState> _lastConfirmedRemoteInputs = new Dictionary<int, InputState>();
        private Dictionary<int, int> _lastConfirmedRemoteFrame = new Dictionary<int, int>();
        
        private readonly RollbackConfig _config;
        private readonly GameConfig _gameConfig;
        private readonly IRollbackTelemetry _telemetry;
        private readonly IReplayStorage? _storage;
        
        private int MaxSnapshotFrames => _config.MaxSnapshotFrames;
        private int MaxPredictionFrames => _config.MaxPredictionFrames;
        private double FixedTimeStep => _gameConfig.FixedTimeStep;
        
        private int _totalPlayers;
        private int _localPlayerId;
        private int _replayFrame = 0;
        private int _seed;

        /// <summary>
        /// Initializes the rollback system.
        /// </summary>
        /// <param name="localPlayerId">The ID of the local player (0 for host).</param>
        /// <param name="totalPlayers">Total players in session.</param>
        /// <param name="config">Rollback configuration.</param>
        /// <param name="gameConfig">Game configuration (for simulation).</param>
        /// <param name="telemetry">Telemetry logger.</param>
        /// <param name="storage">Replay storage provider.</param>
        public RollbackSystem(int localPlayerId, int totalPlayers, RollbackConfig? config = null, GameConfig? gameConfig = null, IRollbackTelemetry? telemetry = null, IReplayStorage? storage = null)
        {
            _localPlayerId = localPlayerId;
            _totalPlayers = totalPlayers;
            _config = config ?? RollbackConfig.Default;
            _gameConfig = gameConfig ?? GameConfig.Default;
            _telemetry = telemetry ?? new NoOpTelemetry();
            _storage = storage;
            _recorder = new InputRecorder(_storage);
            _snapshotStore = new SnapshotStore(MaxSnapshotFrames);
            _detector = new MispredictionDetector(_telemetry);
            _runner = new ResimulationRunner(_gameConfig, _telemetry);
        }

        public int GetLastConfirmedFrame(int playerId)
        {
            if (_lastConfirmedRemoteFrame.TryGetValue(playerId, out int frame))
            {
                return frame;
            }
            return 0; // Default
        }

        /// <summary>
        /// Calculates how many simulation steps to execute this frame to stay in sync.
        /// Uses simple catch-up/slow-down logic based on frame lag.
        /// </summary>
        public int CalculateTargetSteps(int localFrame, int hostFrame)
        {
             int lag = hostFrame - localFrame; // Positive = We are Behind. Negative = We are Ahead.
                
             // 1. Catch-Up: We are behind confirmed inputs (Speed up)
             if (lag > 2) 
             {
                 return 1 + Math.Min(lag, 8); // Cap at 8x speed
             }
             // 2. Slow-Down: We are too far ahead (Stall)
             else if (lag < -5)
             {
                 return 0;
             }
             
             // Normal speed
             return 1;
        }

        /// <summary>
        /// Initializes the simulation with a specific seed and player count.
        /// Takes an initial snapshot at frame -1.
        /// </summary>
        public void InitializeSimulation(int seed, int totalPlayers)
        {
            _seed = seed;
            _totalPlayers = totalPlayers; // Update in case it changed (e.g. from replay)
            Simulation = new Simulation(seed, totalPlayers, _gameConfig);
            // Save initial state (Frame -1) so we can rollback TO Frame 0
            _snapshotStore.Save(-1, Simulation.World);
            // Logger hookup can be done externally or passed in
        }

        /// <summary>
        /// Loads a replay file and prepares the system for playback.
        /// </summary>
        public void InitializeFromReplay(string path)
        {
            _recorder.Load(path);
            if (_recorder.FrameCount > 0)
            {
                _telemetry.Log($"Initializing Replay: Seed={_recorder.Seed}, Players={_recorder.TotalPlayers}");
                InitializeSimulation(_recorder.Seed, _recorder.TotalPlayers);
                IsReplaying = true;
                IsRecording = false; // Don't record while replaying
                _replayFrame = 0;
            }
            else
            {
                _telemetry.LogError("Replay failed to load or empty.");
            }
        }

        public void SaveReplay(string path)
        {
             _recorder.Save(path, _seed, _totalPlayers);
        }
        
        public void Reset()
        {
            _currentFrame = 0;
            _snapshotStore.Clear();
            _remoteInputBuffer.Clear();
            _localInputBuffer.Clear();
            _lastConfirmedRemoteInputs.Clear();
            _lastConfirmedRemoteFrame.Clear();
            _recorder.Reset();
        }

        public void SyncToFrame(int frame)
        {
            _currentFrame = frame;
            _currentFrame = frame;
            _snapshotStore.Clear();
             _snapshotStore.Save(frame, Simulation.World);
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

        /// <summary>
        /// Attempts to bundle the input for the most recently completed frame (CurrentFrame - 1).
        /// Includes redundant history for packet loss recovery.
        /// </summary>
        /// <param name="bundle">The output bundle if successful.</param>
        /// <returns>True if a bundle was created, false if no input is available.</returns>
        public bool TryBuildOutgoingBundle(out OutgoingInputBundle bundle)
        {
            bundle = default;
            // We want to send the input for the frame we JUST simulated (or the latest available).
            // Since Step() increments _currentFrame, the latest complete frame is _currentFrame - 1.
            int frameToSend = _currentFrame - 1;
            
            if (frameToSend < 0) return false;

            if (_localInputBuffer.ContainsKey(frameToSend))
            {
                 int redundancy = _config.RedundancyFactor;
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

        private HashSet<int> _disconnectedPlayers = new HashSet<int>();

        public void SetPlayerDisconnected(int pid)
        {
            if (pid >= 0 && pid < _totalPlayers && !_disconnectedPlayers.Contains(pid))
            {
                _disconnectedPlayers.Add(pid);
                _telemetry.LogWarning($"[Rollback] Player {pid} Disconnected. Switching to Auto-Input.");
            }
        }

        /// <summary>
        /// Advances the simulation by one frame.
        /// Handles local prediction, input recording, and snapshotting.
        /// </summary>
        /// <param name="localInput">Input from the local player for this frame.</param>
        public void Step(InputState localInput)
        {
            // ... (Replay logic - handle separately or keep basic check) ...
            if (IsReplaying)
            {
                 if (IsReplayFinished) return;

                 InputState[] replayInputs = _recorder.GetFrame(_replayFrame);
                 if (replayInputs == null || replayInputs.Length == 0)
                 {
                     IsReplayFinished = true;
                     _telemetry.Log("[Rollback] Replay Finished (End of Input Stream).");
                     return;
                 }
                 
                 if (Simulation != null) Simulation.Update(replayInputs, (float)FixedTimeStep);
                 _replayFrame++;
                 return;
            }

            // Synthesize inputs for disconnected players to prevent stall
            foreach (int pid in _disconnectedPlayers)
            {
                int lastFrame = -1;
                if (_lastConfirmedRemoteFrame.ContainsKey(pid)) lastFrame = _lastConfirmedRemoteFrame[pid];
                
                // Keep disconnected players 1 frame ahead to avoid throttling
                int targetFrame = _currentFrame + 1;
                
                for (int f = lastFrame + 1; f <= targetFrame; f++)
                {
                    AddRemoteInput(pid, f, new InputState()); // Neutral Input
                    _lastConfirmedRemoteFrame[pid] = f;
                }
            }

            // Store Local Input
            _localInputBuffer[_currentFrame] = localInput;

            bool isNetworked = SimulateNetworked; // Or controlled externally

            if (isNetworked)
            {
                // Throttling logic based on confirmed frames
                
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

                    // Save Snapshot
                    _snapshotStore.Save(_currentFrame, Simulation.World);
                    
                    // Cleanup Old History
                    int oldestFrameToKeep = _currentFrame - MaxSnapshotFrames;
                    
                    // Trim Input Buffers
                    // We can safely remove inputs older than the oldest snapshot we can rollback to.
                    if (_localInputBuffer.ContainsKey(oldestFrameToKeep - 1))
                    {
                        _localInputBuffer.Remove(oldestFrameToKeep - 1);
                    }
                    if (_remoteInputBuffer.ContainsKey(oldestFrameToKeep - 1))
                    {
                        _remoteInputBuffer.Remove(oldestFrameToKeep - 1);
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

        public enum InputResult
        {
            /// <summary>Input was accepted successfully.</summary>
            Success,
            /// <summary>Input caused a misprediction, rollback triggered.</summary>
            Misprediction,
            /// <summary>Input was too old to be applied (before earliest snapshot).</summary>
            TooOld
        }

        /// <summary>
        /// Processes a packet of inputs from a remote player.
        /// Checks for mispredictions or desyncs and triggers rollback if necessary.
        /// </summary>
        /// <param name="pid">ID of the remote player.</param>
        /// <param name="startFrame">The frame of the NEWEST input in the packet.</param>
        /// <param name="inputs">History of inputs ending at startFrame (redundant history).</param>
        /// <param name="remotePos">Position of remote player for desync check.</param>
        /// <param name="remoteHash">State hash of remote player for desync check.</param>
        public InputResult HandleRemoteInput(int pid, int startFrame, InputState[] inputs, IntVector2 remotePos, int remoteHash)
        {
             int earliestMisprediction = -1;
             
             // Check if inputs are too old
             if (inputs.Length > 0 && _detector.IsInputTooOld(startFrame - (inputs.Length - 1), _snapshotStore))
             {
                 return InputResult.TooOld;
             }
             
             // Process all inputs in the packet (Oldest first) for Buffering
             for (int i = inputs.Length - 1; i >= 0; i--)
             {
                 int frame = startFrame - i;
                 if (frame < 0) continue;
                 AddRemoteInput(pid, frame, inputs[i]);
             }

             // Detect Mispredictions (Inputs)
             _detector.DetectInputMisprediction(pid, startFrame, inputs, _currentFrame, _recorder, ref earliestMisprediction);

             // Detect Desync (State)
             _detector.DetectStateDesync(pid, startFrame, remotePos, remoteHash, _currentFrame, _snapshotStore, ref earliestMisprediction);

             // Update latest known input for prediction
             if (inputs.Length > 0)
             {
                 _lastConfirmedRemoteInputs[pid] = inputs[0];
                 _lastConfirmedRemoteFrame[pid] = startFrame;
             }

             // Trigger Rollback if needed
             if (earliestMisprediction != -1)
             {
                 // Verify we actually CAN rollback to this frame
                 if (!_snapshotStore.Has(earliestMisprediction - 1))
                 {
                     _telemetry.LogWarning($"[Rollback] Request to rollback to {earliestMisprediction} but too old. Returning TooOld.");
                     return InputResult.TooOld;
                 }
                 
                 PerformRollback(earliestMisprediction);
                 return InputResult.Misprediction;
             }
             
             return InputResult.Success;
        }

        /// <summary>
        /// Restores the game state to a previous snapshot and re-simulates up to the current frame.
        /// Uses confirmed inputs where available, and predicts others.
        /// </summary>
        /// <param name="mispredictedFrame">The frame number where the divergence occurred.</param>
        private void PerformRollback(int mispredictedFrame)
        {
            if (Simulation == null) return;

            _runner.PerformRollback(
                _currentFrame, 
                mispredictedFrame, 
                Simulation, 
                _snapshotStore, 
                _localInputBuffer, 
                _remoteInputBuffer, 
                _lastConfirmedRemoteInputs, 
                _recorder, 
                _localPlayerId, 
                _totalPlayers, 
                IsRecording);
        }
    }
}
