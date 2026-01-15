using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.Net;

namespace Bomberman.App.States
{
    /// <summary>
    /// Helper class containing details about a discovered server.
    /// </summary>
    public class ServerInfo
    {
        public required IPEndPoint Endpoint;
        public required string Name;
        public int Players;
        public int MaxPlayers;
        public DateTime LastSeen;
    }

    /// <summary>
    /// State for discovering LAN servers via UDP broadcast.
    /// Lists available servers and allows joining.
    /// </summary>
    public class ServerBrowserState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;


        private List<ServerInfo> _servers = new List<ServerInfo>();
        private int _selectedIndex = 0;
        private DateTime _lastBroadcastTime;
        private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(2.0);

        // Discovery range
        private const int StartPort = 5000;
        private const int EndPort = 5010; // Scan 10 ports

        public ServerBrowserState(GameContext context, GameStateManager manager)
        {
            _context = context;
            _manager = manager;
        }

        public void Enter()
        {
            _context.Logger.Info("[ServerBrowser] Entering...");
            // Ensure NetworkController exists for discovery
            if (_context.Network == null)
            {
                _context.Network = new NetworkController(new UdpTransport(0)); // Bind to random port
            }

            _context.Network.OnDiscoveryResponseReceived += HandleDiscoveryResponse;
            
            // Initial Broadcast
            BroadcastDiscovery();
        }

        public void Exit()
        {
            _context.Logger.Info("[ServerBrowser] Exiting...");
            if (_context.Network != null)
            {
                _context.Network.OnDiscoveryResponseReceived -= HandleDiscoveryResponse;
            }
        }

        private void BroadcastDiscovery()
        {
            _context.Logger.Info("[ServerBrowser] Broadcasting Discovery Request...");
            _context.Network?.BroadcastDiscoveryRequest(StartPort, EndPort);
            _lastBroadcastTime = DateTime.Now;
        }

        private void HandleDiscoveryResponse(IPEndPoint sender, string name, int players, int max)
        {
            // Update existing or add new
            var existing = _servers.Find(s => s.Endpoint.Equals(sender));
            if (existing != null)
            {
                existing.Name = name;
                existing.Players = players;
                existing.MaxPlayers = max;
                existing.LastSeen = DateTime.Now;
            }
            else
            {
                _servers.Add(new ServerInfo
                {
                    Endpoint = sender,
                    Name = name,
                    Players = players,
                    MaxPlayers = max,
                    LastSeen = DateTime.Now
                });
                _context.Logger.Info($"[ServerBrowser] Found Server: {name} at {sender}");
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_context.Network != null) _context.Network.Update();

            if (DateTime.Now - _lastBroadcastTime > BroadcastInterval)
            {
                BroadcastDiscovery();
            }

            // Prune old servers (> 5s)
            _servers.RemoveAll(s => (DateTime.Now - s.LastSeen).TotalSeconds > 5);
            if (_selectedIndex >= _servers.Count) _selectedIndex = Math.Max(0, _servers.Count - 1);

            if (_context.Input.IsMenuCancel())
            {
                // Back to Menu
                // Ensure Network is closed as Host needs fresh binding
                _context.Network?.Close();
                _context.Network = null;
                
                _manager.ChangeState(_context.StateFactory.CreateMenu());
            }

            if (_servers.Count > 0)
            {
                if (_context.Input.IsMenuUp())
                {
                    _selectedIndex--;
                    if (_selectedIndex < 0) _selectedIndex = _servers.Count - 1;
                }
                if (_context.Input.IsMenuDown())
                {
                    _selectedIndex++;
                    if (_selectedIndex >= _servers.Count) _selectedIndex = 0;
                }

                if (_context.Input.IsMenuSelect())
                {
                    JoinSelectedServer();
                }
            }
        }



        private void JoinSelectedServer()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _servers.Count) return;

            var target = _servers[_selectedIndex];
            _context.Logger.Info($"[ServerBrowser] Joining {target.Name} ({target.Endpoint})...");

            // Transition to Lobby as Client
            // We reuse the current NetworkController which is bound to a random port
            _manager.ChangeState(_context.StateFactory.CreateLobby(false, target.Endpoint));
        }

        public void Draw(GameTime gameTime)
        {
            _context.Renderer.ClearScreen(Color.DarkSlateGray);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.WindowWidth / 2;
            int width = _context.Game.WindowWidth;
            
            _context.Renderer.DrawTextCentered("SERVER BROWSER", centerX, 50, Color.White, 4); // Match Header Style
            _context.Renderer.DrawTextCentered("Scanning LAN...", centerX, 100, Color.Gray, 2);

            int startY = 150;
            int lineHeight = 30;

            if (_servers.Count == 0)
            {
                _context.Renderer.DrawTextCentered("NO SERVERS FOUND...", centerX, startY, Color.Gray, 2);
            }
            else
            {
                for (int i = 0; i < _servers.Count; i++)
                {
                    var server = _servers[i];
                    bool selected = (i == _selectedIndex);
                    
                    string row = $"{server.Name} [{server.Players}/{server.MaxPlayers}]";
                    if (selected) row = $"> {row} <";
                    
                    Color color = selected ? Color.Yellow : Color.White;
                    _context.Renderer.DrawTextCentered(row, centerX, startY + (i * lineHeight), color, 2);
                }
            }

            _context.Renderer.EndDraw();
        }
    }
}
