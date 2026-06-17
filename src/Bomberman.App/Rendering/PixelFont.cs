using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Bomberman.App
{
    /// <summary>
    /// A tiny 5x5 bitmap font drawn with the 1x1 white pixel the View already owns. Week 2's Game1
    /// could only draw rectangles; the lobby/menu screens need text, so this is the minimal way to
    /// render strings without content pipeline assets. Back-ported/extended from the prototype PixelFont.
    /// </summary>
    public static class PixelFont
    {
        private const int GW = 5, GH = 5; // glyph grid

        private static readonly Dictionary<char, string[]> G = new()
        {
            {'A',new[]{"01110","10001","11111","10001","10001"}},
            {'B',new[]{"11110","10001","11110","10001","11110"}},
            {'C',new[]{"01111","10000","10000","10000","01111"}},
            {'D',new[]{"11110","10001","10001","10001","11110"}},
            {'E',new[]{"11111","10000","11110","10000","11111"}},
            {'F',new[]{"11111","10000","11110","10000","10000"}},
            {'G',new[]{"01111","10000","10011","10001","01111"}},
            {'H',new[]{"10001","10001","11111","10001","10001"}},
            {'I',new[]{"01110","00100","00100","00100","01110"}},
            {'J',new[]{"00111","00010","00010","10010","01100"}},
            {'K',new[]{"10001","10010","11100","10010","10001"}},
            {'L',new[]{"10000","10000","10000","10000","11111"}},
            {'M',new[]{"10001","11011","10101","10001","10001"}},
            {'N',new[]{"10001","11001","10101","10011","10001"}},
            {'O',new[]{"01110","10001","10001","10001","01110"}},
            {'P',new[]{"11110","10001","11110","10000","10000"}},
            {'Q',new[]{"01110","10001","10001","10101","01111"}},
            {'R',new[]{"11110","10001","11110","10010","10001"}},
            {'S',new[]{"01111","10000","01110","00001","11110"}},
            {'T',new[]{"11111","00100","00100","00100","00100"}},
            {'U',new[]{"10001","10001","10001","10001","01110"}},
            {'V',new[]{"10001","10001","10001","01010","00100"}},
            {'W',new[]{"10001","10001","10101","11011","10001"}},
            {'X',new[]{"10001","01010","00100","01010","10001"}},
            {'Y',new[]{"10001","01010","00100","00100","00100"}},
            {'Z',new[]{"11111","00010","00100","01000","11111"}},
            {'0',new[]{"01110","10011","10101","11001","01110"}},
            {'1',new[]{"00100","01100","00100","00100","01110"}},
            {'2',new[]{"11110","00001","01110","10000","11111"}},
            {'3',new[]{"11110","00001","01110","00001","11110"}},
            {'4',new[]{"00010","00110","01010","11111","00010"}},
            {'5',new[]{"11111","10000","11110","00001","11110"}},
            {'6',new[]{"01110","10000","11110","10001","01110"}},
            {'7',new[]{"11111","00001","00010","00100","01000"}},
            {'8',new[]{"01110","10001","01110","10001","01110"}},
            {'9',new[]{"01110","10001","01111","00001","01110"}},
            {' ',new[]{"00000","00000","00000","00000","00000"}},
            {'.',new[]{"00000","00000","00000","00000","00100"}},
            {',',new[]{"00000","00000","00000","00100","01000"}},
            {':',new[]{"00000","00100","00000","00100","00000"}},
            {'-',new[]{"00000","00000","11111","00000","00000"}},
            {'/',new[]{"00001","00010","00100","01000","10000"}},
            {'!',new[]{"00100","00100","00100","00000","00100"}},
            {'?',new[]{"01110","10001","00110","00000","00100"}},
            {'>',new[]{"01000","00100","00010","00100","01000"}},
            {'<',new[]{"00010","00100","01000","00100","00010"}},
            {'[',new[]{"01110","01000","01000","01000","01110"}},
            {']',new[]{"01110","00010","00010","00010","01110"}},
            {'(',new[]{"00110","01000","01000","01000","00110"}},
            {')',new[]{"01100","00010","00010","00010","01100"}},
            {'%',new[]{"10001","00010","00100","01000","10001"}},
            {'*',new[]{"00000","10101","01110","10101","00000"}},
        };

        /// <summary>Width in pixels a string occupies at the given pixel scale (1px inter-glyph gap).</summary>
        public static float MeasureWidth(string text, float px) => text.Length * (GW + 1) * px;

        /// <summary>Draws text using filled cells of the bitmap font. <paramref name="px"/> is the size
        /// of one font pixel in screen pixels.</summary>
        public static void Draw(SpriteBatch sb, Texture2D pixel, string text, float x, float y, float px, Color color)
        {
            float cx = x;
            foreach (char raw in text)
            {
                char c = char.ToUpperInvariant(raw);
                if (G.TryGetValue(c, out var rows))
                {
                    for (int gy = 0; gy < GH; gy++)
                        for (int gx = 0; gx < GW; gx++)
                            if (rows[gy][gx] == '1')
                                sb.Draw(pixel, new Rectangle((int)(cx + gx * px), (int)(y + gy * px), (int)px, (int)px), color);
                }
                cx += (GW + 1) * px;
            }
        }

        public static void DrawCentered(SpriteBatch sb, Texture2D pixel, string text, float centerX, float y, float px, Color color)
            => Draw(sb, pixel, text, centerX - MeasureWidth(text, px) / 2f, y, px, color);
    }
}
