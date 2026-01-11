using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Bomberman.App.States
{
    public class PromptState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private string _message;
        private Action _onConfirm;
        private KeyboardState _prevKeyboard;

        public PromptState(GameContext context, GameStateManager manager, string message, Action onConfirm)
        {
            _context = context;
            _manager = manager;
            _message = message;
            _onConfirm = onConfirm;
        }

        public void Enter()
        {
            _prevKeyboard = _context.Input.GetKeyboard();
        }

        public void Exit()
        {
        }

        public void Update(GameTime gameTime)
        {
            var kState = _context.Input.GetKeyboard();
            
            if (kState.IsKeyDown(Keys.Enter) && !_prevKeyboard.IsKeyDown(Keys.Enter) ||
                kState.IsKeyDown(Keys.Space) && !_prevKeyboard.IsKeyDown(Keys.Space) ||
                kState.IsKeyDown(Keys.Escape) && !_prevKeyboard.IsKeyDown(Keys.Escape))
            {
                _onConfirm?.Invoke();
            }

            _prevKeyboard = kState;
        }

        public void Draw(GameTime gameTime)
        {
            _context.Game.GraphicsDevice.Clear(Color.Black);
            _context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);

            int centerX = _context.Game.GraphicsDevice.Viewport.Width / 2;
            int centerY = _context.Game.GraphicsDevice.Viewport.Height / 2;

            // Draw Box (simulated by text for now)
            DrawCenteredText(_context.SpriteBatch, "NOTICE", centerX, centerY - 50, Color.Yellow, 4);
            DrawCenteredText(_context.SpriteBatch, _message, centerX, centerY + 20, Color.White, 2);
            DrawCenteredText(_context.SpriteBatch, "Press [ENTER] to Continue", centerX, centerY + 100, Color.Gray, 2);

            _context.SpriteBatch.End();
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, string text, int x, int y, Color color, int scale)
        {
            if (string.IsNullOrEmpty(text)) return;
            var size = _context.Font.MeasureString(text, scale);
            int posX = x - size.X / 2;
            int posY = y - size.Y / 2;
            _context.Font.DrawText(spriteBatch, posX, posY, text, color, scale);
        }
    }
}
