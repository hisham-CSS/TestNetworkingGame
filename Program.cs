using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Bomberman
{
    public class Game1 : Game
    {
        private Texture2D? _pixelTexture;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch? _spriteBatch;
        
        private Simulation? _simulation;
        private KeyboardState _previousKeyboardState;
        
        private double _accumulator = 0.0;
        private const double FixedTimeStep = 1.0 / 60.0;

        // Replay System
        private InputRecorder _recorder = new InputRecorder();
        private bool _isRecording = false;
        private bool _isReplaying = false;
        private int _replayFrame = 0;
        private int randomSeed = 12345;

        // Networking
        private NetworkManager? _networkManager;
        private bool _isNetworked = false;
        private int _localPlayerId = 0; // 0 = Host, 1 = Client
        private Dictionary<int, Dictionary<int, InputState>> _remoteInputBuffer = new Dictionary<int, Dictionary<int, InputState>>(); // Frame -> PlayerId -> Input
        private Dictionary<int, InputState> _localInputBuffer = new Dictionary<int, InputState>(); // Frame -> LocalInput
        private int _currentFrame = 0;

        // Game State
        private enum GameState { Menu, Lobby, Playing, Replaying }
        private GameState _state = GameState.Menu;
        private int _connectedPlayerCount = 1; // 1 = Self
        private int _totalPlayersForGame = 2; // Default
        private int _networkSeed = 12345;
        private int _menuSelection = 0; // 0 = Play (Local), 1 = Host, 2 = Join, 3 = Replay

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
                if (_networkManager != null) _networkManager.Update();

                if (_state == GameState.Menu)
                {
                    // Menu Logic
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
                             _isRecording = true;
                             _isReplaying = false;
                             _isNetworked = false;
                             _localPlayerId = 0;
                             _recorder.Reset();
                             _simulation = new Simulation(randomSeed, 1);
                        }
                        else if (_menuSelection == 1) // Host
                        {
                            _state = GameState.Lobby;
                            _isRecording = false; 
                            _isReplaying = false;
                            _isNetworked = true;
                            _localPlayerId = 0;
                            _currentFrame = 0;
                            _remoteInputBuffer.Clear();
                            _networkSeed = new Random().Next();
                            _connectedPlayerCount = 1;
                            _totalPlayersForGame = 2; // Default 2P

                            _networkManager = new NetworkManager(5000); // Host on 5000
                            _networkManager.OnPacketReceived += OnNetworkPacket;
                            
                            Console.WriteLine("Hosting on Port 5000...");
                        }
                        else if (_menuSelection == 2) // Join
                        {
                            _state = GameState.Lobby;
                            _isRecording = false;
                            _isReplaying = false;
                            _isNetworked = true;
                            _localPlayerId = -1; // Unknown yet
                            _currentFrame = 0;
                            _remoteInputBuffer.Clear();

                            _networkManager = new NetworkManager(0); // Client on Ephemeral Port
                            _networkManager.Connect("127.0.0.1", 5000); 
                            _networkManager.OnPacketReceived += OnNetworkPacket;

                            // Send Join Request (Initial)
                            _networkManager.Send(NetworkProtocol.CreateJoinRequest());
                             Console.WriteLine("Sent Join Request...");
                             _joinRetryTimer = 1.0f; // Seconds
                        }
                        else if (_menuSelection == 3) // Replay
                        {
                            _state = GameState.Replaying;
                            _isRecording = false;
                            _isReplaying = true;
                            _isNetworked = false;
                            _replayFrame = 0;
                            _recorder.Load(Path.Combine("Replays", "replay.json"));
                            _simulation = new Simulation(randomSeed, 2); // Assume 2P replay for now
                        }
                    }
                }
                else if (_state == GameState.Lobby)
                {
                    if (keyboardState.IsKeyDown(Keys.Escape))
                    {
                        _state = GameState.Menu;
                        _networkManager?.Close();
                        _networkManager = null;
                    }

                    // Client: Retry Join Request
                    if (_localPlayerId == -1 && _networkManager != null)
                    {
                        _joinRetryTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                        if (_joinRetryTimer <= 0)
                        {
                            _networkManager.Send(NetworkProtocol.CreateJoinRequest());
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
                             byte[] update = NetworkProtocol.CreateLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);
                             _networkManager.Broadcast(update);
                         }

                         // Start Game
                         if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                         {
                             // Check if we have enough connected players match the required count
                             if (_connectedPlayerCount >= _totalPlayersForGame) 
                             {
                                 _networkManager.Broadcast(NetworkProtocol.CreateStartGame(_networkSeed, _totalPlayersForGame));
                                 _state = GameState.Playing;
                                 _simulation = new Simulation(_networkSeed, _totalPlayersForGame);
                             }
                             else
                             {
                                 Console.WriteLine($"Cannot start: {_connectedPlayerCount}/{_totalPlayersForGame} players ready.");
                             }
                         }
                    }
                }
                else if (_state == GameState.Playing || _state == GameState.Replaying)
                {
                    if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
                    {
                        _state = GameState.Menu;
                        if (_networkManager != null) { _networkManager.Close(); _networkManager = null; }
                        if (_isRecording) _recorder.Save(Path.Combine("Replays", "replay.json"));
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

                _previousKeyboardState = keyboardState;
                base.Update(gameTime);
            }
            catch(Exception e)
            {
                 Console.WriteLine("Update Crash: " + e.ToString());
                 throw;
            }
        }

        private void OnNetworkPacket(byte[] data, System.Net.IPEndPoint sender)
        {
            PacketType type = NetworkProtocol.ReadType(data);

            switch (type)
            {
                case PacketType.JoinRequest:
                    if (_localPlayerId == 0 && _state == GameState.Lobby) // Only Host handles this
                    {
                        if (_connectedPlayerCount < _totalPlayersForGame)
                        {
                            _networkManager.AddClient(sender);
                            int newId = _connectedPlayerCount;
                            _connectedPlayerCount++;
                            
                            // Send Welcome
                            byte[] welcome = NetworkProtocol.CreateWelcome(newId, _networkSeed, _totalPlayersForGame);
                            _networkManager.SendTo(welcome, sender);

                            // Broadcast Lobby Update to everyone
                            byte[] update = NetworkProtocol.CreateLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);
                            _networkManager.Broadcast(update);

                            Console.WriteLine($"Client {newId} Joined from {sender}");
                        }
                    }
                    break;
                
                case PacketType.Welcome:
                    if (_localPlayerId == -1) // Client waiting for welcome
                    {
                        var info = NetworkProtocol.ReadWelcome(data);
                        _localPlayerId = info.playerId;
                        _networkSeed = info.seed;
                        _totalPlayersForGame = info.playerCount;
                        Console.WriteLine($"Joined as Player {_localPlayerId}. seed={_networkSeed}");
                    }
                    break;

                case PacketType.LobbyUpdate:
                    if (_state == GameState.Lobby)
                    {
                        var info = NetworkProtocol.ReadLobbyUpdate(data);
                        _connectedPlayerCount = info.currentCount;
                        _totalPlayersForGame = info.totalRequired;
                    }
                    break;

                case PacketType.StartGame:
                    if (_state == GameState.Lobby)
                    {
                        var info = NetworkProtocol.ReadStartGame(data);
                        _networkSeed = info.seed;
                        _totalPlayersForGame = info.playerCount;
                        
                        _state = GameState.Playing;
                        _simulation = new Simulation(_networkSeed, _totalPlayersForGame);
                         Console.WriteLine($"Game Started! Seed={_networkSeed}, Players={_totalPlayersForGame}");
                    }
                    break;

                case PacketType.Input:
                     if (_state == GameState.Playing)
                     {
                        var (pid, frame, input) = NetworkProtocol.ReadInputPacket(data);
                        
                        // Buffer logic: Frame -> Player -> Input
                        if (!_remoteInputBuffer.ContainsKey(frame))
                        {
                            _remoteInputBuffer[frame] = new Dictionary<int, InputState>();
                        }
                        
                        if (!_remoteInputBuffer[frame].ContainsKey(pid))
                        {
                            _remoteInputBuffer[frame][pid] = input;
                        }

                        // Host Relay Logic
                        if (_localPlayerId == 0)
                        {
                            // If we received this from a client, we must broadcast it to everyone else
                            // data is the exact packet bytes we received. 
                            // Verify it's not from us? (Host doesn't receive via OnNetworkPacket from itself usually, but check just in case)
                            if (pid != 0) 
                            {
                                _networkManager.Broadcast(data);
                            }
                        }
                     }
                    break;
            }
        }

        private void StepSimulation(KeyboardState keyboardState)
        {
            InputState[] inputs;

            if (_isReplaying)
            {
                    inputs = _recorder.GetFrame(_replayFrame);
                    if (inputs == null || inputs.Length == 0) inputs = new InputState[1];
                    if (inputs == null || inputs.Length == 0) inputs = new InputState[1];
                    if (_simulation != null) _simulation.Update(inputs, (float)FixedTimeStep);
                    _replayFrame++;
                    return;
            }

            // Capture Local Input
            Vector2 movement = Vector2.Zero;
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) movement.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) movement.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) movement.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) movement.X += 1;

            if (movement != Vector2.Zero) movement.Normalize();

            if (movement != Vector2.Zero) movement.Normalize();

            // Check if we already decided input for this frame?
            InputState localInput;
            if (_localInputBuffer.ContainsKey(_currentFrame))
            {
                localInput = _localInputBuffer[_currentFrame];
            }
            else
            {
                // New Frame: Consume Latch
                bool placeBomb = _pendingBombInput;
                _pendingBombInput = false; // Reset Latch after consumption
                localInput = new InputState { Movement = movement, PlaceBomb = placeBomb };
                _localInputBuffer[_currentFrame] = localInput;
            }

            if (_isNetworked)
            {
                // Lockstep Logic
                
                // 1. Send Local Input for THIS frame
                byte[] packet = NetworkProtocol.CreateInputPacket(_localPlayerId, _currentFrame, localInput);
                if (_networkManager != null) 
                {
                    if (_localPlayerId == 0) 
                    {
                        _networkManager.Broadcast(packet); // Host sends to all
                        // Host also needs to treat this as received input? 
                        // Actually StepSimulation uses 'localInput' directly for the local player.
                        // So we don't need to loopback locally for execution, ONLY for relay (handled in OnNetworkPacket if we did that, but we just Broadcast here).
                    }
                    else 
                    {
                        _networkManager.Send(packet); // Client sends to Host
                    }
                }

                // 2. Do we have Remote Input for THIS frame from ALL other players?
                bool haveAllInputs = true;
                if (!_remoteInputBuffer.ContainsKey(_currentFrame)) 
                {
                    haveAllInputs = false;
                }
                else
                {
                    var frameInputs = _remoteInputBuffer[_currentFrame];
                    for (int i = 0; i < _totalPlayersForGame; i++)
                    {
                        if (i == _localPlayerId) continue; // Don't need remote input for self
                        if (!frameInputs.ContainsKey(i))
                        {
                            haveAllInputs = false;
                            break;
                        }
                    }
                }

                if (haveAllInputs)
                {
                    // YES! We have everyone.
                    var frameInputs = _remoteInputBuffer.ContainsKey(_currentFrame) ? _remoteInputBuffer[_currentFrame] : new Dictionary<int, InputState>();
                    
                    // Construct Full Input Array
                    inputs = new InputState[_totalPlayersForGame];
                    inputs[_localPlayerId] = localInput;
                    
                    for (int i = 0; i < _totalPlayersForGame; i++)
                    {
                        if (i == _localPlayerId) continue;
                        if (frameInputs.ContainsKey(i)) 
                            inputs[i] = frameInputs[i];
                        else 
                            inputs[i] = new InputState(); // Should not happen given check above
                    }

                    // Advance
                    if (_simulation != null) _simulation.Update(inputs, (float)FixedTimeStep);
                    _currentFrame++;
                    // Remove old input to save memory
                    _remoteInputBuffer.Remove(_currentFrame - 100); 
                    _localInputBuffer.Remove(_currentFrame - 100);
                }
                else
                {
                    // NO. STALL.
                    // Console.WriteLine($"Stalling Frame {_currentFrame}");
                }
            }
            else
            {
                // Local Single Player
                inputs = new InputState[] { localInput };
                if (_isRecording) _recorder.RecordFrame(inputs);
                if (_simulation != null) _simulation.Update(inputs, (float)FixedTimeStep);
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            try
            {
                // Console.WriteLine("Draw Start");
                GraphicsDevice.Clear(Color.CornflowerBlue); // Background
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        
                if (_state == GameState.Menu)
                {
                    // Draw Menu
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
                else if (_state == GameState.Lobby)
                {
                     int scale = 3;
                     DrawText("LOBBY", new Vector2(50, 50), scale, Color.White);
                     
                     if (_localPlayerId == 0)
                     {
                         DrawText($"HOSTING: {_connectedPlayerCount}/{_totalPlayersForGame} Players", new Vector2(50, 100), 2, Color.Yellow);
                         DrawText("Press 2,3,4 to set Count", new Vector2(50, 140), 1, Color.White);
                         DrawText("Press ENTER to Start", new Vector2(50, 180), 2, Color.Green);
                     }
                     else
                     {
                         if (_localPlayerId == -1) DrawText("Connecting...", new Vector2(50, 100), 2, Color.Yellow);
                         else DrawText($"WAITING FOR HOST... (P{_localPlayerId})", new Vector2(50, 100), 2, Color.Yellow);
                     }
                }
                else
                {
                    // Draw Simulation
                    if (_simulation == null) return;
                    var world = _simulation.World;
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
                        // If it's solid, we'll draw over it, but for Destructible, we need a floor underneath when it breaks
                        DrawRectangle(transform.Position + new Vector2(1,1), transform.Size - new Vector2(2,2), Color.Gray);

                        if (tiles[i].Type == TileComponent.TileType.Solid) 
                        {
                            DrawRectangle(transform.Position + new Vector2(1,1), transform.Size - new Vector2(2,2), Color.DarkGray);
                        }
                        else if (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed) 
                        {
                            DrawRectangle(transform.Position + new Vector2(1,1), transform.Size - new Vector2(2,2), Color.SaddleBrown);
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

                         DrawRectangle(transform.Position + new Vector2(4, 4), transform.Size - new Vector2(8,8), bColor);
                    }

                    // 3. Draw Powerups
                    var powerups = world.Powerups.GetAll();
                    var powerupEntities = world.Powerups.GetEntities();
                    for(int i=0; i<powerups.Count; i++)
                    {
                         if (i >= powerups.Count) break;
                         var entity = powerupEntities[i];
                         TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                         
                         Color pColor = Color.White;
                         if (powerups[i].Type == PowerupComponent.PowerupType.Range) pColor = Color.Yellow;
                         if (powerups[i].Type == PowerupComponent.PowerupType.Capacity) pColor = Color.Black;
                         
                         DrawRectangle(transform.Position, transform.Size, pColor);
                    }

                    // 4. Draw Explosions
                    var expList = world.Explosions.GetAll();
                    var expEntities = world.Explosions.GetEntities();
                    for(int i=0; i<expList.Count; i++)
                    {
                        if (i >= expEntities.Count) break;
                        var entity = expEntities[i];
                        TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                        DrawRectangle(transform.Position, transform.Size, Color.OrangeRed);
                    }

                    // 5. Draw Players
                    var players = world.Players.GetAll();
                    var playerEntities = world.Players.GetEntities();
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (!players[i].Alive) continue; 
                        var entity = playerEntities[i];
                        TransformComponent transform = FindTransform(entity, transformEntities, transforms); // Returns 24x24 hitbox

                        // Render Logic:
                        // Draw at actual hitbox size to prevent clipping
                        Color pColor = i == 0 ? Color.White : Color.Blue;
                        DrawRectangle(transform.Position, transform.Size, pColor);
                        
                        // Eyes (Adjusted for 24x24)
                        Vector2 eyeOffset = new Vector2(4, 6);
                        DrawRectangle(transform.Position + eyeOffset, new Vector2(4, 6), Color.Black);
                        DrawRectangle(transform.Position + new Vector2(transform.Size.X - eyeOffset.X - 4, eyeOffset.Y), new Vector2(4, 6), Color.Black);
                    }
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

    public static class Program
    {
        [STAThread]
        static void Main()
        {
            try 
            {
                using (var game = new Game1())
                    game.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("CRASH: " + e.ToString());
                throw;
            }
        }
    }
}
