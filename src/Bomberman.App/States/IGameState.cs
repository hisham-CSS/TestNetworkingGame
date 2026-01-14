using Microsoft.Xna.Framework;

namespace Bomberman.App.States
{
    /// <summary>
    /// Represents a distinct state in the game application (e.g., Menu, Gameplay, Lobby).
    /// </summary>
    public interface IGameState
    {
        /// <summary>Called when the state becomes active.</summary>
        void Enter();
        /// <summary>Called when the state is being exited.</summary>
        void Exit();
        /// <summary>Updates the state logic.</summary>
        void Update(GameTime gameTime);
        /// <summary>Draws the state visualization.</summary>
        void Draw(GameTime gameTime);
    }
}
