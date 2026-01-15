using Bomberman.App.States;
using Microsoft.Xna.Framework;

namespace Bomberman.Tests.Mocks
{
    public class StubGameStateManager : GameStateManager
    {
        public IGameState? LastChangedState { get; private set; }
        
        public override void ChangeState(IGameState newState)
        {
            LastChangedState = newState;
            // Don't call base logic to avoid complexity in simple tests, 
            // or do? Base logic calls Exit/Enter. 
            // For unit tests, we mainly want to verify the transition was requested.
            base.ChangeState(newState);
        }
    }
}
