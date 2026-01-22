using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.App.States;

namespace Bomberman.App.GameHost
{
    /// <summary>
    /// The main entry point for the game.
    /// Manages the GameStateManager, GraphicsDevice, and top-level game loop.
    /// </summary>
    public class Game1 : Game, IGameHost
    {
        private GraphicsDeviceManager _graphics;
        private GameStateManager _stateManager = null!;
        private GameContext _context = null!;
        
        public int WindowWidth => GraphicsDevice.Viewport.Width;
        public int WindowHeight => GraphicsDevice.Viewport.Height;

        /// <summary>
        /// Initializes the graphics device and content directory.
        /// Sets the window size to match the simulation grid.
        /// </summary>
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
            Exiting += (s, e) => _context.Network?.Close();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _context = GameBootstrapper.InitializeDependencies(this, GraphicsDevice);
            _stateManager = new GameStateManager();
            
            // Initialize Factory
            _context.StateFactory = new StateFactory(_context, _stateManager);
            
            _stateManager.ChangeState(_context.StateFactory.CreateMenu());
        }

        protected override void Update(GameTime gameTime)
        {
            _context.Input.Update();
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
