using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.App.States;

namespace Bomberman.App.GameHost
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
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
            _context = GameBootstrapper.InitializeDependencies(this, GraphicsDevice);
            _stateManager = new GameStateManager();
            
            // Initialize Factory
            _context.StateFactory = new StateFactory(_context, _stateManager);
            
            _stateManager.ChangeState(_context.StateFactory.CreateMenu());
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
