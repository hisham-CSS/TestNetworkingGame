using System;
using Bomberman.Core;



using Bomberman.Core.Rollback;

namespace Bomberman.Core.Game
{
    public class GameSession
    {
        public RollbackSystem RollbackSystem { get; private set; }
        public Simulation? Simulation => RollbackSystem.Simulation;
        public int CurrentFrame => RollbackSystem.CurrentFrame;

        public GameSession(int localPlayerId, int totalPlayers, int seed)
        {
            TotalPlayers = totalPlayers;
            RollbackSystem = new RollbackSystem(localPlayerId, totalPlayers);
            RollbackSystem.IsRecording = true;
            RollbackSystem.InitializeSimulation(seed, totalPlayers);
        }

        public int TotalPlayers { get; private set; }
        
        // Constructor for Replay
        public GameSession(string replayPath)
        {
            // Replay mode (player ID 0 for view, but logic might vary)
            // RollbackSystem constructor requires localPlayerId. For replay view, maybe 0?
            RollbackSystem = new RollbackSystem(0, 2); // Default 2? Will be overwritten by LoadReplay?
            // Wait, LoadReplay sets simulation?
            // RollbackSystem.LoadReplay reads seed and totalPlayers?
            // RollbackSystem.LoadReplay implementation needs checking.
            
            // For now, I'll expose a method to load replay or just use the same constructor and call logic externally
            // But GameSession should encapsulate it.
             RollbackSystem = new RollbackSystem(0, 2); 
             RollbackSystem.LoadReplay(replayPath);
             // Note: InitializeSimulation call is needed after LoadReplay?
             // In Game1.cs it was: LoadReplay -> InitializeSimulation(seed, players).
             // I'll need to replicate that logic or push it into RollbackSystem.
        }

        public void Update(InputState localInput)
        {
            RollbackSystem.Step(localInput);
        }

        public bool TryBuildOutgoingBundle(out OutgoingInputBundle bundle)
        {
            return RollbackSystem.TryBuildOutgoingBundle(out bundle);
        }
        
        public void SaveReplay(string path)
        {
            if (RollbackSystem.IsRecording)
                RollbackSystem.SaveReplay(path);
        }
        
        public void HandleRemoteInput(int pid, int startFrame, InputState[] inputs, Bomberman.Core.IntVector2 remotePos, int remoteHash)
        {
            RollbackSystem.HandleRemoteInput(pid, startFrame, inputs, remotePos, remoteHash);
        }
    }
}
