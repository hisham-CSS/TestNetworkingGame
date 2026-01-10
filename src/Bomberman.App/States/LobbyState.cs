using System;
using System.IO;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        private KeyboardState _prevKeyboard;
        
        private Dictionary<int, bool> _playerReady = new Dictionary<int, bool>();
        private bool _amIReady = false;

        public LobbyState(GameContext context, GameStateManager manager, bool isHost, IPEndPoint? hostEndpoint)
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
            else if (hostEndpoint != null && _context.Network != null)
            {
                // Client: Connect to selected host
                _context.Network.Connect(hostEndpoint.Address.ToString(), hostEndpoint.Port);
            }
        }

        public void Enter()
        {
            Console.WriteLine($"[LobbyState] Enter. Host={_isHost}");
            _prevKeyboard = Keyboard.GetState();

            if (_context.Network != null)
            {
                _context.Network.OnJoinRequestRaw += HandleJoinRequest;
                _context.Network.OnWelcomeReceived += HandleWelcome;
                _context.Network.OnLobbyUpdateReceived += HandleLobbyUpdate;
                _context.Network.OnStartGameReceived += HandleStartGame;
                _context.Network.OnDiscoveryRequestReceived += HandleDiscoveryRequest;
                _context.Network.OnDisconnected += HandleDisconnected;
                _context.Network.OnLobbyReadyReceived += HandleLobbyReady;
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
                 _context.Network.OnDisconnected -= HandleDisconnected;
                 _context.Network.OnLobbyReadyReceived -= HandleLobbyReady;
             }
        }

        private void HandleDisconnected(IPEndPoint sender, string reason)
        {
             if (!_isHost)
             {
                 Console.WriteLine($"[Lobby] Host Disconnected: {reason}");
                 ReturnToMenu();
             }
             else
             {
                 Console.WriteLine($"[Lobby] Client {sender} Disconnected: {reason}");
                 _connectedPlayerCount--;
                 if (_connectedPlayerCount < 1) _connectedPlayerCount = 1;
                 _context.Network?.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);
             }
        }
        
        private void ReturnToMenu()
        {
             if (_context.Network != null)
            {
                _context.Network.Close();
                _context.Network = null;
            }
            _manager.ChangeState(new MenuState(_context, _manager));
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
                    bool allReady = true;
                    // Host is always ready if they press Enter? Or must toggle?
                    // Let's say Host must toggle too, OR implicitly ready by pressing Enter.
                    // For safety: Check all clients used slots.
                    // Actually, let's enforce: Everyone connected must be Ready.
                    for (int i = 0; i < _connectedPlayerCount; i++)
                    {
                        if (!_playerReady.ContainsKey(i) || !_playerReady[i]) allReady = false;
                    }

                    if (_connectedPlayerCount >= _totalPlayersForGame && allReady)
                    {
                         _context.Network?.BroadcastStartGame(_networkSeed, _totalPlayersForGame);
                         StartGame(_networkSeed, _totalPlayersForGame);
                    }
                    else
                    {
                         // Feedback?
                    }
                }
            }
            
            // Ready Toggle
            if (kState.IsKeyDown(Keys.Space) && !_prevKeyboard.IsKeyDown(Keys.Space))
            {
                _amIReady = !_amIReady;
                if (_localPlayerId != -1)
                {
                    _playerReady[_localPlayerId] = _amIReady;
                    _context.Network?.SendLobbyReady(_localPlayerId, _amIReady);
                }
            }
            _prevKeyboard = kState;
        }

        private void HandleLobbyReady(int pid, bool isReady)
        {
            _playerReady[pid] = isReady;
            if (_isHost)
            {
                // Relay to everyone so they see the status
                _context.Network?.BroadcastLobbyReady(pid, isReady);
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

                    // Sync existing ready states to new client
                    foreach(var kvp in _playerReady)
                    {
                        if (kvp.Value)
                        {
                            _context.Network.SendLobbyReadyTo(sender, kvp.Key, true);
                        }
                    }

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

             int centerX = _context.Game.GraphicsDevice.Viewport.Width / 2;
             
             DrawCenteredText(_context.SpriteBatch, "LOBBY", centerX, 50, Color.Red, 8);
             
             DrawCenteredText(_context.SpriteBatch, $"PLAYERS: {_connectedPlayerCount} / {_totalPlayersForGame}", centerX, 150, Color.White, 3);
             
             // Instructions
             if (_isHost)
             {
                 DrawCenteredText(_context.SpriteBatch, "ADJUST PLAYER COUNT:  [2]   [3]   [4]", centerX, 200, Color.Yellow, 2);
             }
             
             // Slots
             int startY = 300;
             int slotHeight = 35;
             
             for(int i=0; i<_totalPlayersForGame; i++)
             {
                 string slotInfo = $"SLOT {i+1}:   ";
                 Color c = Color.DarkGray;
                 
                 if (i < _connectedPlayerCount)
                 {
                     bool ready = _playerReady.ContainsKey(i) && _playerReady[i];
                     slotInfo += ready ? "READY" : "NOT READY";
                     c = ready ? Color.Lime : Color.Orange;
                     
                     if (i == _localPlayerId) slotInfo += "  (YOU)";
                 }
                 else
                 {
                     slotInfo += "EMPTY";
                 }
                 
                 DrawCenteredText(_context.SpriteBatch, slotInfo, centerX, startY + (i*slotHeight), c, 2);
             }

             // Footer Controls
             string statusMsg = "";
             Color statusColor = Color.Gray;

             if (_isHost)
             {
                 bool allReady = true; 
                 for (int i=0; i<_connectedPlayerCount; i++) if (!_playerReady.ContainsKey(i) || !_playerReady[i]) allReady = false;

                 if (_connectedPlayerCount >= _totalPlayersForGame && allReady)
                 {
                     statusMsg = "PRESS [ENTER] TO START GAME";
                     statusColor = Color.Green;
                 }
                 else
                 {
                     statusMsg = "WAITING FOR PLAYERS TO READY UP...";
                 }
             }
             else
             {
                 statusMsg = "WAITING FOR HOST...";
             }
             
             DrawCenteredText(_context.SpriteBatch, statusMsg, centerX, 500, statusColor, 2);

             string readyMsg = _amIReady ? "PRESS [SPACE] TO UNREADY" : "PRESS [SPACE] TO READY UP";
             DrawCenteredText(_context.SpriteBatch, readyMsg, centerX, 550, _amIReady ? Color.Cyan : Color.Magenta, 2);

             _context.SpriteBatch.End();
        }

        private void DrawCenteredText(SpriteBatch spriteBatch, string text, int x, int y, Color color, int scale)
        {
            var size = _context.Font.MeasureString(text, scale);
            _context.Font.DrawText(spriteBatch, x - size.X / 2, y - size.Y / 2, text, color, scale);
        }

        private void DrawText(string text, Vector2 position, int scale, Color color)
        {
            _context.Font.DrawText(_context.SpriteBatch, (int)position.X, (int)position.Y, text, color, scale);
        }
    }
}
