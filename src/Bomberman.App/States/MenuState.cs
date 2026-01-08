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
        
        // Browser Logic
        private Dictionary<IPEndPoint, (string Name, int Current, int Max, float Time)> _foundServers = new Dictionary<IPEndPoint, (string, int, int, float)>();
        private float _discoveryTimer = 0;
        private int _browserSelection = 0;
        private bool _prevDown = false;
        private bool _prevUp = false;
        private bool _prevEnter = false;

        public MenuState(GameContext context, GameStateManager manager)
        {
            _context = context;
            _manager = manager;
        }

        public void Enter()
        {
            Console.WriteLine("[MenuState] Enter");
            // Ensure NetworkController exists for discovery
            // If not, maybe create a temporary one or use the main one?
            // Game1 likely initializes it? Or should we initialize it here?
            // For discovery, we need a random port or fixed client port?
            // Game1 currently does: _networkController = new NetworkController(0); if host.
            // Wait, Browser needs to LISTEN. So it needs a port.
            
            // If we are just browsing, we can bind to any port.
            if (_context.Network == null)
            {
                // We need to ask Game1 to create it, or create it and assign to Context
                // context.Network is a reference to Game1's controller.
                // But Game1 might manage the lifecycle.
                // Let's assume Game1 provides a valid NetworkController or we initialize it.
                // Actually, Game1 logic was: Create NC on Host or Join. 
                // BUT for Discovery, we need to send/receive.
                // So we should have a NetworkController active in Menu too?
                // Yes.
                
                 if (_context.Network == null)
                 {
                     // Bind to 0 (random) for client
                     _context.Network = new NetworkController(0); 
                 }
            }

            if (_context.Network != null)
            {
                _context.Network.OnDiscoveryResponseReceived += HandleDiscoveryResponse;
                _context.Network.OnJoinRequestRaw += HandleJoinRequestRaw; // Shouldn't happen in Menu but good to clear
            }
            
            _foundServers.Clear();
            _discoveryTimer = 0; // Trigger immediate broadcast
        }

        public void Exit()
        {
            Console.WriteLine("[MenuState] Exit");
            if (_context.Network != null)
            {
                _context.Network.OnDiscoveryResponseReceived -= HandleDiscoveryResponse;
                _context.Network.OnJoinRequestRaw -= HandleJoinRequestRaw;
            }
            // Do NOT close network here, we hand it off to Lobby/Play
        }

        private void HandleDiscoveryResponse(IPEndPoint sender, string name, int cur, int max)
        {
            _foundServers[sender] = (name, cur, max, 0);
        }

        private void HandleJoinRequestRaw(IPEndPoint sender) { }

        public void Update(GameTime gameTime)
        {
             if (_context.Network != null) _context.Network.Update();

             var kState = Keyboard.GetState();

             if (kState.IsKeyDown(Keys.H))
            {
                // HOST
                // Re-init network on port 5000 (default host)
                _context.Network?.Close();
                _context.Network = new NetworkController(5000);
                
                // Go to Lobby
                // We need to pass info that we are HOST (locald = 0)
                _manager.ChangeState(new LobbyState(_context, _manager, true));
                return;
            }

            // Discovery Broadcast
            _discoveryTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_discoveryTimer <= 0)
            {
                // Broadcast to range
                if (_context.Network != null)
                {
                    _context.Network.BroadcastDiscoveryRequest(5000, 5010);
                }
                _discoveryTimer = 2.0f;
            }

            // Prune old servers
            List<IPEndPoint> toRemove = new List<IPEndPoint>();
            foreach(var kvp in _foundServers)
            {
                var val = kvp.Value;
                val.Time += (float)gameTime.ElapsedGameTime.TotalSeconds;
                _foundServers[kvp.Key] = val;
                if (val.Time > 5.0f) toRemove.Add(kvp.Key);
            }
            foreach(var r in toRemove) _foundServers.Remove(r);

            // Menu Navigation
            bool down = kState.IsKeyDown(Keys.Down);
            bool up = kState.IsKeyDown(Keys.Up);
            bool enter = kState.IsKeyDown(Keys.Enter);

            if (down && !_prevDown) _browserSelection++;
            if (up && !_prevUp) _browserSelection--;
            
            int count = _foundServers.Count;
            if (count > 0)
            {
                if (_browserSelection < 0) _browserSelection = count - 1;
                if (_browserSelection >= count) _browserSelection = 0;

                if (enter && !_prevEnter)
                {
                    // JOIN
                    var endpoint = new List<IPEndPoint>(_foundServers.Keys)[_browserSelection];
                    
                    // We stick with our current port (random)
                    _context.Network?.Connect(endpoint.Address.ToString(), endpoint.Port);
                    
                    _manager.ChangeState(new LobbyState(_context, _manager, false)); // Client
                }
            }

            _prevDown = down;
            _prevUp = up;
            _prevEnter = enter;
        }

        public void Draw(GameTime gameTime)
        {
            _context.Game.GraphicsDevice.Clear(Color.Black);

            _context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
            
            _context.Font.DrawText(_context.SpriteBatch, 10, 10, "BOMBERMAN", Color.White, 4);
            _context.Font.DrawText(_context.SpriteBatch, 10, 60, "Press [H] to HOST", Color.Yellow, 2);
            _context.Font.DrawText(_context.SpriteBatch, 10, 90, "Server Browser:", Color.Cyan, 2);

            int y = 120;
            int i = 0;
            foreach(var kvp in _foundServers)
            {
                string marker = (i == _browserSelection) ? "> " : "  ";
                string txt = $"{marker}{kvp.Value.Name} ({kvp.Value.Current}/{kvp.Value.Max}) - {kvp.Key}";
                _context.Font.DrawText(_context.SpriteBatch, 20, y, txt, Color.White, 2);
                y += 24;
                i++;
            }
            
            if (_foundServers.Count == 0)
            {
                 _context.Font.DrawText(_context.SpriteBatch, 20, 120, "Searching...", Color.Gray, 2);
            }

            _context.SpriteBatch.End();
        }
    }
}
