using Microsoft.Xna.Framework;

namespace Bomberman.App.Rendering
{
    /// <summary>
    /// Abstraction for 2D rendering operations.
    /// Decouples game logic from specific MonoGame calls.
    /// </summary>
    public interface IRenderer
    {
        /// <summary>Begins a new drawing batch.</summary>
        void BeginDraw();
        /// <summary>Ends the current drawing batch.</summary>
        void EndDraw();
        /// <summary>Clears the screen with a specific color.</summary>
        void ClearScreen(Color color);
        /// <summary>Draws text centered at the specified position.</summary>
        void DrawTextCentered(string text, int x, int y, Color color, int scale);
        /// <summary>Draws text at the specified position.</summary>
        void DrawText(string text, int x, int y, Color color, int scale);
        /// <summary>Draws a solid colored rectangle (using a white pixel texture).</summary>
        void DrawTexture(Rectangle destination, Color color);
    }
}
