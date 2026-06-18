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
            _context.Renderer.ClearScreen(Rendering.Theme.Bg);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.WindowWidth / 2;
            int centerY = _context.Game.WindowHeight / 2;

            // Draw Message Box
            _context.Renderer.DrawTextCentered("NOTICE", centerX, centerY - 50, Rendering.Theme.Title, 4);
            _context.Renderer.DrawTextCentered(_message, centerX, centerY + 20, Rendering.Theme.Text, 2);
            _context.Renderer.DrawTextCentered("PRESS [ENTER] TO CONTINUE", centerX, centerY + 100, Rendering.Theme.Muted, 2);

            _context.Renderer.EndDraw();
        }
    }
}
