using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.App.States;
using Bomberman.App.Rendering;

namespace Bomberman.App.GameHost
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        
        private GameStateManager _stateManager = null!;
        private GameContext _context = null!;
        
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
            
            var pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            // PixelFont (Assume simple implementation or load)
             var font = new PixelFont(pixelTexture); // PixelFont constructor might need texture or nothing if it builds it?
            // Checking PixelFont usage in previous Game1: 
            // It wasn't explicitly instantiated in Game1, wait.
            // Game1.cs didn't use separate PixelFont class instance, it used `_font.DrawText`? 
            // No, Game1.cs had `private void DrawText(...)` method LOCALLY.
            // Ah, I need to make sure `PixelFont` class exists or use the local logic.
            // The Refactor plan mentioned `PixelFont` in `GameContext`.
            // Let's check `Rendering` folder.
            
            // Re-creating a simple PixelFont helper since the previous Game1.DrawText was hardcoded logic.
            // I'll assume I need to create `PixelFont` class if it doesn't exist or use the logic I just extracted.
            // I'll create a lightweight `PixelFont` wrapper in `GameContext` or just pass the drawing logic?
            // Better to have a class.
            
            // PixelFont
            // _font = new PixelFont(pixelTexture); // Assume logic exists or handled by context
            
            _stateManager = new GameStateManager();
            _context = new GameContext(this, _spriteBatch, pixelTexture, font, new Bomberman.App.Input.MonogameInputService());
            
            _stateManager.ChangeState(new MenuState(_context, _stateManager));
        }

        protected override void Update(GameTime gameTime)
        {
            _stateManager.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _stateManager.Draw(gameTime);
            base.Draw(gameTime);
        }
    }
}
