
using System;

namespace Bomberman.Core
{
    public struct IntVector2 : IEquatable<IntVector2>
    {
        public int X;
        public int Y;

        public IntVector2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static IntVector2 Zero => new IntVector2(0, 0);
        public static IntVector2 One => new IntVector2(1, 1);

        public static IntVector2 operator +(IntVector2 a, IntVector2 b) => new IntVector2(a.X + b.X, a.Y + b.Y);
        public static IntVector2 operator -(IntVector2 a, IntVector2 b) => new IntVector2(a.X - b.X, a.Y - b.Y);
        public static IntVector2 operator *(IntVector2 a, int b) => new IntVector2(a.X * b, a.Y * b);
        public static IntVector2 operator /(IntVector2 a, int b) => new IntVector2(a.X / b, a.Y / b);

        public bool Equals(IntVector2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is IntVector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(IntVector2 left, IntVector2 right) => left.Equals(right);
        public static bool operator !=(IntVector2 left, IntVector2 right) => !left.Equals(right);

        public override string ToString() => $"{{X:{X} Y:{Y}}}";
    }
}
