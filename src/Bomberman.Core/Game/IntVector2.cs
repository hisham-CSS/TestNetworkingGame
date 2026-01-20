
using System;

namespace Bomberman.Core
{
    /// <summary>
    /// Represents a 2D vector with integer coordinates, used for deterministic physics.
    /// </summary>
    public struct IntVector2 : IEquatable<IntVector2>
    {
        /// <summary>
        /// The X coordinate.
        /// </summary>
        public int X;

        /// <summary>
        /// The Y coordinate.
        /// </summary>
        public int Y;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntVector2"/> struct.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        public IntVector2(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Gets a vector with (0,0).
        /// </summary>
        public static IntVector2 Zero => new IntVector2(0, 0);

        /// <summary>
        /// Gets a vector with (1,1).
        /// </summary>
        public static IntVector2 One => new IntVector2(1, 1);

        public static IntVector2 operator +(IntVector2 a, IntVector2 b) => new IntVector2(a.X + b.X, a.Y + b.Y);
        public static IntVector2 operator -(IntVector2 a, IntVector2 b) => new IntVector2(a.X - b.X, a.Y - b.Y);
        public static IntVector2 operator *(IntVector2 a, int b) => new IntVector2(a.X * b, a.Y * b);
        public static IntVector2 operator /(IntVector2 a, int b) => new IntVector2(a.X / b, a.Y / b);

        /// <inheritdoc/>
        public bool Equals(IntVector2 other) => X == other.X && Y == other.Y;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is IntVector2 other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(IntVector2 left, IntVector2 right) => left.Equals(right);
        public static bool operator !=(IntVector2 left, IntVector2 right) => !left.Equals(right);

        /// <inheritdoc/>
        public override string ToString() => $"{{X:{X} Y:{Y}}}";
    }
}
