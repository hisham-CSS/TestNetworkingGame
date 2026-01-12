using Bomberman.Core.Input;
using Bomberman.Core;



using Bomberman.Core.Rollback;
using Bomberman.Core.Game;

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
            // Dummy LocalPlayerId (0), will be overridden or ignored during replay view?
            // Actually RollbackSystem needs a valid ID for some checks, but for replay viewing 
            // we typically just watch. We'll pass 0.
            RollbackSystem = new RollbackSystem(0, 2); 
            RollbackSystem.InitializeFromReplay(replayPath);
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

        public void DisconnectPlayer(int pid)
        {
            RollbackSystem.SetPlayerDisconnected(pid);
        }
    }
}
