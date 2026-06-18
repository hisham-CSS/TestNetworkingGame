using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Chronos.Net;
using Bomberman.Core.Input;
using Bomberman.App.Input;

namespace Bomberman.App.States
{
    /// <summary>
    /// The main menu state.
    /// Handles navigation options to Host Game, Join Game (Browser), Watch Replay, or Quit.
    /// </summary>
    public class MenuState : Bomberman.App.States.IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        
        private string? _message;
        
        // Navigation
        private int _selectedIndex = 0;
        private string[] _menuOptions = new string[] 
        { 
            "HOST GAME", 
            "FIND LAN GAME", 
            "REPLAYS", 
            "QUIT" 
        };

        public MenuState(GameContext context, GameStateManager manager, string? message = null)
        {
            _context = context;
            _manager = manager;
            _message = message;
        }

        public void Enter()
        {
            _context.Logger.Info("[MenuState] Enter");
            if (_context.Network != null)
            {
                _context.Network.Close();
                _context.Network = null;
            }
            _selectedIndex = 0;
        }
        public void Exit()
        {
            _context.Logger.Info("[MenuState] Exit");
        }

        public void Update(GameTime gameTime)
        {
            if (_context.Input.IsMenuDown())
            {
                _selectedIndex++;
                if (_selectedIndex >= _menuOptions.Length) _selectedIndex = 0;
            }
            if (_context.Input.IsMenuUp())
            {
                _selectedIndex--;
                if (_selectedIndex < 0) _selectedIndex = _menuOptions.Length - 1;
            }
            
            if (_context.Input.IsMenuSelect())
            {
                ExecuteSelection();
            }
        }
        private void ExecuteSelection()
        {
            switch (_selectedIndex)
            {
                case 0: // HOST
                    _context.Network?.Close();
                    _context.Network = new NetworkController<InputState>(new UdpTransport(5000));
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
            _context.Renderer.ClearScreen(Rendering.Theme.Bg);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.WindowWidth / 2;
            // Title
            _context.Renderer.DrawTextCentered("BOMBERMAN", centerX, 80, Rendering.Theme.Title, 8);
            _context.Renderer.DrawTextCentered("ROLLBACK NETWORKING", centerX, 135, Rendering.Theme.Muted, 2);
            
            // Menu
            int startY = 220;
            int gap = 30;

            for (int i = 0; i < _menuOptions.Length; i++)
            {
                bool selected = (i == _selectedIndex);
                Color color = selected ? Rendering.Theme.Accent : Rendering.Theme.Text;
                string text = _menuOptions[i];
                if (selected) text = $"> {text} <";
                
                _context.Renderer.DrawTextCentered(text, centerX, startY + (i * gap), color, 2);
            }
            
            // Message
            if (!string.IsNullOrEmpty(_message))
            {
                _context.Renderer.DrawTextCentered(_message, centerX, 380, Rendering.Theme.Bad, 2);
            }
            
            // Controls Hint
            _context.Renderer.DrawTextCentered("[UP/DOWN] SELECT   [ENTER] CONFIRM", centerX, 400, Rendering.Theme.Muted, 1);

            _context.Renderer.EndDraw();
        }
    }
}
