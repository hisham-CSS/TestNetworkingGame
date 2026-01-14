using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.App.GameHost;
using Bomberman.App.States;
using Bomberman.App.Rendering;
using Bomberman.App.Input;

namespace Bomberman.App
{
    /// <summary>
    /// Handles the initialization of game dependencies and services.
    /// </summary>
    public class GameBootstrapper
    {
        /// <summary>
        /// Creates and wires up the GameContext with all necessary services (Renderer, Input, Logger, etc.).
        /// </summary>
        public static GameContext InitializeDependencies(Game1 game, GraphicsDevice graphicsDevice)
        {
            var spriteBatch = new SpriteBatch(graphicsDevice);
            
            // Core Assets
            var pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            // Services
            var font = new PixelFont(pixelTexture);
            var inputService = new MonogameInputService();
            var renderer = new RenderingService(graphicsDevice, spriteBatch, font, pixelTexture);
            
            // Logging
            var logger = new Bomberman.Core.Logging.CompositeLogger(
                new Bomberman.Core.Logging.ConsoleLogger(),
                new Bomberman.Core.Logging.FileLogger("gamelog.txt")
            );

            return new GameContext(game, spriteBatch, pixelTexture, font, inputService, renderer, logger);
        }
    }
}
