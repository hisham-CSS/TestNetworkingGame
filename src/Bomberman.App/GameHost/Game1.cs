using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.Core;

namespace Bomberman.App
{
    /// <summary>
    /// The View layer. It owns the GameSession and a SimulationLoop, but does NOT step the
    /// simulation itself — the loop runs the fixed-timestep sim on a worker thread. Each frame the
    /// View submits the latest input and draws the most recently published RenderSnapshot.
    /// </summary>
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private Texture2D _pixel = null!;
        private KeyboardState _previousKeyboardState;

        private GameSession _session = null!;
        private SimulationLoop _loop = null!;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 480;   // 15 tiles * 32
            _graphics.PreferredBackBufferHeight = 416;  // 13 tiles * 32
        }

        protected override void Initialize()
        {
            _session = new GameSession(12345);

            // Determinism self-check still runs headlessly before the loop starts.
            DeterminismHarness.Verify(12345, out string report);
            Console.WriteLine(report);

            _loop = new SimulationLoop(_session);
            _loop.Start();   // simulation now advances on its own thread
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();

            Vector2 movement = Vector2.Zero;
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))    movement.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))  movement.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))  movement.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) movement.X += 1;
            if (movement != Vector2.Zero) movement.Normalize();

            bool placeBomb = keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space);

            // Hand the input to the simulation thread (producer-consumer).
            _loop.SubmitInput(new InputState { Movement = movement, PlaceBomb = placeBomb });

            _previousKeyboardState = keyboardState;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            RenderSnapshot? snap = _loop.LatestSnapshot;   // consume the latest published frame
            if (snap == null) { base.Draw(gameTime); return; }

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            foreach (var t in snap.Tiles)
            {
                if (t.Variant == (int)TileComponent.TileType.Solid)
                    Rect(t.Position, t.Size, Color.DarkGray);
                else if (t.Variant == (int)TileComponent.TileType.Destructible && !t.Flag)
                    Rect(t.Position, t.Size, Color.Brown);
            }
            foreach (var b in snap.Bombs)
            {
                Rect(b.Position + new Vector2(4, 4), b.Size - new Vector2(8, 8), Color.Black);
                Rect(b.Position + new Vector2(12, 12), new Vector2(8, 8), Color.Yellow);
            }
            foreach (var p in snap.Powerups)
            {
                Color c = p.Variant == (int)PowerupComponent.PowerupType.Range ? Color.Yellow
                        : p.Variant == (int)PowerupComponent.PowerupType.Capacity ? Color.Black
                        : Color.White;
                Rect(p.Position, p.Size, c);
            }
            foreach (var e in snap.Explosions) Rect(e.Position, e.Size, Color.OrangeRed);
            foreach (var pl in snap.Players) if (pl.Flag) Rect(pl.Position, pl.Size, Color.Blue);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private void Rect(Vector2 position, Vector2 size, Color color)
        {
            _spriteBatch.Draw(_pixel, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
        }

        protected override void Dispose(bool disposing)
        {
            _loop?.Stop();   // stop the simulation thread on shutdown
            base.Dispose(disposing);
        }
    }
}
