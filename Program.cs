using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Bomberman
{
    public class Game1 : Game
    {
        private enum AppState { Menu, Playing, GameOver }
        private AppState _state = AppState.Menu;

        private Texture2D _pixelTexture = null!;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private PixelFont _font = null!;

        private Simulation _simulation = null!;
        private InputBuffer _inputBuffer = new InputBuffer();
        private int _frame = 0;
        private KeyboardState _previousKeyboardState;

        private const float FixedTimeStep = 1f / 60f;
        private double _accumulator = 0.0;
        private const int Seed = 12345;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 480;   // 15 * 32
            _graphics.PreferredBackBufferHeight = 416;  // 13 * 32
        }

        protected override void Initialize()
        {
            // Determinism self-check runs once at startup, independent of menu state.
            DeterminismHarness.Verify(Seed, out string report);
            Console.WriteLine(report);
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
            _font = new PixelFont(_pixelTexture);
        }

        private void StartGame()
        {
            _simulation = new Simulation(Seed);
            _inputBuffer = new InputBuffer();
            _frame = 0;
            _accumulator = 0;
            _state = AppState.Playing;
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            bool enter = keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter);

            switch (_state)
            {
                case AppState.Menu:     if (enter) StartGame();                 break;
                case AppState.Playing:  UpdatePlaying(gameTime, keyboardState); break;
                case AppState.GameOver: if (enter) _state = AppState.Menu;      break;
            }

            _previousKeyboardState = keyboardState;
            base.Update(gameTime);
        }

        private void UpdatePlaying(GameTime gameTime, KeyboardState keyboardState)
        {
            Vector2 movement = Vector2.Zero;
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))    movement.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))  movement.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))  movement.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) movement.X += 1;
            if (movement != Vector2.Zero) movement.Normalize();
            bool placeBomb = keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space);

            var inputs = new InputState[] { new InputState { Movement = movement, PlaceBomb = placeBomb } };

            _accumulator += gameTime.ElapsedGameTime.TotalSeconds;
            while (_accumulator >= FixedTimeStep)
            {
                _inputBuffer.Record(_frame, inputs);
                _simulation.Update(inputs, FixedTimeStep);
                int stateHash = StateHasher.Hash(_simulation.World);
                if (_frame % 60 == 0) Console.WriteLine($"[frame {_frame}] state hash = 0x{stateHash:X8}");
                _frame++;
                _accumulator -= FixedTimeStep;
            }

            if (!_simulation.AnyPlayerAlive()) _state = AppState.GameOver;
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            if (_state == AppState.Menu)
            {
                DrawCentered("BOMBERMAN", 110, Color.White, 6);
                DrawCentered("PRESS ENTER TO PLAY", 250, Color.Yellow, 2);
            }
            else
            {
                DrawWorld();
                if (_state == AppState.GameOver)
                {
                    DrawCentered("GAME OVER", 150, Color.Red, 5);
                    DrawCentered("PRESS ENTER", 230, Color.White, 2);
                }
            }

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private void DrawCentered(string text, int y, Color color, int scale)
        {
            Point size = _font.MeasureString(text, scale);
            _font.DrawText(_spriteBatch, (480 - size.X) / 2, y, text, color, scale);
        }

        private void DrawWorld()
        {
            var world = _simulation.World;
            var transformEntities = world.Transforms.GetEntities();
            var transforms = world.Transforms.GetAll();

            var tileList = world.Tiles.GetAll();
            var tileEntities = world.Tiles.GetEntities();
            for (int i = 0; i < tileList.Count; i++)
            {
                var t = FindTransform(tileEntities[i], transformEntities, transforms);
                if (tileList[i].Type == TileComponent.TileType.Destructible && !tileList[i].Destroyed)
                    DrawRectangle(t.Position, t.Size, Color.Brown);
                else if (tileList[i].Type == TileComponent.TileType.Solid)
                    DrawRectangle(t.Position, t.Size, Color.DarkGray);
            }

            var bombList = world.Bombs.GetAll();
            var bombEntities = world.Bombs.GetEntities();
            for (int i = 0; i < bombList.Count; i++)
            {
                var t = FindTransform(bombEntities[i], transformEntities, transforms);
                DrawRectangle(t.Position + new Vector2(4, 4), t.Size - new Vector2(8, 8), Color.Black);
                DrawRectangle(t.Position + new Vector2(12, 12), new Vector2(8, 8), Color.Yellow);
            }

            var powerups = world.Powerups.GetAll();
            var powerupEntities = world.Powerups.GetEntities();
            for (int i = 0; i < powerups.Count; i++)
            {
                var t = FindTransform(powerupEntities[i], transformEntities, transforms);
                Color c = powerups[i].Type == PowerupComponent.PowerupType.Range ? Color.Yellow
                        : powerups[i].Type == PowerupComponent.PowerupType.Capacity ? Color.Black : Color.White;
                DrawRectangle(t.Position, t.Size, c);
            }

            var expList = world.Explosions.GetAll();
            var expEntities = world.Explosions.GetEntities();
            for (int i = 0; i < expList.Count; i++)
            {
                var t = FindTransform(expEntities[i], transformEntities, transforms);
                DrawRectangle(t.Position, t.Size, Color.OrangeRed);
            }

            var playerList = world.Players.GetAll();
            var playerEntities = world.Players.GetEntities();
            for (int i = 0; i < playerList.Count; i++)
            {
                if (!playerList[i].Alive) continue;
                var t = FindTransform(playerEntities[i], transformEntities, transforms);
                DrawRectangle(t.Position, t.Size, Color.Blue);
            }
        }

        private TransformComponent FindTransform(Entity entity, List<Entity> transformEntities, List<TransformComponent> transforms)
        {
            for (int i = 0; i < transformEntities.Count; i++)
                if (transformEntities[i].Equals(entity)) return transforms[i];
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
            try { using var game = new Game1(); game.Run(); }
            catch (Exception e) { Console.WriteLine("CRASH: " + e); throw; }
        }
    }
}
