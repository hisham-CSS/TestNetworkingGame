using System;
using System.Collections.Generic;
using Chronos.Core;

namespace Chronos.Rollback
{
    public enum InputResult { Success, Misprediction, TooOld }

    /// <summary>
    /// The core Rollback Networking system.
    /// Manages state snapshots, input prediction, and time synchronization.
    /// </summary>
    public class RollbackSystem<TInput, TState> 
        where TInput : struct, IInputState<TInput>
        where TState : IGameState
    {
        public IGameSimulation<TInput, TState>? Simulation { get; private set; }
        public int CurrentFrame => _currentFrame;
        
        public bool IsRecording { get; set; }
        public bool IsReplaying { get; set; }
        public bool IsReplayFinished { get; private set; }
        public bool SimulateNetworked { get; set; }

        private InputRecorder<TInput> _recorder;
        private SnapshotStore<TState> _snapshotStore;
        private MispredictionDetector<TInput, TState> _detector;
        private ResimulationRunner<TInput, TState> _runner;
        
        private Dictionary<int, Dictionary<int, TInput>> _remoteInputBuffer = new Dictionary<int, Dictionary<int, TInput>>(); // Frame -> Plr -> Input
        private Dictionary<int, TInput> _localInputBuffer = new Dictionary<int, TInput>(); 
        private Dictionary<int, TInput> _lastConfirmedRemoteInputs = new Dictionary<int, TInput>();
        private Dictionary<int, int> _lastConfirmedRemoteFrame = new Dictionary<int, int>();
        
        private readonly RollbackConfig _config;
        private readonly IRollbackTelemetry _telemetry;
        private readonly IReplayStorage? _storage;
        private readonly float _fixedTimeStep;
        
        private int MaxSnapshotFrames => _config.MaxSnapshotFrames;
        private int MaxPredictionFrames => _config.MaxPredictionFrames;
        
        private int _totalPlayers;
        private int _localPlayerId;
        private int _replayFrame = 0;
        private int _seed;

        public RollbackSystem(int localPlayerId, int totalPlayers, float fixedTimeStep, RollbackConfig? config = null, IRollbackTelemetry? telemetry = null, IReplayStorage? storage = null)
        {
            _localPlayerId = localPlayerId;
            _totalPlayers = totalPlayers;
            _fixedTimeStep = fixedTimeStep;
            _config = config ?? RollbackConfig.Default;
            _telemetry = telemetry ?? new NoOpTelemetry();
            _storage = storage;
            
            _recorder = new InputRecorder<TInput>(_storage);
            _snapshotStore = new SnapshotStore<TState>(MaxSnapshotFrames);
            _detector = new MispredictionDetector<TInput, TState>(_telemetry);
            _runner = new ResimulationRunner<TInput, TState>(_fixedTimeStep, _telemetry);
        }

        public void AttachSimulation(IGameSimulation<TInput, TState> simulation)
        {
            Simulation = simulation;
        }

        public void InitializeSimulation(int seed, int totalPlayers)
        {
            _seed = seed;
            _totalPlayers = totalPlayers;
            
            if (Simulation == null) throw new InvalidOperationException("Simulation not attached!");

            // Save initial state (Frame -1)
            _snapshotStore.Save(-1, Simulation.CaptureState());
        }

        public void InitializeFromReplay(string path)
        {
            _recorder.Load(path);
            if (_recorder.FrameCount > 0)
            {
                _telemetry.Log($"Initializing Replay: Seed={_recorder.Seed}, Players={_recorder.TotalPlayers}");
                InitializeSimulation(_recorder.Seed, _recorder.TotalPlayers);
                IsReplaying = true;
                IsRecording = false; 
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

        private int _currentFrame = 0;

        public void AddRemoteInput(int pid, int frame, TInput input)
        {
            if (!_remoteInputBuffer.ContainsKey(frame))
            {
                _remoteInputBuffer[frame] = new Dictionary<int, TInput>();
            }
            _remoteInputBuffer[frame][pid] = input;
        }

        public int CalculateTargetSteps(int localFrame, int hostFrame)
        {
            // TODO (LA3 - Time sync): return how many simulation steps to take this tick.
            //  - lag = hostFrame - localFrame  (how far the host is ahead of us).
            //  - If lag > 2 (we are behind): take extra steps to catch up: 1 + Min(lag, 8).
            //  - If lag < -5 (we are too far ahead): stall this tick: return 0.
            //  - Otherwise: take one normal step: return 1.
            throw new System.NotImplementedException("LA3: implement CalculateTargetSteps");
        }

        public void SyncToFrame(int frame)
        {
            _currentFrame = frame;
            _snapshotStore.Clear(); // Clear history as we just jumped
        }

        public int GetLastConfirmedFrame(int pid)
        {
            if (_lastConfirmedRemoteFrame.TryGetValue(pid, out int frame))
            {
                return frame;
            }
            return 0; // Default if not yet received
        }

        public bool TryBuildOutgoingBundle(out OutgoingInputBundle<TInput> bundle)
        {
            bundle = default;
            int frameToSend = _currentFrame - 1;
            
            if (frameToSend < 0) return false;

            if (_localInputBuffer.ContainsKey(frameToSend))
            {
                 int redundancy = _config.RedundancyFactor;
                 var history = new List<TInput>();
                 history.Add(_localInputBuffer[frameToSend]); 

                 for (int i = 1; i < redundancy; i++)
                 {
                     int histFrame = frameToSend - i;
                     if (histFrame >= 0 && _localInputBuffer.ContainsKey(histFrame))
                     {
                         history.Add(_localInputBuffer[histFrame]);
                     }
                     else break;
                 }

                 // Generic Position/Hash handling
                 int px = 0;
                 int py = 0;
                 int hash = 0;

                 if (Simulation != null)
                 {
                      var state = Simulation.CaptureState();
                      if (state is IDeterministicState det)
                      {
                          hash = det.CalculateHash();
                      }
                      
                      // For position, we don't have a generic "GetPlayerPos(i)".
                      // We will rely on Hash for now, or assume the TState/Input contains it?
                      // Actually, the InputPacket requires it.
                      // We will leave it as 0 for strictly agnostic impl.
                      // If the game needs position syncing, it should be part of the protocol or derived from hash mismatch.
                 }
                 
                 bundle = new OutgoingInputBundle<TInput>
                 {
                     PlayerId = _localPlayerId,
                     Frame = frameToSend,
                     RedundantHistory = history.ToArray(),
                     LocalPosX = px,
                     LocalPosY = py,
                     LocalStateHash = hash
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

        public void Step(TInput localInput)
        {
            if (IsReplaying)
            {
                 if (IsReplayFinished) return;
                 TInput[] replayInputs = _recorder.GetFrame(_replayFrame);
                 if (replayInputs == null || replayInputs.Length == 0)
                 {
                     IsReplayFinished = true;
                     return;
                 }
                 Simulation?.Update(replayInputs, _fixedTimeStep);
                 _replayFrame++;
                 return;
            }

            // Synthesize inputs for disconnected players
            foreach (int pid in _disconnectedPlayers)
            {
                int lastFrame = -1;
                if (_lastConfirmedRemoteFrame.ContainsKey(pid)) lastFrame = _lastConfirmedRemoteFrame[pid];
                int targetFrame = _currentFrame + 1;
                for (int f = lastFrame + 1; f <= targetFrame; f++)
                {
                    AddRemoteInput(pid, f, new TInput()); 
                    _lastConfirmedRemoteFrame[pid] = f;
                }
            }

            _localInputBuffer[_currentFrame] = localInput;

            if (SimulateNetworked)
            {
                int minConfirmedFrame = _currentFrame;
                for (int i = 0; i < _totalPlayers; i++)
                {
                    if (i == _localPlayerId) continue;
                    if (_lastConfirmedRemoteFrame.TryGetValue(i, out int lastFrame))
                    {
                        if (lastFrame < minConfirmedFrame) minConfirmedFrame = lastFrame;
                    }
                    else minConfirmedFrame = 0; 
                }

                if (_currentFrame > minConfirmedFrame + MaxPredictionFrames) return; 

                TInput[] inputs = new TInput[_totalPlayers];
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
                
                if (IsRecording) _recorder.RecordFrame(inputs);

                if (Simulation != null) 
                {
                    Simulation.Update(inputs, _fixedTimeStep);
                    _snapshotStore.Save(_currentFrame, Simulation.CaptureState());
                    
                    int oldestFrameToKeep = _currentFrame - MaxSnapshotFrames;
                    if (_localInputBuffer.ContainsKey(oldestFrameToKeep - 1)) _localInputBuffer.Remove(oldestFrameToKeep - 1);
                    if (_remoteInputBuffer.ContainsKey(oldestFrameToKeep - 1)) _remoteInputBuffer.Remove(oldestFrameToKeep - 1);
                }
            }
            else
            {
                TInput[] inputs = new TInput[] { localInput };
                if (IsRecording) _recorder.RecordFrame(inputs);
                Simulation?.Update(inputs, _fixedTimeStep);
            }
            
            _currentFrame++; 
        }

         private TInput PredictInputForPlayer(int playerId)
        {
            // TODO (LA3 - Prediction): predict a remote player's input for the current frame.
            //  - If we have a last-confirmed input for this player
            //    (_lastConfirmedRemoteInputs.TryGetValue(playerId, out var last)), repeat it.
            //  - Otherwise return a default input: new TInput().
            throw new System.NotImplementedException("LA3: implement PredictInputForPlayer");
        }



        public InputResult HandleRemoteInput(int pid, int startFrame, TInput[] inputs, int remotePosX, int remotePosY, int remoteHash)
        {
             int earliestMisprediction = -1;
             
             if (inputs.Length > 0 && _detector.IsInputTooOld(startFrame - (inputs.Length - 1), _snapshotStore))
             {
                 return InputResult.TooOld;
             }
             
             for (int i = inputs.Length - 1; i >= 0; i--)
             {
                 int frame = startFrame - i;
                 if (frame < 0) continue;
                 AddRemoteInput(pid, frame, inputs[i]);
             }

             _detector.DetectInputMisprediction(pid, startFrame, inputs, _currentFrame, _recorder, ref earliestMisprediction);
             // Note: We ignore Pos verification for now due to agnosticism, only Hash.
             _detector.DetectStateDesync(pid, startFrame, remoteHash, _currentFrame, _snapshotStore, ref earliestMisprediction);

             if (inputs.Length > 0)
             {
                 _lastConfirmedRemoteInputs[pid] = inputs[0];
                 _lastConfirmedRemoteFrame[pid] = startFrame;
             }

             if (earliestMisprediction != -1)
             {
                 if (!_snapshotStore.Has(earliestMisprediction - 1))
                 {
                     return InputResult.TooOld;
                 }
                 PerformRollback(earliestMisprediction);
                 return InputResult.Misprediction;
             }
             
             return InputResult.Success;
        }

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
