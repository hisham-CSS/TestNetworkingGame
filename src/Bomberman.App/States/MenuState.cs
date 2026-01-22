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
            "HOST GAME (LAN)", 
            "HOST GAME (RELAY)",
            "JOIN GAME (LAN)", 
            "JOIN GAME (RELAY)",
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
                case 0: // HOST (LAN)
                    _context.Network?.Close();
                    var hostTransport = new SimulatedLagTransport(new UdpTransport(5000));
                    _context.Network = new NetworkController<InputState>(hostTransport);
                    _manager.ChangeState(_context.StateFactory.CreateLobby(true, null));
                    break;

                case 1: // HOST (RELAY)
                    _context.Network?.Close();
                     // Ask for Relay IP (so host can connect to public relay too)
                     _manager.ChangeState(new TextInputState(_context, _manager, "Enter Relay IP (Default 127.0.0.1):", (ip) => 
                     {
                         if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
                         
                         _manager.ChangeState(new TextInputState(_context, _manager, "Enter Session ID to Host (e.g. 1234):", (sessIdStr) => 
                        {
                            if (ushort.TryParse(sessIdStr, out ushort sessionId))
                            {
                                var relayTransport = new RelayTransport(ip, 7777, sessionId, 0); // Host is Player 0
                                _context.Network = new NetworkController<InputState>(relayTransport);
                                _context.Network.Connect(ip, 7777); 
                                
                                // Wait for connection
                                _manager.ChangeState(new ConnectingState(_context, _manager, 
                                    onConnected: () => _manager.ChangeState(_context.StateFactory.CreateLobby(true, null)),
                                    onFailure: () => {
                                        _context.Network?.Close();
                                        _context.Network = null;
                                        _manager.ChangeState(_context.StateFactory.CreateMenu("Connection Failed (Relay Timeout)"));
                                    }
                                ));
                            }
                            else
                            {
                                _manager.ChangeState(_context.StateFactory.CreateMenu("Invalid Session ID"));
                            }
                        }));
                     }));
                    break;
                case 2: // JOIN (LAN)
                    _context.Network?.Close();
                    var clientTransport = new SimulatedLagTransport(new UdpTransport(0)); 
                    _context.Network = new NetworkController<InputState>(clientTransport);
                    _manager.ChangeState(_context.StateFactory.CreateServerBrowser());
                    break;
                    
                case 3: // JOIN (RELAY)
                     // 1. Ask for IP
                     _manager.ChangeState(new TextInputState(_context, _manager, "Enter Relay IP (Default 127.0.0.1):", (ip) => 
                     {
                         if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
                         
                         // 2. Ask for Session ID
                         _manager.ChangeState(new TextInputState(_context, _manager, "Enter Session ID:", (sess) => 
                         {
                             if (ushort.TryParse(sess, out ushort sid))
                             {
                                 // Join as randomly assigned ID (10-250)
                                 byte tempId = (byte)new Random().Next(10, 250);
                                 var transport = new RelayTransport(ip, 7777, sid, tempId);
                                 _context.Network = new NetworkController<InputState>(transport);
                                 _context.Network.Connect(ip, 7777); 
                                 
                                 _manager.ChangeState(new ConnectingState(_context, _manager,
                                     onConnected: () => _manager.ChangeState(_context.StateFactory.CreateLobby(false, null)),
                                     onFailure: () => {
                                         _context.Network?.Close();
                                         _context.Network = null;
                                         _manager.ChangeState(_context.StateFactory.CreateMenu("Connection Failed (Relay Timeout)"));
                                     }
                                 ));
                             }
                             else
                             {
                                 _manager.ChangeState(_context.StateFactory.CreateMenu("Invalid Session ID"));
                             }
                         }));
                     }));
                    break;
                    
                case 4: // REPLAYS
                    _manager.ChangeState(_context.StateFactory.CreateReplaySelect());
                    break;
                case 5: // EXIT
                    _context.Game.Exit();
                    break;
            }
        }

        public void Draw(GameTime gameTime)
        {
            _context.Renderer.ClearScreen(Color.CornflowerBlue);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.WindowWidth / 2;
            // Title
            _context.Renderer.DrawTextCentered("BOMBERMAN", centerX, 80, Color.White, 8);
            
            // Menu
            int startY = 220;
            int gap = 30;

            for (int i = 0; i < _menuOptions.Length; i++)
            {
                bool selected = (i == _selectedIndex);
                Color color = selected ? Color.Yellow : Color.White;
                string text = _menuOptions[i];
                if (selected) text = $"> {text} <";
                
                _context.Renderer.DrawTextCentered(text, centerX, startY + (i * gap), color, 2);
            }
            
            // Message
            if (!string.IsNullOrEmpty(_message))
            {
                _context.Renderer.DrawTextCentered(_message, centerX, 380, Color.OrangeRed, 2);
            }
            
            // Controls Hint
            _context.Renderer.DrawTextCentered("[UP/DOWN] Select   [ENTER] Confirm", centerX, 400, Color.LightGray, 1);

            _context.Renderer.EndDraw();
        }
    }
}
