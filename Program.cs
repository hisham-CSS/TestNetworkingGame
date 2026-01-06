using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Bomberman
{
    public class Game1 : Game
    {
        private Texture2D _pixelTexture;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        
        private Simulation _simulation;
        private KeyboardState _previousKeyboardState;

        // Fixed Timestep
        private const float FixedTimeStep = 1f / 60f;
        private double _accumulator = 0.0;
        
        // Replay System
        private InputRecorder _recorder = new InputRecorder();
        private bool _isRecording = false;
        private bool _isReplaying = false;
        private int _replayFrame = 0;
        private int randomSeed = 12345;

        // Game State
        private enum GameState { Menu, Playing, Replaying }
        private GameState _state = GameState.Menu;
        private int _menuSelection = 0; // 0 = Play, 1 = Replay

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window size to match Simulation (15x13 tiles at 32 pixels)
            // 15 * 32 = 480
            // 13 * 32 = 416
            _graphics.PreferredBackBufferWidth = 480;
            _graphics.PreferredBackBufferHeight = 416;
        }

        protected override void Initialize()
        {
            // _simulation = new Simulation(randomSeed); // Init on play instead
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Create a 1x1 white texture for drawing primitives
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            try
            {
                // Console.WriteLine("Update Start");
                var keyboardState = Keyboard.GetState();
                
                if (_state == GameState.Menu)
                {
                    // Menu Logic
                    if ((keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) && 
                        !(_previousKeyboardState.IsKeyDown(Keys.W) || _previousKeyboardState.IsKeyDown(Keys.Up)))
                    {
                        _menuSelection--;
                        if (_menuSelection < 0) _menuSelection = 1;
                    }
                    if ((keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) && 
                        !(_previousKeyboardState.IsKeyDown(Keys.S) || _previousKeyboardState.IsKeyDown(Keys.Down)))
                    {
                        _menuSelection++;
                        if (_menuSelection > 1) _menuSelection = 0;
                    }

                    if (keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space) ||
                        keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                    {
                        if (_menuSelection == 0)
                        {
                            // Play
                             _state = GameState.Playing;
                             _isRecording = true;
                             _isReplaying = false;
                             _recorder.Reset();
                             _simulation = new Simulation(randomSeed);
                        }
                        else if (_menuSelection == 1)
                        {
                            // Replay
                            _state = GameState.Replaying;
                            _isRecording = false;
                            _isReplaying = true;
                            _replayFrame = 0;
                            _recorder.Load(Path.Combine("Replays", "replay.json"));
                            _simulation = new Simulation(randomSeed);
                        }
                    }
                }
                else if (_state == GameState.Playing || _state == GameState.Replaying)
                {
                    if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
                    {
                        // Return to Menu
                        _state = GameState.Menu;
                        if (_isRecording) _recorder.Save(Path.Combine("Replays", "replay.json")); // Auto-save on exit
                    }

                    InputState[] inputs;

                    if (_state == GameState.Replaying)
                    {
                         // Replay Mode
                         inputs = _recorder.GetFrame(_replayFrame);
                         if (inputs == null || inputs.Length == 0) inputs = new InputState[1];
                    }
                    else
                    {
                        // Live Logic
                        Vector2 movement = Vector2.Zero;
                        if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) movement.Y -= 1;
                        if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) movement.Y += 1;
                        if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) movement.X -= 1;
                        if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) movement.X += 1;

                        if (movement != Vector2.Zero) movement.Normalize();

                        bool placeBomb = keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space);

                        inputs = new InputState[] 
                        {
                            new InputState { Movement = movement, PlaceBomb = placeBomb }
                        };

                        if (_isRecording) _recorder.RecordFrame(inputs);
                    }

                    // Fixed Update Loop
                    _accumulator += gameTime.ElapsedGameTime.TotalSeconds;

                    while (_accumulator >= FixedTimeStep)
                    {
                        _simulation.Update(inputs, FixedTimeStep);
                        if (_state == GameState.Replaying) _replayFrame++;
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
                    int btnHeight = 50;
                    int centerX = _graphics.PreferredBackBufferWidth / 2 - btnWidth / 2;
                    int centerY = _graphics.PreferredBackBufferHeight / 2 - 60;

                    // Play Button (Green)
                    Color playColor = _menuSelection == 0 ? Color.Lime : Color.Green;
                    DrawRectangle(new Vector2(centerX, centerY), new Vector2(btnWidth, btnHeight), playColor);
                    // Selection Border
                    if (_menuSelection == 0) DrawHollowRect(new Rectangle(centerX-2, centerY-2, btnWidth+4, btnHeight+4), Color.White);

                    // Replay Button (Blue)
                    int rX = centerX;
                    int rY = centerY + 80;
                    Color replayColor = _menuSelection == 1 ? Color.Cyan : Color.Blue;
                    DrawRectangle(new Vector2(rX, rY), new Vector2(btnWidth, btnHeight), replayColor);
                    // Selection Border
                    if (_menuSelection == 1) DrawHollowRect(new Rectangle(rX-2, rY-2, btnWidth+4, btnHeight+4), Color.White);
                }
                else
                {
                    // Draw Simulation
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

        private void DrawHollowRect(Rectangle rect, Color color)
        {
            int t = 2; // Thickness
            DrawRectangle(new Vector2(rect.X, rect.Y), new Vector2(rect.Width, t), color); // Top
            DrawRectangle(new Vector2(rect.X, rect.Bottom - t), new Vector2(rect.Width, t), color); // Bottom
            DrawRectangle(new Vector2(rect.X, rect.Y), new Vector2(t, rect.Height), color); // Left
            DrawRectangle(new Vector2(rect.Right - t, rect.Y), new Vector2(t, rect.Height), color); // Right
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
