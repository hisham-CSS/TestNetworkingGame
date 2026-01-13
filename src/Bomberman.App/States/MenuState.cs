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
            // _prevKeyboard managed by InputService
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
            if (_context.Input.IsGameHost())
            {
                // HOST
                _context.Network?.Close();
                _context.Network = new NetworkController(new UdpTransport(5000));
                
                // Host starts Lobby directly
                _manager.ChangeState(_context.StateFactory.CreateLobby(true, null));
            }
            
            
            if (_context.Input.IsGameJoin())
            {
                // JOIN (Server Browser)
                _context.Network?.Close();
                _manager.ChangeState(_context.StateFactory.CreateServerBrowser());
            }

            if (_context.Input.IsGameReplay())
            {
                _manager.ChangeState(_context.StateFactory.CreateReplaySelect());
            }

            if (_context.Input.IsMenuCancel())
            {
                _context.Game.Exit();
            }

            // _prevKeyboard managed by InputService
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
