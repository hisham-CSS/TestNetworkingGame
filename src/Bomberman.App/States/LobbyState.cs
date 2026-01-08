using System;
using System.IO;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Bomberman.Net;
using Bomberman.Core.Game;

namespace Bomberman.App.States
{
    public class LobbyState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private bool _isHost;

        // Lobby Data
        private int _connectedPlayerCount = 1;
        private int _totalPlayersForGame = 2;
        private int _networkSeed;
        private int _localPlayerId = -1; // -1 for client initially
        private float _joinRetryTimer = 0;

        public LobbyState(GameContext context, GameStateManager manager, bool isHost)
        {
            _context = context;
            _manager = manager;
            _isHost = isHost;
            
            if (_isHost)
            {
                _localPlayerId = 0;
                _networkSeed = new Random().Next();
                _connectedPlayerCount = 1; // Host is 1
            }
        }

        public void Enter()
        {
            Console.WriteLine($"[LobbyState] Enter. Host={_isHost}");
            if (_context.Network != null)
            {
                _context.Network.OnJoinRequestRaw += HandleJoinRequest;
                _context.Network.OnWelcomeReceived += HandleWelcome;
                _context.Network.OnLobbyUpdateReceived += HandleLobbyUpdate;
                _context.Network.OnStartGameReceived += HandleStartGame;
                _context.Network.OnDiscoveryRequestReceived += HandleDiscoveryRequest;
            }

            if (!_isHost)
            {
                // Client: Send initial join request
                _context.Network?.SendJoinRequest();
                Console.WriteLine("Sent Join Request...");
                _joinRetryTimer = 1.0f;
            }
        }

        public void Exit()
        {
             Console.WriteLine("[LobbyState] Exit");
             if (_context.Network != null)
             {
                 _context.Network.OnJoinRequestRaw -= HandleJoinRequest;
                 _context.Network.OnWelcomeReceived -= HandleWelcome;
                 _context.Network.OnLobbyUpdateReceived -= HandleLobbyUpdate;
                 _context.Network.OnStartGameReceived -= HandleStartGame;
                 _context.Network.OnDiscoveryRequestReceived -= HandleDiscoveryRequest;
             }
        }

        public void Update(GameTime gameTime)
        {
            if (_context.Network != null) _context.Network.Update();

            var kState = Keyboard.GetState();
            if (kState.IsKeyDown(Keys.Escape))
            {
                // Back to Menu
                // Host should close server
                if (_isHost)
                {
                    _context.Network?.Close();
                   // Re-open as client/browser? 
                   // Or just clear network.
                   _context.Network = null;
                }
                else
                {
                    // Client leaves
                    _context.Network?.Close();
                    _context.Network = null;
                }
                _manager.ChangeState(new MenuState(_context, _manager));
                return;
            }

            if (!_isHost && _localPlayerId == -1 && _context.Network != null)
            {
                _joinRetryTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_joinRetryTimer <= 0)
                {
                    _context.Network.SendJoinRequest();
                    Console.WriteLine("Resending Join Request...");
                    _joinRetryTimer = 1.0f;
                }
            }

            if (_isHost)
            {
                int prevPlayers = _totalPlayersForGame;
                if (kState.IsKeyDown(Keys.D2)) _totalPlayersForGame = 2;
                if (kState.IsKeyDown(Keys.D3)) _totalPlayersForGame = 3;
                if (kState.IsKeyDown(Keys.D4)) _totalPlayersForGame = 4;

                if (prevPlayers != _totalPlayersForGame)
                {
                    _context.Network?.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);
                }

                if (kState.IsKeyDown(Keys.Enter))
                {
                    if (_connectedPlayerCount >= _totalPlayersForGame)
                    {
                        // Start!
                         _context.Network?.BroadcastStartGame(_networkSeed, _totalPlayersForGame);
                         StartGame(_networkSeed, _totalPlayersForGame);
                    }
                    else
                    {
                         // Debounce or log?
                    }
                }
            }
        }

        private void StartGame(int seed, int totalPlayers)
        {
             // Transition to PlayState
             // We need to pass the game session info
             _manager.ChangeState(new PlayState(_context, _manager, _localPlayerId, totalPlayers, seed));
        }

        // --- Network Handlers ---

        private void HandleJoinRequest(IPEndPoint sender)
        {
            if (!_isHost || _context.Network == null) return;

             bool alreadyConnected = false;
             foreach(var c in _context.Network.ConnectedClients)
             {
                 if (c.Equals(sender)) 
                 {
                     alreadyConnected = true; 
                     break;
                 }
             }

             if (!alreadyConnected)
             {
                if (_connectedPlayerCount < _totalPlayersForGame)
                {
                    _context.Network.AddClient(sender);
                    int newId = _connectedPlayerCount;
                    _connectedPlayerCount++;
                    
                    _context.Network.SendWelcome(sender, newId, _networkSeed, _totalPlayersForGame);
                    _context.Network.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);

                    Console.WriteLine($"Client {newId} Joined from {sender}");
                }
             }
        }

        private void HandleWelcome(int assignedId, int seed, int totalPlayers)
        {
            if (_isHost) return;
            if (_localPlayerId == -1)
            {
                _localPlayerId = assignedId;
                _networkSeed = seed;
                _totalPlayersForGame = totalPlayers;
                Console.WriteLine($"Joined as Player {_localPlayerId}. seed={_networkSeed}");
            }
        }

        private void HandleLobbyUpdate(int connectedCount, int totalPlayers)
        {
            if (_isHost) return;
            _connectedPlayerCount = connectedCount;
            _totalPlayersForGame = totalPlayers;
        }

        private void HandleStartGame(int seed, int totalPlayers)
        {
            if (_isHost) return;
            _networkSeed = seed;
            _totalPlayersForGame = totalPlayers;
            StartGame(seed, totalPlayers);
        }

        private void HandleDiscoveryRequest(IPEndPoint sender, string header, int cur, int max)
        {
            if (_isHost && _context.Network != null)
            {
                _context.Network.SendDiscoveryResponse(sender, "Local Game", _connectedPlayerCount, _totalPlayersForGame);
            }
        }

        // --- Drawing ---

        public void Draw(GameTime gameTime)
        {
             _context.Game.GraphicsDevice.Clear(Color.CornflowerBlue);
             _context.SpriteBatch.Begin(samplerState: Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp);

             DrawText("LOBBY", new Vector2(50, 50), 3, Color.White);
             DrawText($"Players: {_connectedPlayerCount} / {_totalPlayersForGame}", new Vector2(50, 100), 2, Color.Yellow);

             if (_isHost)
             {
                 DrawText("HOST CONTROLS:", new Vector2(50, 150), 2, Color.LightGray);
                 DrawText("[2] 2 Players", new Vector2(50, 180), 1, _totalPlayersForGame == 2 ? Color.Green : Color.White);
                 DrawText("[3] 3 Players", new Vector2(50, 200), 1, _totalPlayersForGame == 3 ? Color.Green : Color.White);
                 DrawText("[4] 4 Players", new Vector2(50, 220), 1, _totalPlayersForGame == 4 ? Color.Green : Color.White);
                 
                 DrawText("Press ENTER to Start Game", new Vector2(50, 260), 2, Color.Cyan);
             }
             else
             {
                 DrawText("Waiting for Host...", new Vector2(50, 150), 2, Color.LightGray);
                 if (_localPlayerId == -1)
                     DrawText("Connecting...", new Vector2(50, 180), 2, Color.Orange);
                 else
                     DrawText($"Assigned Player {_localPlayerId + 1}", new Vector2(50, 180), 2, Color.Green);
             }
             
             _context.SpriteBatch.End();
        }

        private void DrawText(string text, Vector2 position, int scale, Color color)
        {
            _context.Font.DrawText(_context.SpriteBatch, (int)position.X, (int)position.Y, text, color, scale);
        }
    }
}
