using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Bomberman
{
    public class Game1 : Game
    {
        private Texture2D _pixelTexture = null!;   // assigned in LoadContent
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;  // assigned in LoadContent
        
        private Simulation _simulation = null!;    // assigned in Initialize
        private KeyboardState _previousKeyboardState;

        // Week 1: input buffering + determinism verification
        private InputBuffer _inputBuffer = new InputBuffer();
        private int _frame = 0;

        // Fixed Timestep
        private const float FixedTimeStep = 1f / 60f;
        private double _accumulator = 0.0;

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
            _simulation = new Simulation(12345); // Seeded

            // Verify determinism BEFORE the first frame is drawn: record a scripted run,
            // replay it from a fresh world, and confirm the per-frame state hashes match.
            DeterminismHarness.Verify(12345, out string report);
            Console.WriteLine(report);

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
                // Console.WriteLine("Update Start");
                var keyboardState = Keyboard.GetState();
                
                // Collect Input (Poll)
                Vector2 movement = Vector2.Zero;
                if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) movement.Y -= 1;
                if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) movement.Y += 1;
                if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) movement.X -= 1;
                if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) movement.X += 1;

                if (movement != Vector2.Zero) movement.Normalize();

                bool placeBomb = keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space);

                var inputs = new InputState[] 
                {
                    new InputState { Movement = movement, PlaceBomb = placeBomb }
                };

                // Fixed Update Loop
                _accumulator += gameTime.ElapsedGameTime.TotalSeconds;

                while (_accumulator >= FixedTimeStep)
                {
                    // Record this frame's inputs into the 256-frame ring buffer (enables replay).
                    _inputBuffer.Record(_frame, inputs);

                    _simulation.Update(inputs, FixedTimeStep);

                    // Per-frame state hash; logged once per simulated second as a determinism heartbeat.
                    int stateHash = StateHasher.Hash(_simulation.World);
                    if (_frame % 60 == 0)
                        Console.WriteLine($"[frame {_frame}] state hash = 0x{stateHash:X8}");

                    _frame++;
                    _accumulator -= FixedTimeStep;
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
                GraphicsDevice.Clear(Color.CornflowerBlue); 
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        
                var world = _simulation.World;
                var transformEntities = world.Transforms.GetEntities();
                var transforms = world.Transforms.GetAll();

                // 1. Draw Tiles
                var tileList = world.Tiles.GetAll();
                var tileEntities = world.Tiles.GetEntities();
                
                for(int i=0; i<tileList.Count; i++)
                {
                    var entity = tileEntities[i];
                    TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                    
                    Color color = tileList[i].Type switch
                    {
                        TileComponent.TileType.Solid => Color.DarkGray,
                        TileComponent.TileType.Destructible => tileList[i].Destroyed ? Color.DarkGreen : Color.Brown,
                        _ => Color.Transparent
                    };
                    
                    if (tileList[i].Type == TileComponent.TileType.Destructible && !tileList[i].Destroyed)
                    {
                        DrawRectangle(transform.Position, transform.Size, color);
                    }
                    else if (tileList[i].Type == TileComponent.TileType.Solid)
                    {
                        DrawRectangle(transform.Position, transform.Size, color);
                    }
                }

                // 2. Draw Bombs
                var bombList = world.Bombs.GetAll();
                var bombEntities = world.Bombs.GetEntities();
                for(int i=0; i<bombList.Count; i++)
                {
                    var entity = bombEntities[i];
                    TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                    
                    DrawRectangle(transform.Position + new Vector2(4,4), transform.Size - new Vector2(8,8), Color.Black);
                    DrawRectangle(transform.Position + new Vector2(12, 12), new Vector2(8,8), Color.Yellow);
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
                    if (i >= expList.Count) break; // Defensive
                    var entity = expEntities[i];
                    TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                    DrawRectangle(transform.Position, transform.Size, Color.OrangeRed);
                }

                // 4. Draw Players
                var playerList = world.Players.GetAll();
                var playerEntities = world.Players.GetEntities();
                for(int i=0; i<playerList.Count; i++)
                {
                     if (i >= playerList.Count) break; // Defensive
                    if (!playerList[i].Alive) continue;
                    
                    var entity = playerEntities[i];
                    TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                    
                    DrawRectangle(transform.Position, transform.Size, Color.Blue);
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
