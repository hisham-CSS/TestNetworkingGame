using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.Net;

namespace Bomberman.App.States
{
    public class ServerInfo
    {
        public required IPEndPoint Endpoint;
        public required string Name;
        public int Players;
        public int MaxPlayers;
        public DateTime LastSeen;
    }

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
            Console.WriteLine("[ServerBrowser] Entering...");
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
            Console.WriteLine("[ServerBrowser] Exiting...");
            if (_context.Network != null)
            {
                _context.Network.OnDiscoveryResponseReceived -= HandleDiscoveryResponse;
            }
        }

        private void BroadcastDiscovery()
        {
            Console.WriteLine("[ServerBrowser] Broadcasting Discovery Request...");
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
                Console.WriteLine($"[ServerBrowser] Found Server: {name} at {sender}");
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
                // If we created the NetworkController just for browsing, maybe we should keep it?
                // Actually, if we go back to Menu, we might kill it or keep it.
                // MenuState usually expects null Network or handles it?
                // For simplicity, let's keep it alive or Dispose if Menu creates a new one.
                // MenuState.Enter doesn't create one. It waits for Host/Join action.
                // We'll dispose it here to be clean, as Host mode needs specific port binding.
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
            Console.WriteLine($"[ServerBrowser] Joining {target.Name} ({target.Endpoint})...");

            // Transition to Lobby as Client
            // We reuse the current NetworkController which is bound to a random port
            _manager.ChangeState(_context.StateFactory.CreateLobby(false, target.Endpoint));
        }

        public void Draw(GameTime gameTime)
        {
            _context.Game.GraphicsDevice.Clear(Color.DarkSlateGray);
            _context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);

            int centerX = _context.Game.GraphicsDevice.Viewport.Width / 2;
            int width = _context.Game.GraphicsDevice.Viewport.Width;
            
            DrawCenteredText(_context.SpriteBatch, "SERVER BROWSER", centerX, 50, Color.White, 4); // Match Header Style
            DrawCenteredText(_context.SpriteBatch, "Scanning LAN...", centerX, 100, Color.Gray, 2); // Simpler subtext

            int startY = 150;
            int lineHeight = 30; // Tighter gap

            if (_servers.Count == 0)
            {
                DrawCenteredText(_context.SpriteBatch, "NO SERVERS FOUND...", centerX, startY, Color.Gray, 2);
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
                    DrawCenteredText(_context.SpriteBatch, row, centerX, startY + (i * lineHeight), color, 2);
                }
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
