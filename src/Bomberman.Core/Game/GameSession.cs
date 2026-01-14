using Bomberman.Core.Input;
using Bomberman.Core;



using Bomberman.Core.Rollback;
using Bomberman.Core.Game;

namespace Bomberman.Core.Game
{
    /// <summary>
    /// Manages the high-level game session, including the rollback system and simulation state.
    /// Acts as the bridge between networking/input and the core simulation.
    /// </summary>
    public class GameSession
    {
        public RollbackSystem RollbackSystem { get; private set; }
        public Simulation? Simulation => RollbackSystem.Simulation;
        public int CurrentFrame => RollbackSystem.CurrentFrame;
        
        /// <summary>
        /// Total number of players in the session.
        /// </summary>
        public int TotalPlayers { get; private set; }

        /// <summary>
        /// Initializes a new networked game session.
        /// </summary>
        /// <param name="localPlayerId">The ID of the local player.</param>
        /// <param name="totalPlayers">Total expected players.</param>
        /// <param name="seed">Random seed associated with this session.</param>
        public GameSession(int localPlayerId, int totalPlayers, int seed)
        {
            TotalPlayers = totalPlayers;
            RollbackSystem = new RollbackSystem(localPlayerId, totalPlayers);
            RollbackSystem.IsRecording = true;
            RollbackSystem.InitializeSimulation(seed, totalPlayers);
        }

        /// <summary>
        /// Initializes a session for viewing a replay.
        /// </summary>
        /// <param name="replayPath">Absolute path to the replay JSON file.</param>
        public GameSession(string replayPath)
        {
            // Dummy LocalPlayerId (0), will be overridden or ignored during replay view?
            // Actually RollbackSystem needs a valid ID for some checks, but for replay viewing 
            // we typically just watch. We'll pass 0.
            RollbackSystem = new RollbackSystem(0, 2); 
            RollbackSystem.InitializeFromReplay(replayPath);
        }

        /// <summary>
        /// Updates the session by feeding local input into the rollback system.
        /// Should be called exactly once per frame.
        /// </summary>
        public void Update(InputState localInput)
        {
            RollbackSystem.Step(localInput);
        }

        /// <summary>
        /// Attempts to generate an input bundle to send to remote peers.
        /// </summary>
        public bool TryBuildOutgoingBundle(out OutgoingInputBundle bundle)
        {
            return RollbackSystem.TryBuildOutgoingBundle(out bundle);
        }
        
        /// <summary>
        /// Saves the current session history to a replay file.
        /// </summary>
        public void SaveReplay(string path)
        {
            if (RollbackSystem.IsRecording)
                RollbackSystem.SaveReplay(path);
        }
        
        /// <summary>
        /// Processes input received from a remote peer.
        /// </summary>
        public RollbackSystem.InputResult HandleRemoteInput(int pid, int startFrame, InputState[] inputs, Bomberman.Core.IntVector2 remotePos, int remoteHash)
        {
            return RollbackSystem.HandleRemoteInput(pid, startFrame, inputs, remotePos, remoteHash);
        }

        /// <summary>
        /// Marks a player as disconnected, stopping prediction for them.
        /// </summary>
        public void DisconnectPlayer(int pid)
        {
            RollbackSystem.SetPlayerDisconnected(pid);
        }
    }
}
