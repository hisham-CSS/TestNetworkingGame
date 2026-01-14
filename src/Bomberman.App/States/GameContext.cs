using Microsoft.Xna.Framework.Graphics;
using Bomberman.App.GameHost;
using Bomberman.App.Rendering;
using Bomberman.Net;
using Bomberman.App.Input;

namespace Bomberman.App.States
{
    /// <summary>
    /// Shared service container/context passed between game states.
    /// Holds references to global dependencies like Input, Renderer, Network, and Factory.
    /// </summary>
    public class GameContext
    {
        public Game1 Game { get; set; }
        public NetworkController? Network { get; set; }
        public SpriteBatch SpriteBatch { get; set; }
        public Texture2D PixelTexture { get; set; }
        public PixelFont Font { get; set; }
        public IInputService Input { get; set; }
        public IRenderer Renderer { get; set; }
        public Core.Logging.ILogger Logger { get; set; }
        public StateFactory StateFactory { get; set; } = null!;
        
        /// <summary>Global toggle for debug overlays (FPS, etc).</summary>
        public bool EnableDebugLogs { get; set; } = true;

        public GameContext(Game1 game, SpriteBatch spriteBatch, Texture2D pixelTexture, PixelFont font, IInputService input, IRenderer renderer, Core.Logging.ILogger logger)
        {
            Game = game;
            SpriteBatch = spriteBatch;
            PixelTexture = pixelTexture;
            Font = font;
            Input = input;
            Renderer = renderer;
            Logger = logger;
        }
    }
}
