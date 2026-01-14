using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Bomberman.App.States
{
    /// <summary>
    /// A simple state that displays a message and waits for user confirmation.
    /// Used for alerts or simple notifications.
    /// </summary>
    public class PromptState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private string _message;
        private Action _onConfirm;

        public PromptState(GameContext context, GameStateManager manager, string message, Action onConfirm)
        {
            _context = context;
            _manager = manager;
            _message = message;
            _onConfirm = onConfirm;
        }

        public void Enter()
        {
            // Input service handles state
        }

        public void Exit()
        {
        }

        public void Update(GameTime gameTime)
        {
            // Use abstract input checks
            if (_context.Input.IsMenuSelect() || _context.Input.IsMenuCancel() || _context.Input.IsMenuToggle())
            {
                _onConfirm?.Invoke();
            }
        }

        public void Draw(GameTime gameTime)
        {
            _context.Renderer.ClearScreen(Color.Black);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.GraphicsDevice.Viewport.Width / 2;
            int centerY = _context.Game.GraphicsDevice.Viewport.Height / 2;

            // Draw Box (simulated by text for now)
            _context.Renderer.DrawTextCentered("NOTICE", centerX, centerY - 50, Color.Yellow, 4);
            _context.Renderer.DrawTextCentered(_message, centerX, centerY + 20, Color.White, 2);
            _context.Renderer.DrawTextCentered("Press [ENTER] to Continue", centerX, centerY + 100, Color.Gray, 2);

            _context.Renderer.EndDraw();
        }
    }
}
