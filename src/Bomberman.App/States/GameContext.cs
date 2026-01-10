using Microsoft.Xna.Framework.Graphics;
using Bomberman.App.GameHost;
using Bomberman.App.Rendering;
using Bomberman.Net;

namespace Bomberman.App.States
{
    public class GameContext
    {
        public Game1 Game { get; set; }
        public NetworkController? Network { get; set; }
        public SpriteBatch SpriteBatch { get; set; }
        public Texture2D PixelTexture { get; set; }
        public PixelFont Font { get; set; }
        public bool EnableDebugLogs { get; set; } = true;

        public GameContext(Game1 game, SpriteBatch spriteBatch, Texture2D pixelTexture, PixelFont font)
        {
            Game = game;
            SpriteBatch = spriteBatch;
            PixelTexture = pixelTexture;
            Font = font;
        }
    }
}
