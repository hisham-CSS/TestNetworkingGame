using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Bomberman
{
    /// <summary>A tiny 5x5 bitmap font drawn with the 1x1 pixel texture (no content pipeline needed).
    /// Back-ported from the production Bomberman.App.Rendering.PixelFont.</summary>
    public class PixelFont
    {
        private readonly Texture2D _pixelTexture;
        public PixelFont(Texture2D pixelTexture) { _pixelTexture = pixelTexture; }

        public void DrawText(SpriteBatch spriteBatch, int x, int y, string text, Color color, int scale)
        {
            int spacing = 1 * scale, charWidth = 5 * scale, currentX = x, currentY = y;
            foreach (char c in text)
            {
                if (c == '\n') { currentY += 5 * scale + spacing * 2; currentX = x; continue; }
                bool[,] grid = GetBitmap(c);
                for (int py = 0; py < 5; py++)
                    for (int px = 0; px < 5; px++)
                        if (grid[px, py])
                            spriteBatch.Draw(_pixelTexture, new Rectangle(currentX + px * scale, currentY + py * scale, scale, scale), color);
                currentX += charWidth + spacing;
            }
        }

        public Point MeasureString(string text, int scale)
        {
            int spacing = 1 * scale, charWidth = 5 * scale, charHeight = 5 * scale;
            if (string.IsNullOrEmpty(text)) return Point.Zero;
            int width = 0, height = charHeight, lineWidth = 0;
            foreach (char c in text)
            {
                if (c == '\n') { width = System.Math.Max(width, lineWidth); lineWidth = 0; height += charHeight + spacing * 2; continue; }
                lineWidth += charWidth + spacing;
            }
            width = System.Math.Max(width, lineWidth);
            return new Point(width, height);
        }

        private static readonly Dictionary<char, string[]> _chars = new Dictionary<char, string[]>
        {
            {'A',new[]{"01110","10001","11111","10001","10001"}}, {'B',new[]{"11110","10001","11110","10001","11110"}},
            {'C',new[]{"01111","10000","10000","10000","01111"}}, {'D',new[]{"11110","10001","10001","10001","11110"}},
            {'E',new[]{"11111","10000","11110","10000","11111"}}, {'F',new[]{"11111","10000","11110","10000","10000"}},
            {'G',new[]{"01111","10000","10011","10001","01111"}}, {'H',new[]{"10001","10001","11111","10001","10001"}},
            {'I',new[]{"01110","00100","00100","00100","01110"}}, {'J',new[]{"01111","00010","00010","10010","01100"}},
            {'K',new[]{"10001","10010","11100","10010","10001"}}, {'L',new[]{"10000","10000","10000","10000","11111"}},
            {'M',new[]{"10001","11011","10101","10001","10001"}}, {'N',new[]{"10001","11001","10101","10011","10001"}},
            {'O',new[]{"01110","10001","10001","10001","01110"}}, {'P',new[]{"11110","10001","11110","10000","10000"}},
            {'Q',new[]{"01110","10001","10001","10101","01111"}}, {'R',new[]{"11110","10001","11110","10001","10001"}},
            {'S',new[]{"01111","10000","01110","00001","11110"}}, {'T',new[]{"11111","00100","00100","00100","00100"}},
            {'U',new[]{"10001","10001","10001","10001","01110"}}, {'V',new[]{"10001","10001","10001","01010","00100"}},
            {'W',new[]{"10001","10001","10101","11011","10001"}}, {'X',new[]{"10001","01010","00100","01010","10001"}},
            {'Y',new[]{"10001","10001","01110","00100","00100"}}, {'Z',new[]{"11111","00010","00100","01000","11111"}},
            {' ',new[]{"00000","00000","00000","00000","00000"}}, {'-',new[]{"00000","00000","11111","00000","00000"}},
            {'!',new[]{"00100","00100","00100","00000","00100"}}, {'.',new[]{"00000","00000","00000","00000","00100"}}
        };

        public static bool[,] GetBitmap(char c)
        {
            char upper = char.ToUpper(c);
            if (!_chars.ContainsKey(upper)) return new bool[5, 5];
            string[] map = _chars[upper];
            bool[,] grid = new bool[5, 5];
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    grid[x, y] = map[y][x] == '1';
            return grid;
        }
    }
}
