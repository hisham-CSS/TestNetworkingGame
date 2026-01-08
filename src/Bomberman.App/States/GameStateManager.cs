using Microsoft.Xna.Framework;

namespace Bomberman.App.States
{
    public class GameStateManager
    {
        private IGameState? _currentState;

        public IGameState? CurrentState => _currentState;

        public void ChangeState(IGameState newState)
        {
            if (_currentState != null)
            {
                _currentState.Exit();
            }

            _currentState = newState;

            if (_currentState != null)
            {
                _currentState.Enter();
            }
        }

        public void Update(GameTime gameTime)
        {
            _currentState?.Update(gameTime);
        }

        public void Draw(GameTime gameTime)
        {
            _currentState?.Draw(gameTime);
        }
    }
}
