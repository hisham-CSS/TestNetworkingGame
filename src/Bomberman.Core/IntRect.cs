using System;

namespace Bomberman.Core
{
    public struct IntRect : IEquatable<IntRect>
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public IntRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Left => X;
        public int Right => X + Width;
        public int Top => Y;
        public int Bottom => Y + Height;

        public bool Intersects(IntRect other)
        {
            return other.Left < Right &&
                   Left < other.Right &&
                   other.Top < Bottom &&
                   Top < other.Bottom;
        }
        
        public bool Contains(IntVector2 point)
        {
            return (point.X >= X && point.X < X + Width && point.Y >= Y && point.Y < Y + Height); 
        }

        public bool Equals(IntRect other)
        {
             return X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj) => obj is IntRect other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public static bool operator ==(IntRect left, IntRect right) => left.Equals(right);
        public static bool operator !=(IntRect left, IntRect right) => !left.Equals(right);

        public override string ToString() => $"{{X:{X} Y:{Y} Width:{Width} Height:{Height}}}";
    }
}
