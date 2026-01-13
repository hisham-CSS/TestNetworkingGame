using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.App.GameHost;
using Bomberman.App.States;
using Bomberman.App.Rendering;
using Bomberman.App.Input;

namespace Bomberman.App
{
    public class GameBootstrapper
    {
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

            return new GameContext(game, spriteBatch, pixelTexture, font, inputService, renderer);
        }
    }
}
