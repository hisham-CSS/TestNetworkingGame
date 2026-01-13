using Microsoft.Xna.Framework;

namespace Bomberman.App.Rendering
{
    public interface IRenderer
    {
        void BeginDraw();
        void EndDraw();
        void ClearScreen(Color color);
        void DrawTextCentered(string text, int x, int y, Color color, int scale);
        void DrawText(string text, int x, int y, Color color, int scale);
        void DrawTexture(Rectangle destination, Color color);
    }
}
