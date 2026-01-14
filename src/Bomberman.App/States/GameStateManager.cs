using Microsoft.Xna.Framework;

namespace Bomberman.App.States
{
    /// <summary>
    /// Manages the active game state and transitions.
    /// </summary>
    public class GameStateManager
    {
        private IGameState? _currentState;

        /// <summary>The currently active state.</summary>
        public IGameState? CurrentState => _currentState;

        /// <summary>
        /// Transitions to a new state, calling Exit on the current state and Enter on the new one.
        /// </summary>
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

        /// <summary>Updates the current state.</summary>
        public void Update(GameTime gameTime)
        {
            _currentState?.Update(gameTime);
        }

        /// <summary>Draws the current state.</summary>
        public void Draw(GameTime gameTime)
        {
            _currentState?.Draw(gameTime);
        }
    }
}
