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
        private Dictionary<int, InputState> _remoteInputBuffer = new Dictionary<int, InputState>(); // Frame -> Input
        private int _currentFrame = 0;

        // Game State
        private enum GameState { Menu, Playing, Replaying }
        private GameState _state = GameState.Menu;
        private int _menuSelection = 0; // 0 = Play (Local), 1 = Host, 2 = Join, 3 = Replay

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
                            _state = GameState.Playing;
                            _isRecording = false; // Disable recording in net play for now
                            _isReplaying = false;
                            _isNetworked = true;
                            _localPlayerId = 0;
                            _currentFrame = 0;
                            _remoteInputBuffer.Clear();

                            _networkManager = new NetworkManager(5000); // Host on 5000
                            _networkManager.OnPacketReceived += OnNetworkPacket;
                            
                            _simulation = new Simulation(randomSeed, 2);
                            Console.WriteLine("Hosting on Port 5000...");
                        }
                        else if (_menuSelection == 2) // Join
                        {
                            _state = GameState.Playing;
                            _isRecording = false;
                            _isReplaying = false;
                            _isNetworked = true;
                            _localPlayerId = 1;
                            _currentFrame = 0;
                            _remoteInputBuffer.Clear();

                            _networkManager = new NetworkManager(5001); // Client on 5001 (simplified)
                            _networkManager.Connect("127.0.0.1", 5000); // Connect to localhost host
                            _networkManager.OnPacketReceived += OnNetworkPacket;

                            _simulation = new Simulation(randomSeed, 2);
                             Console.WriteLine("Joining 127.0.0.1:5000...");
                        }
                        else if (_menuSelection == 3) // Replay
                        {
                            _state = GameState.Replaying;
                            _isRecording = false;
                            _isReplaying = true;
                            _isNetworked = false;
                            _replayFrame = 0;
                            _recorder.Load(Path.Combine("Replays", "replay.json"));
                            _simulation = new Simulation(randomSeed, 2);
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

        private void OnNetworkPacket(byte[] data)
        {
            var (frame, input) = InputRecorder.DeserializeInput(data);
            // Console.WriteLine($"Recv Frame {frame}");
            if (!_remoteInputBuffer.ContainsKey(frame))
            {
                _remoteInputBuffer.Add(frame, input);
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

            bool placeBomb = keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space);
            
            InputState localInput = new InputState { Movement = movement, PlaceBomb = placeBomb };

            if (_isNetworked)
            {
                // Lockstep Logic
                
                // 1. Send Local Input for THIS frame
                byte[] packet = InputRecorder.SerializeInput(_currentFrame, localInput);
                if (_networkManager != null) _networkManager.Send(packet);

                // 2. Do we have Remote Input for THIS frame?
                if (_remoteInputBuffer.ContainsKey(_currentFrame))
                {
                    // YES! We have both.
                    InputState remoteInput = _remoteInputBuffer[_currentFrame];
                    
                    // Construct Full Input Array (Player 0, Player 1)
                    inputs = new InputState[2];
                    inputs[_localPlayerId] = localInput;
                    inputs[1 - _localPlayerId] = remoteInput;

                    // Advance
                    // Advance
                    if (_simulation != null) _simulation.Update(inputs, (float)FixedTimeStep);
                    _currentFrame++;
                    // Remove old input to save memory (optional)
                    _remoteInputBuffer.Remove(_currentFrame - 100); 
                }
                else
                {
                    // NO. STALL.
                    // Console.WriteLine($"Stalling Frame {_currentFrame} (Waiting for P{1-_localPlayerId})");
                }
            }
            else
            {
                // Local Single Player
                inputs = new InputState[] { localInput };
                if (_isRecording) _recorder.RecordFrame(inputs);
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
