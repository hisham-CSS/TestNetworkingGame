using Microsoft.Xna.Framework;
using Bomberman.App.Rendering;

namespace Bomberman.Tests.Mocks
{
    public class MockRenderer : IRenderer
    {
        public void BeginDraw() { }
        public void EndDraw() { }
        public void ClearScreen(Color color) { }
        public void DrawTextCentered(string text, int x, int y, Color color, int scale) { }
        public void DrawText(string text, int x, int y, Color color, int scale) { }
        public void DrawTexture(Rectangle destination, Color color) { }
    }
}
