using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Bomberman.App.Rendering
{
    public class RenderingService : IRenderer
    {
        private readonly SpriteBatch _spriteBatch;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly PixelFont _font;
        private readonly Texture2D _pixelTexture;

        public RenderingService(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, PixelFont font, Texture2D pixelTexture)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = font;
            _pixelTexture = pixelTexture;
        }

        public void BeginDraw()
        {
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        }

        public void EndDraw()
        {
            _spriteBatch.End();
        }

        public void ClearScreen(Color color)
        {
            _graphicsDevice.Clear(color);
        }

        public void DrawTextCentered(string text, int x, int y, Color color, int scale)
        {
            var size = _font.MeasureString(text, scale);
            _font.DrawText(_spriteBatch, x - size.X / 2, y - size.Y / 2, text, color, scale);
        }

        public void DrawText(string text, int x, int y, Color color, int scale)
        {
            _font.DrawText(_spriteBatch, x, y, text, color, scale);
        }

        public void DrawTexture(Rectangle destination, Color color)
        {
            _spriteBatch.Draw(_pixelTexture, destination, color);
        }
    }
}
