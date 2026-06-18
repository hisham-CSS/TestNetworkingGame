using System;

namespace Bomberman.Core
{
    /// <summary>
    /// Represents a 2D rectangle with integer coordinates.
    /// </summary>
    public struct IntRect : IEquatable<IntRect>
    {
        /// <summary>X coordinate of the top-left corner.</summary>
        public int X;
        /// <summary>Y coordinate of the top-left corner.</summary>
        public int Y;
        /// <summary>Width of the rectangle.</summary>
        public int Width;
        /// <summary>Height of the rectangle.</summary>
        public int Height;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntRect"/> struct.
        /// </summary>
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

        /// <summary>
        /// Checks if this rectangle intersects with another.
        /// </summary>
        public bool Intersects(IntRect other)
        {
            return other.Left < Right &&
                   Left < other.Right &&
                   other.Top < Bottom &&
                   Top < other.Bottom;
        }
        
        /// <summary>
        /// Checks if this rectangle contains the given point.
        /// </summary>
        public bool Contains(IntVector2 point)
        {
            return (point.X >= X && point.X < X + Width && point.Y >= Y && point.Y < Y + Height); 
        }

        /// <inheritdoc/>
        public bool Equals(IntRect other)
        {
             return X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is IntRect other && Equals(other);
        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public static bool operator ==(IntRect left, IntRect right) => left.Equals(right);
        public static bool operator !=(IntRect left, IntRect right) => !left.Equals(right);

        /// <inheritdoc/>
        public override string ToString() => $"{{X:{X} Y:{Y} Width:{Width} Height:{Height}}}";
    }
}
