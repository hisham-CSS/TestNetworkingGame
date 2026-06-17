using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace Bomberman.Core
{
    /// <summary>One player's intent for a single frame. A serializable value — the unit the
    /// input buffer records and (from Week 3) the network sends.</summary>
    public struct InputState : IInputState<InputState>
    {
        public Vector2 Movement;  // normalized
        public bool PlaceBomb;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Movement.X);
            writer.Write(Movement.Y);
            writer.Write(PlaceBomb);
        }

        public static InputState Deserialize(BinaryReader reader)
        {
            var s = new InputState();
            s.Movement = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            s.PlaceBomb = reader.ReadBoolean();
            return s;
        }

        public bool Equals(InputState other) => Movement == other.Movement && PlaceBomb == other.PlaceBomb;
        public override bool Equals(object? obj) => obj is InputState s && Equals(s);
        public override int GetHashCode() => HashCode.Combine(Movement, PlaceBomb);
    }
}
