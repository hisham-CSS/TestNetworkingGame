using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.Net;
using Bomberman.App.Input;

namespace Bomberman.App.States
{
    public class MenuState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        
        private string? _message;
        
        // Navigation
        private int _selectedIndex = 0;
        private string[] _menuOptions = new string[] 
        { 
            "HOST GAME", 
            "JOIN GAME", 
            "REPLAYS", 
            "EXIT" 
        };

        public MenuState(GameContext context, GameStateManager manager, string? message = null)
        {
            _context = context;
            _manager = manager;
            _message = message;
        }

        public void Enter()
        {
            Console.WriteLine("[MenuState] Enter");
            if (_context.Network != null)
            {
                _context.Network.Close();
                _context.Network = null;
            }
            _selectedIndex = 0;
        }

        public void Exit()
        {
            Console.WriteLine("[MenuState] Exit");
        }

        public void Update(GameTime gameTime)
        {
            // Navigation
            if (_context.Input.IsMenuUp())
            {
                _selectedIndex--;
                if (_selectedIndex < 0) _selectedIndex = _menuOptions.Length - 1;
            }
            else if (_context.Input.IsMenuDown())
            {
                _selectedIndex++;
                if (_selectedIndex >= _menuOptions.Length) _selectedIndex = 0;
            }

            // Selection
            if (_context.Input.IsMenuSelect())
            {
                ExecuteSelection();
            }

            // Legacy H/J/R Hotkeys (Optional: Keep or Remove? Removing for consistency)
            // ESC
            if (_context.Input.IsMenuCancel())
            {
                _context.Game.Exit(); // Exit on ESC from Main Menu
            }
        }

        private void ExecuteSelection()
        {
            switch (_selectedIndex)
            {
                case 0: // HOST
                    _context.Network?.Close();
                    _context.Network = new NetworkController(new UdpTransport(5000));
                    _manager.ChangeState(_context.StateFactory.CreateLobby(true, null));
                    break;
                case 1: // JOIN
                    _context.Network?.Close();
                    _manager.ChangeState(_context.StateFactory.CreateServerBrowser());
                    break;
                case 2: // REPLAYS
                    _manager.ChangeState(_context.StateFactory.CreateReplaySelect());
                    break;
                case 3: // EXIT
                    _context.Game.Exit();
                    break;
            }
        }

        public void Draw(GameTime gameTime)
        {
            _context.Game.GraphicsDevice.Clear(Color.CornflowerBlue);
            _context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);

            int centerX = _context.Game.GraphicsDevice.Viewport.Width / 2;
            // Title
            DrawCenteredText(_context.SpriteBatch, "BOMBERMAN", centerX, 80, Color.White, 8);
            
            // Menu
            int startY = 220;
            int gap = 30;

            for (int i = 0; i < _menuOptions.Length; i++)
            {
                bool selected = (i == _selectedIndex);
                Color color = selected ? Color.Yellow : Color.White;
                string text = _menuOptions[i];
                if (selected) text = $"> {text} <";
                
                DrawCenteredText(_context.SpriteBatch, text, centerX, startY + (i * gap), color, 2);
            }
            
            // Message
            if (!string.IsNullOrEmpty(_message))
            {
                DrawCenteredText(_context.SpriteBatch, _message, centerX, 380, Color.OrangeRed, 2);
            }
            
            // Controls Hint
            DrawCenteredText(_context.SpriteBatch, "[UP/DOWN] Select   [ENTER] Confirm", centerX, 400, Color.LightGray, 1);

            _context.SpriteBatch.End();
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, string text, int x, int y, Color color, int scale)
        {
            var size = _context.Font.MeasureString(text, scale);
            _context.Font.DrawText(spriteBatch, x - size.X / 2, y - size.Y / 2, text, color, scale);
        }
    }
}
