using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.Net;

namespace Bomberman.App.States
{
    public class MenuState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private KeyboardState _prevKeyboard;
        
        private string? _message;

        public MenuState(GameContext context, GameStateManager manager, string? message = null)
        {
            _context = context;
            _manager = manager;
            _message = message;
        }

        public void Enter()
        {
            Console.WriteLine("[MenuState] Enter");
            _prevKeyboard = Keyboard.GetState();
            // Clean up any existing network session when returning to Menu
            if (_context.Network != null)
            {
                _context.Network.Close();
                _context.Network = null;
            }
        }

        public void Exit()
        {
            Console.WriteLine("[MenuState] Exit");
        }

        public void Update(GameTime gameTime)
        {
            var kState = Keyboard.GetState();

            if (IsNewKeyPress(kState, Keys.H))
            {
                // HOST
                _context.Network?.Close();
                _context.Network = new NetworkController(5000);
                
                // Host starts Lobby directly
                _manager.ChangeState(new LobbyState(_context, _manager, true, null));
            }
            
            if (IsNewKeyPress(kState, Keys.J))
            {
                // JOIN (Server Browser)
                _context.Network?.Close();
                _manager.ChangeState(new ServerBrowserState(_context, _manager));
            }

            if (IsNewKeyPress(kState, Keys.R))
            {
                _manager.ChangeState(new ReplaySelectState(_context, _manager));
            }

            if (IsNewKeyPress(kState, Keys.Escape))
            {
                _context.Game.Exit();
            }

            _prevKeyboard = kState;
        }

        private bool IsNewKeyPress(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && !_prevKeyboard.IsKeyDown(key);
        }

        public void Draw(GameTime gameTime)
        {
            _context.Game.GraphicsDevice.Clear(Color.CornflowerBlue);
            _context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);

            int centerX = _context.Game.GraphicsDevice.Viewport.Width / 2;
            int centerY = _context.Game.GraphicsDevice.Viewport.Height / 2;

            DrawCenteredText(_context.SpriteBatch, "BOMBERMAN", centerX, 100, Color.White, 8);
            
            int startY = 300;
            int gap = 50;

            DrawCenteredText(_context.SpriteBatch, "Press [H] to HOST Game", centerX, startY, Color.Yellow, 2);
            DrawCenteredText(_context.SpriteBatch, "Press [J] to JOIN Game (Browser)", centerX, startY + gap, Color.Green, 2);
            DrawCenteredText(_context.SpriteBatch, "Press [R] for REPLAYS", centerX, startY + gap * 2, Color.Cyan, 2);
            DrawCenteredText(_context.SpriteBatch, "Press [ESC] to Quit", centerX, startY + gap * 3, Color.Red, 2);
            
            if (!string.IsNullOrEmpty(_message))
            {
                DrawCenteredText(_context.SpriteBatch, _message, centerX, 500, Color.OrangeRed, 2);
            }

            _context.SpriteBatch.End();
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, string text, int x, int y, Color color, int scale)
        {
            var size = _context.Font.MeasureString(text, scale);
            _context.Font.DrawText(spriteBatch, x - size.X / 2, y - size.Y / 2, text, color, scale);
        }
    }
}
