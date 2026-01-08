using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.Net;
using Bomberman.Core;
using Bomberman.Core.Game;
using Bomberman.Core.Rollback;
using Bomberman.App.Rendering; 
// Note: PixelFont is in Bomberman.App.Rendering, but Program.cs had 'using Bomberman;' which covered basic namespace?
// Actually Program.cs was namespace Bomberman.
// PixelFont.cs namespace was Bomberman (I didn't change it explicitly? I moved it).
// If I moved PixelFont.cs to src/Bomberman.App/Rendering/, I should check its namespace.
// I'll assume for now I need to fix namespaces later or add usings.

namespace Bomberman.App.GameHost
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        private Texture2D _pixelTexture;
        
        // Rollback System
        // Game Session
        private GameSession? _gameSession;
        private int _currentFrame => _gameSession != null ? _gameSession.CurrentFrame : 0;
        
        private KeyboardState _previousKeyboardState;
        
        private double _accumulator = 0.0;
        private const double FixedTimeStep = 1.0 / 60.0;
        
        private int randomSeed = 12345;

        // Networking
        private NetworkController? _networkController;
        // _isNetworked removed
        private int _localPlayerId = 0; // 0 = Host, 1 = Client

        // Rollback System (Extracted)

        // Game State
        private enum GameState { Menu, Lobby, Playing, Replaying, ServerBrowser }
        private GameState _state = GameState.Menu;
        private int _connectedPlayerCount = 1; // 1 = Self
        private int _totalPlayersForGame = 2; // Default
        private int _networkSeed = 12345;
        private int _menuSelection = 0; // 0 = Play (Local), 1 = Host, 2 = Join, 3 = Replay
        
        // Discovery
        private Dictionary<System.Net.IPEndPoint, (string name, int current, int max)> _foundServers = new Dictionary<System.Net.IPEndPoint, (string, int, int)>();
        private float _discoveryTimer = 0f;
        private int _browserSelection = 0;

        private float _joinRetryTimer = 0f;
        private bool _pendingBombInput = false;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window size to match Simulation (15x13 tiles at 32 pixels)
            _graphics.PreferredBackBufferWidth = 480;
            _graphics.PreferredBackBufferHeight = 416;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            try
            {
                var keyboardState = Keyboard.GetState();
                
                // Network Update
                if (_networkController != null) _networkController.Update();

                switch (_state)
                {
                    case GameState.Menu:
                        UpdateMenu(keyboardState);
                        break;
                    case GameState.Lobby:
                        UpdateLobby(gameTime, keyboardState);
                        break;
                    case GameState.Playing:
                    case GameState.Replaying:
                        UpdateGame(gameTime, keyboardState);
                        break;
                    case GameState.ServerBrowser:
                        UpdateServerBrowser(gameTime, keyboardState);
                        break;
                }

                _previousKeyboardState = keyboardState;
                base.Update(gameTime);
            }
            catch(Exception e)
            {
                 Console.WriteLine("Update Crash: " + e.ToString());
                 throw;
            }
        }

        private void UpdateMenu(KeyboardState keyboardState)
        {
             if ((keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) && 
                !(_previousKeyboardState.IsKeyDown(Keys.W) || _previousKeyboardState.IsKeyDown(Keys.Up)))
            {
                _menuSelection--;
                if (_menuSelection < 0) _menuSelection = 3;
            }
            if ((keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) && 
                !(_previousKeyboardState.IsKeyDown(Keys.S) || _previousKeyboardState.IsKeyDown(Keys.Down)))
            {
                _menuSelection++;
                if (_menuSelection > 3) _menuSelection = 0;
            }

            if (keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space) ||
                keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                if (_menuSelection == 0) // Local Play
                {
                        _state = GameState.Playing;
                        _localPlayerId = 0;
                        
                        _gameSession = new GameSession(_localPlayerId, 1, randomSeed);
                        
                        string logFile = $"debug_log_player_{_localPlayerId}.txt";
                        File.WriteAllText(logFile, "--- Local Play Start ---\n");
                        if (_gameSession.Simulation != null)
                        {
                            _gameSession.Simulation.Log = (msg) => {
                                string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Frame {_currentFrame}] {msg}\n";
                                File.AppendAllText(logFile, line);
                                Console.Write(line);
                            };
                        }
                }
                else if (_menuSelection == 1) // Host
                {
                     _state = GameState.Lobby;
                     // _isNetworked = true; // Implied by controller existence
                     _localPlayerId = 0;
                    // _currentFrame reset handled by RollbackSystem later
                    _networkSeed = new Random().Next();
                    _connectedPlayerCount = 1;
                    _totalPlayersForGame = 2; // Default 2P 

                    _networkController = null;
                    for(int port = 5000; port < 5010; port++)
                    {
                        try 
                        {
                            _networkController = new NetworkController(port);
                            HookNetworkEvents();
                            Console.WriteLine($"Hosting on Port {port}...");
                            break; 
                        }
                        catch(System.Net.Sockets.SocketException)
                        {
                            Console.WriteLine($"Port {port} busy, trying next...");
                            _networkController = null;
                        }
                    }

                    if (_networkController == null)
                    {
                         Console.WriteLine("Failed to bind any port (5000-5009)!");
                         _state = GameState.Menu; // Abort
                    }
                }
                else if (_menuSelection == 2) // Join -> Server Browser
                {
                    _state = GameState.ServerBrowser;
                    // _isNetworked = true;
                    _foundServers.Clear();
                    _discoveryTimer = 0f;
                    _browserSelection = 0;
                    
                    // Start network manager immediately for broadcast
                    if (_networkController == null)
                    {
                        _networkController = new NetworkController(0); // Client on Ephemeral
                        HookNetworkEvents();
                    }

                    Console.WriteLine("Entered Server Browser...");
                }
                else if (_menuSelection == 3) // Replay
                {
                    _state = GameState.Replaying;
                    // _isNetworked = false;
                    
                    _gameSession = new GameSession(Path.Combine("Replays", "replay.json"));
                }
            }
        }

        private void UpdateLobby(GameTime gameTime, KeyboardState keyboardState)
        {
             if (keyboardState.IsKeyDown(Keys.Escape))
            {
                _state = GameState.Menu;
                _networkController?.Close();
                _networkController = null;
            }

            // Client: Retry Join Request
            if (_localPlayerId == -1 && _networkController != null)
            {
                _joinRetryTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_joinRetryTimer <= 0)
                {
                    _networkController.SendJoinRequest();
                        Console.WriteLine("Resending Join Request...");
                        _joinRetryTimer = 1.0f;
                }
            }

            if (_localPlayerId == 0) // HOST
            {
                    // Configure Players
                    int prevPlayers = _totalPlayersForGame;
                    if (keyboardState.IsKeyDown(Keys.D2)) _totalPlayersForGame = 2;
                    if (keyboardState.IsKeyDown(Keys.D3)) _totalPlayersForGame = 3;
                    if (keyboardState.IsKeyDown(Keys.D4)) _totalPlayersForGame = 4;
                    
                    if (prevPlayers != _totalPlayersForGame)
                    {
                        // Broadcast Update
                        _networkController?.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);
                    }

                    // Start Game
                    if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                    {
                        // Check if we have enough connected players match the required count
                        if (_connectedPlayerCount >= _totalPlayersForGame) 
                        {
                            _networkController?.BroadcastStartGame(_networkSeed, _totalPlayersForGame);
                            _state = GameState.Playing;
                            _gameSession = new GameSession(_localPlayerId, _totalPlayersForGame, _networkSeed);
                            _gameSession.RollbackSystem.SimulateNetworked = true;
                            
                            string logFile = $"debug_log_player_{_localPlayerId}.txt";
                             File.WriteAllText(logFile, "--- Host Start ---\n");
                             if (_gameSession.Simulation != null)
                             {
                                _gameSession.Simulation.Log = (msg) => {
                                    string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Frame {_currentFrame}] {msg}\n";
                                    File.AppendAllText(logFile, line);
                                    Console.Write(line);
                                };
                             }
                        }
                        else
                        {
                            Console.WriteLine($"Cannot start: {_connectedPlayerCount}/{_totalPlayersForGame} players ready.");
                        }
                    }
            }
        }

        private void UpdateServerBrowser(GameTime gameTime, KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                 _state = GameState.Menu;
                 _networkController?.Close();
                 _networkController = null;
                 return;
            }

            // Periodic Broadcast
            _discoveryTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_discoveryTimer <= 0)
            {
                // Broadcast to ALL possible host ports
                _networkController?.BroadcastDiscoveryRequest(5000, 5010);
                _discoveryTimer = 2.0f; // Retry every 2s
            }

            // Navigation
            if ((keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) && 
               !(_previousKeyboardState.IsKeyDown(Keys.W) || _previousKeyboardState.IsKeyDown(Keys.Up)))
            {
               _browserSelection--;
               if (_browserSelection < 0) _browserSelection = _foundServers.Count - 1; 
               if (_browserSelection < 0) _browserSelection = 0;
            }
            if ((keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) && 
               !(_previousKeyboardState.IsKeyDown(Keys.S) || _previousKeyboardState.IsKeyDown(Keys.Down)))
            {
               _browserSelection++;
               if (_browserSelection >= _foundServers.Count) _browserSelection = 0;
            }

            // Selection
            if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                if (_foundServers.Count > 0 && _browserSelection < _foundServers.Count)
                {
                    // Connect!
                    var endpoint = new List<System.Net.IPEndPoint>(_foundServers.Keys)[_browserSelection];
                    _networkController?.Connect(endpoint.Address.ToString(), endpoint.Port);
                    
                    _state = GameState.Lobby;
                    _localPlayerId = -1;
                    // _currentFrame = 0; // Not needed
                    // _remoteInputBuffer.Clear(); // Not needed
                    
                    // Send Join
                    _networkController?.SendJoinRequest();
                    Console.WriteLine($"Joining {endpoint}...");
                    _joinRetryTimer = 1.0f;
                }
            }
        }
        private void UpdateGame(GameTime gameTime, KeyboardState keyboardState)
        {
             if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                _state = GameState.Menu;
                if (_networkController != null) { _networkController.Close(); _networkController = null; }
                if (_gameSession != null) _gameSession.SaveReplay(Path.Combine("Replays", "replay.json"));
            }

            // Fixed Update Loop
            _accumulator += gameTime.ElapsedGameTime.TotalSeconds;

            // Input Latching (Capture "Just Pressed" events that happen between steps)
            if (keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space))
            {
                _pendingBombInput = true;
            }

            while (_accumulator >= FixedTimeStep)
            {
                StepSimulation(keyboardState); // Extracted method logic
                _accumulator -= FixedTimeStep;
            }
        }

        private void HandleJoinRequest(System.Net.IPEndPoint sender)
        {
             if (_localPlayerId == 0 && _state == GameState.Lobby && _networkController != null) // Only Host handles this
            {
                 bool alreadyConnected = false;
                 foreach(var c in _networkController.ConnectedClients)
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
                        _networkController.AddClient(sender);
                        int newId = _connectedPlayerCount;
                        _connectedPlayerCount++;
                        
                        // Send Welcome
                        _networkController.SendWelcome(sender, newId, _networkSeed, _totalPlayersForGame);

                        // Broadcast Lobby Update to everyone
                        _networkController.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);

                        Console.WriteLine($"Client {newId} Joined from {sender}");
                    }
                 }
            }
        }

        private void HandleWelcome(int assignedId, int seed, int totalPlayers)
        {
             if (_localPlayerId == -1) // Client waiting for welcome
            {
                _localPlayerId = assignedId;
                _networkSeed = seed;
                _totalPlayersForGame = totalPlayers;
                Console.WriteLine($"Joined as Player {_localPlayerId}. seed={_networkSeed}");
            }
        }

        private void HandleLobbyUpdate(int connectedCount, int totalPlayers)
        {
            if (_state == GameState.Lobby)
            {
                _connectedPlayerCount = connectedCount;
                _totalPlayersForGame = totalPlayers;
            }
        }

        private void HandleStartGame(int seed, int totalPlayers)
        {
             if (_state == GameState.Lobby)
            {
                _networkSeed = seed;
                _totalPlayersForGame = totalPlayers;
                
                _state = GameState.Playing;
                _gameSession = new GameSession(_localPlayerId, _totalPlayersForGame, _networkSeed);
                _gameSession.RollbackSystem.SimulateNetworked = true;
                
                string logFile = $"debug_log_player_{_localPlayerId}.txt";
                File.WriteAllText(logFile, "--- Client Start ---\n");
                if (_gameSession.Simulation != null)
                {
                    _gameSession.Simulation.Log = (msg) => {
                            string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Frame {_currentFrame}] {msg}\n";
                            File.AppendAllText(logFile, line);
                            Console.Write(line);
                    };
                }
                    Console.WriteLine($"Game Started! Seed={_networkSeed}, Players={_totalPlayersForGame}");
            }
        }

        private void HandleInputReceived(int pid, int startFrame, InputState[] inputs, IntVector2 remotePos, int remoteHash)
        {
             if (_state == GameState.Playing && _gameSession != null)
             {
                _gameSession.HandleRemoteInput(pid, startFrame, inputs, remotePos, remoteHash);

                // Host Relay Logic (Relay the RAW packet to others)
                if (_localPlayerId == 0 && _networkController != null)
                {
                    if (pid != 0) 
                    {
                        // Loop through all other clients and send Unicast
                        byte[] relayedPacket = NetworkProtocol.CreateInputPacket(pid, startFrame, inputs, remotePos, remoteHash);
                        foreach(var client in _networkController.ConnectedClients)
                        {
                            _networkController.RelayPacket(client, relayedPacket);
                        }
                    }
                }
             }
        }

        private void HandleDiscoveryRequest(System.Net.IPEndPoint sender, string header, int cur, int max)
        {
            if (_localPlayerId == 0 && (_state == GameState.Lobby || _state == GameState.Playing) && _networkController != null)
            {
                // I am host, reply
                _networkController.SendDiscoveryResponse(sender, "Local Game", _connectedPlayerCount, _totalPlayersForGame);
            }
        }

        private void HandleDiscoveryResponse(System.Net.IPEndPoint sender, string name, int cur, int max)
        {
            if (_state == GameState.ServerBrowser)
            {
                _foundServers[sender] = (name, cur, max);
            }
        }

        private void HookNetworkEvents()
        {
            if (_networkController == null) return;
            _networkController.OnJoinRequestRaw += HandleJoinRequest;
            _networkController.OnWelcomeReceived += HandleWelcome;
            _networkController.OnLobbyUpdateReceived += HandleLobbyUpdate;
            _networkController.OnStartGameReceived += HandleStartGame;
            _networkController.OnInputReceived += HandleInputReceived;
            _networkController.OnDiscoveryRequestReceived += HandleDiscoveryRequest;
            _networkController.OnDiscoveryResponseReceived += HandleDiscoveryResponse;
        }

        private void StepSimulation(KeyboardState keyboardState)
        {
            if (_gameSession == null) return;

             // Capture Local Input
             // Capture Local Input
            IntVector2 movement = new IntVector2(0, 0);
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) movement.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) movement.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) movement.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) movement.X += 1;

            // No normalization needed for IntVector2 -1/0/1 input

            // Latch Bomb Input
            bool placeBomb = _pendingBombInput;
            _pendingBombInput = false; // Reset Latch after consumption
            
             // Calculate explicit bomb target based on current local position
             Point bombTarget = new Point(0, 0);
             if (placeBomb)
             {
                 IntVector2 myPos = new IntVector2(0,0);
                 if (_gameSession.Simulation != null)
                 {
                     var pPool = _gameSession.Simulation.World.Players;
                     for(int i=0; i<pPool.Count; i++)
                     {
                         if (pPool.Get(i).PlayerId == _localPlayerId)
                         {
                             var e = pPool.GetEntity(i);
                             if (_gameSession.Simulation.World.Transforms.Has(e))
                                myPos = _gameSession.Simulation.World.Transforms.Get(e).Position;
                             break;
                         }
                     }
                 }
                 
                 // Scale down to pixels first, then grid
                 int pixelX = myPos.X / Simulation.SubpixelScale;
                 int pixelY = myPos.Y / Simulation.SubpixelScale;
                 
                 int centerX = pixelX + 12;
                 int centerY = pixelY + 12;
                 bombTarget = new Point(centerX / 32, centerY / 32);
             }

            InputState localInput = new InputState { Movement = movement, PlaceBomb = placeBomb, BombTarget = bombTarget };
            
            _gameSession.Update(localInput);

            if (_networkController != null && _gameSession.TryBuildOutgoingBundle(out var bundle))
            {
                _networkController.SendInput(bundle);
            }
        }



        protected override void Draw(GameTime gameTime)
        {
            try
            {
                GraphicsDevice.Clear(Color.CornflowerBlue); // Background
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        
                switch (_state)
                {
                    case GameState.Menu:
                        DrawMenu();
                        break;
                    case GameState.Lobby:
                        DrawLobby();
                        break;

                    case GameState.ServerBrowser:
                        DrawServerBrowser();
                        break;
                    default:
                        DrawGame();
                        break;
                }

                _spriteBatch.End();
                base.Draw(gameTime);
            }
            catch(Exception e)
            {
                 Console.WriteLine("Draw Crash: " + e.ToString());
                 throw;
            }
        }

        private void DrawMenu()
        {
            int btnWidth = 200;
            int btnHeight = 40;
            int centerX = _graphics.PreferredBackBufferWidth / 2 - btnWidth / 2;
            int startY = 80;
            int spacing = 50;

            // 0: Play
            DrawMenuButton(0, "PLAY", centerX, startY, btnWidth, btnHeight, Color.Green, Color.Lime);
            
            // 1: Host
            DrawMenuButton(1, "HOST", centerX, startY + spacing, btnWidth, btnHeight, Color.Purple, Color.Magenta);

            // 2: Join
            DrawMenuButton(2, "JOIN", centerX, startY + spacing * 2, btnWidth, btnHeight, Color.Goldenrod, Color.Yellow);

            // 3: Replay
            DrawMenuButton(3, "REPLAY", centerX, startY + spacing * 3, btnWidth, btnHeight, Color.Blue, Color.Cyan);
        }

        private void DrawLobby()
        {
             int scale = 3;
             DrawText("LOBBY", new Vector2(50, 50), scale, Color.White);
             
             DrawText($"Players: {_connectedPlayerCount} / {_totalPlayersForGame}", new Vector2(50, 100), 2, Color.Yellow);
             
             if (_localPlayerId == 0) // HOST
             {
                 DrawText("HOST CONTROLS:", new Vector2(50, 150), 2, Color.LightGray);
                 DrawText("[2] 2 Players", new Vector2(50, 180), 1, _totalPlayersForGame == 2 ? Color.Green : Color.White);
                 DrawText("[3] 3 Players", new Vector2(50, 200), 1, _totalPlayersForGame == 3 ? Color.Green : Color.White);
                 DrawText("[4] 4 Players", new Vector2(50, 220), 1, _totalPlayersForGame == 4 ? Color.Green : Color.White);
                 
                 DrawText("Press ENTER to Start Game", new Vector2(50, 260), 2, Color.Cyan);
             }
             else // CLIENT
             {
                 DrawText("Waiting for Host...", new Vector2(50, 150), 2, Color.LightGray);
                 
                 if (_localPlayerId == -1)
                     DrawText("Connecting...", new Vector2(50, 180), 2, Color.Orange);
                 else
                     DrawText($"Assigned Player {_localPlayerId + 1}", new Vector2(50, 180), 2, Color.Green);
             }
        }

        private void DrawServerBrowser()
        {
             DrawText("SERVER BROWSER", new Vector2(50, 50), 3, Color.White);
             DrawText("Scanning...", new Vector2(50, 90), 1, Color.Gray);

             int startY = 130;
             int index = 0;
             if (_foundServers.Count == 0)
             {
                 DrawText("No servers found.", new Vector2(50, startY), 2, Color.Red);
             }
             foreach(var kvp in _foundServers) // Dictionary enumeration order is undefined but stable enough for simple UI usually
             {
                 var ep = kvp.Key;
                 var info = kvp.Value;
                 bool selected = index == _browserSelection;
                 
                 string line = $"{info.name} - {info.current}/{info.max} ({ep.Address})";
                 DrawText(line, new Vector2(50, startY + index * 40), 2, selected ? Color.Yellow : Color.White);
                 
                 if (selected) DrawText(">", new Vector2(20, startY + index * 40), 2, Color.Yellow);
                 
                 index++;
             }
             
             DrawText("Press ENTER to Join", new Vector2(50, 380), 2, Color.Green);
        }

        private void DrawGame()
        {
            if (_gameSession?.Simulation == null) return;
            var world = _gameSession.Simulation.World;
            var transformEntities = world.Transforms.GetEntities();
            var transforms = world.Transforms.GetAll();

            // 1. Draw Grid/Floor
            var tiles = world.Tiles.GetAll();
            var tileEntities = world.Tiles.GetEntities();
            for(int i=0; i<tiles.Count; i++)
            {
                var entity = tileEntities[i]; 
                TransformComponent transform = FindTransform(entity, transformEntities, transforms);

                // Draw Floor for EVERYTHING (or just Empty/Destructible)
                Vector2 pos = new Vector2(transform.Position.X, transform.Position.Y) / (float)Simulation.SubpixelScale;
                Vector2 size = new Vector2(transform.Size.X, transform.Size.Y) / (float)Simulation.SubpixelScale;

                DrawRectangle(pos + new Vector2(1,1), size - new Vector2(2,2), Color.Gray);

                if (tiles[i].Type == TileComponent.TileType.Solid) 
                {
                    DrawRectangle(pos + new Vector2(1,1), size - new Vector2(2,2), Color.DarkGray);
                }
                else if (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed) 
                {
                    DrawRectangle(pos + new Vector2(1,1), size - new Vector2(2,2), Color.SaddleBrown);
                }
            }

            // 2. Draw Bombs
            var bombs = world.Bombs.GetAll();
            var bombEntities = world.Bombs.GetEntities();
            for(int i=0; i<bombs.Count; i++)
            {
                 if (i >= bombEntities.Count) break;
                 var entity = bombEntities[i];
                 TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                 
                 // Pulse red based on timer
                 float pulse = (bombs[i].Timer % 20) / 20f;
                 Color bColor = Color.Lerp(Color.Red, Color.DarkRed, pulse);

                 Vector2 pos = new Vector2(transform.Position.X, transform.Position.Y) / (float)Simulation.SubpixelScale;
                 Vector2 size = new Vector2(transform.Size.X, transform.Size.Y) / (float)Simulation.SubpixelScale;

                 DrawRectangle(pos + new Vector2(4, 4), size - new Vector2(8,8), bColor);
            }

            // 3. Draw Powerups
            var powerups = world.Powerups.GetAll();
            var powerupEntities = world.Powerups.GetEntities();
            for(int i=0; i<powerups.Count; i++)
            {
                 if (i >= powerups.Count) break;
                 var entity = powerupEntities[i];
                 TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                 
                 // Pulse red based on timer (wait, powerup logic?)
                 Color pColor = Color.White;
                 if (powerups[i].Type == PowerupComponent.PowerupType.Range) pColor = Color.Yellow;
                 if (powerups[i].Type == PowerupComponent.PowerupType.Capacity) pColor = Color.Black;
                 
                 Vector2 pos = new Vector2(transform.Position.X, transform.Position.Y) / (float)Simulation.SubpixelScale;
                 Vector2 size = new Vector2(transform.Size.X, transform.Size.Y) / (float)Simulation.SubpixelScale;

                 DrawRectangle(pos, size, pColor);
            }

            // 4. Draw Explosions
            var expList = world.Explosions.GetAll();
            var expEntities = world.Explosions.GetEntities();
            for(int i=0; i<expList.Count; i++)
            {
                if (i >= expEntities.Count) break;
                var entity = expEntities[i];
                TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                
                Vector2 pos = new Vector2(transform.Position.X, transform.Position.Y) / (float)Simulation.SubpixelScale;
                Vector2 size = new Vector2(transform.Size.X, transform.Size.Y) / (float)Simulation.SubpixelScale;

                DrawRectangle(pos, size, Color.OrangeRed);
            }

            // 5. Draw Players
            var players = world.Players.GetAll();
            var playerEntities = world.Players.GetEntities();
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Alive) continue; 
                var entity = playerEntities[i];
                TransformComponent transform = FindTransform(entity, transformEntities, transforms); 

                Vector2 pos = new Vector2(transform.Position.X, transform.Position.Y) / (float)Simulation.SubpixelScale;
                Vector2 size = new Vector2(transform.Size.X, transform.Size.Y) / (float)Simulation.SubpixelScale;

                Color[] playerColors = new Color[] { Color.White, Color.Blue, Color.Red, Color.Green };
                Color pColor = playerColors[i % playerColors.Length];
                DrawRectangle(pos, size, pColor);
                
                // Eyes
                Vector2 eyeOffset = new Vector2(4, 6);
                DrawRectangle(pos + eyeOffset, new Vector2(4, 6), Color.Black);
                DrawRectangle(pos + new Vector2(size.X - eyeOffset.X - 4, eyeOffset.Y), new Vector2(4, 6), Color.Black);
            }
        }

        private TransformComponent FindTransform(Entity entity, List<Entity> transformEntities, List<TransformComponent> transforms)
        {
            for(int i=0; i<transformEntities.Count; i++)
            {
                if(transformEntities[i].Equals(entity))
                    return transforms[i];
            }
            return new TransformComponent();
        }

        private void DrawMenuButton(int index, string text, int x, int y, int width, int height, Color normalColor, Color selectedColor)
        {
            Color color = _menuSelection == index ? selectedColor : normalColor;
            DrawRectangle(new Vector2(x, y), new Vector2(width, height), color);
            
            // Text
            int scale = 3;
            int textWidth = text.Length * (5 * scale + scale);
            DrawText(text, new Vector2(x + width/2 - textWidth/2, y + 10), scale, Color.White);

            // Selection Border
            if (_menuSelection == index) DrawHollowRect(new Rectangle(x-2, y-2, width+4, height+4), Color.White);
        }


        private void DrawText(string text, Vector2 position, int scale, Color color)
        {
            int spacing = 1 * scale;
            int charWidth = 5 * scale;
            
            for(int i=0; i<text.Length; i++)
            {
                bool[,] bitmap = PixelFont.GetBitmap(text[i]);
                Vector2 charPos = position + new Vector2(i * (charWidth + spacing), 0);
                
                for(int x=0; x<5; x++)
                {
                    for(int y=0; y<5; y++)
                    {
                        if (bitmap[x,y])
                        {
                            DrawRectangle(charPos + new Vector2(x * scale, y * scale), new Vector2(scale, scale), color);
                        }
                    }
                }
            }
        }

        private void DrawHollowRect(Rectangle rect, Color color)
        {
            int t = 2; 
            DrawRectangle(new Vector2(rect.X, rect.Y), new Vector2(rect.Width, t), color); 
            DrawRectangle(new Vector2(rect.X, rect.Bottom - t), new Vector2(rect.Width, t), color); 
            DrawRectangle(new Vector2(rect.X, rect.Y), new Vector2(t, rect.Height), color); 
            DrawRectangle(new Vector2(rect.Right - t, rect.Y), new Vector2(t, rect.Height), color); 
        }

        private void DrawRectangle(Vector2 position, Vector2 size, Color color)
        {
             _spriteBatch.Draw(_pixelTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
        }

    }
}
