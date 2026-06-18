using Bomberman.Core.Input;
using Bomberman.Core;
using Bomberman.Core.Game;
using Chronos.Rollback;
using Chronos.Core;

namespace Bomberman.Core.Game
{
    /// <summary>
    /// Manages the high-level game session, including the rollback system and simulation state.
    /// Acts as the bridge between networking/input and the core simulation.
    /// </summary>
    public class GameSession
    {
        public RollbackSystem<InputState, GameStateSnapshot> RollbackSystem { get; private set; }
        
        // Helper property to access the Concrete Simulation if needed (casted)
        public Simulation? Simulation => RollbackSystem.Simulation as Simulation;
        
        public int CurrentFrame => RollbackSystem.CurrentFrame;
        
        public int TotalPlayers { get; private set; }

        /// <summary>The replay file this session is playing back, if any (enables REWATCH).</summary>
        public string? ReplayPath { get; private set; }

        private const float FixedStep = 1.0f / 60.0f; // Could come from Config

        /// <summary>
        /// Initializes a new networked game session.
        /// </summary>
        public GameSession(int localPlayerId, int totalPlayers, int seed)
        {
            TotalPlayers = totalPlayers;
            // Create System
            RollbackSystem = new RollbackSystem<InputState, GameStateSnapshot>(localPlayerId, totalPlayers, FixedStep);
            
            // Create Simulation
            var simulation = new Simulation(seed, totalPlayers);
            
            // Attach
            RollbackSystem.AttachSimulation(simulation);
            
            // Initialize System (saves initial state)
            RollbackSystem.InitializeSimulation(seed, totalPlayers);
            
            RollbackSystem.IsRecording = true;
        }

        /// <summary>
        /// Initializes a session for viewing a replay.
        /// </summary>
        public GameSession(string replayPath)
        {
            ReplayPath = replayPath;
            // Replay viewing uses a dummy player ID (0)
            RollbackSystem = new RollbackSystem<InputState, GameStateSnapshot>(0, 2, FixedStep);
            
            // We need to Load replay first to get seed/players?
            // Chronos RollbackSystem.InitializeFromReplay loads the recorder.
            // But we need to Create the Simulation manually?
            // Wait, Chronos.RollbackSystem.InitializeFromReplay calls InitializeSimulation, which assumes attached simulation?
            // Ah, I need to look at my Chronos implementation.
            
            // Chronos.RollbackSystem.InitializeFromReplay code:
            // _recorder.Load(path);
            // InitializeSimulation(_recorder.Seed, _recorder.TotalPlayers);
            
            // And InitializeSimulation throws if Simulation not attached.
            // So we face a chicken-egg problem. We need seed/players to create Simulation, but we get them from Recorder load.
            // AND we need to attach Simulation BEFORE InitializeSimulation is called.
            
            // Soln: Load Recorder manually first here? Or add a way to defer Simulation creation?
            // Actually, in Replay mode, `InitializeFromReplay` does `InitializeSimulation`.
            
            // I should load the replay metadata first using InputRecorder?
            // InputRecorder<T> has Load(path).
            
            var recorder = new InputRecorder<InputState>();
            recorder.Load(replayPath);
            
            if (recorder.FrameCount > 0)
            {
                 var sim = new Simulation(recorder.Seed, recorder.TotalPlayers);
                 RollbackSystem.AttachSimulation(sim);
                 // Now tell System to init
                 RollbackSystem.InitializeFromReplay(replayPath);
            }
        }

        public void Update(InputState localInput)
        {
            RollbackSystem.Step(localInput);
        }

        public bool TryBuildOutgoingBundle(out OutgoingInputBundle<InputState> bundle)
        {
            return RollbackSystem.TryBuildOutgoingBundle(out bundle);
        }
        
        public void SaveReplay(string path)
        {
            if (RollbackSystem.IsRecording)
                RollbackSystem.SaveReplay(path);
        }
        
        public Chronos.Rollback.InputResult HandleRemoteInput(int pid, int startFrame, InputState[] inputs, Bomberman.Core.IntVector2 remotePos, int remoteHash)
        {
            // We need to map generic call.
            // Note: Our Generic HandleRemoteInput takes (pid, startFrame, inputs, posX, posY, hash)
            // We map IntVector2 to X/Y
            return RollbackSystem.HandleRemoteInput(pid, startFrame, inputs, remotePos.X, remotePos.Y, remoteHash);
        }

        public void DisconnectPlayer(int pid)
        {
            RollbackSystem.SetPlayerDisconnected(pid);
        }
    }
}
