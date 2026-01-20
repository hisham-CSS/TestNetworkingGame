using System.IO;
using Chronos.Core;

namespace Bomberman.Core.Input;

using Bomberman.Core; // For IntVector2

/// <summary>
/// Represents the input state for a single player for a specific frame.
/// This struct is serialized and sent over the network.
/// </summary>
public struct InputState : IInputState<InputState>
{
    /// <summary>Movement direction vector (typically -1, 0, or 1 for each axis).</summary>
    public IntVector2 Movement; 
    
    /// <summary>True if the player is attempting to place a bomb.</summary>
    public bool PlaceBomb;
    
    /// <summary>The grid coordinate target for the bomb placement.</summary>
    public IntVector2 BombTarget; 

    public bool Equals(InputState other)
    {
        return Movement == other.Movement && PlaceBomb == other.PlaceBomb && BombTarget == other.BombTarget;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Movement.X);
        writer.Write(Movement.Y);
        writer.Write(PlaceBomb);
        writer.Write(BombTarget.X);
        writer.Write(BombTarget.Y);
    }

    public static InputState Deserialize(BinaryReader reader)
    {
        return new InputState
        {
            Movement = new IntVector2(reader.ReadInt32(), reader.ReadInt32()),
            PlaceBomb = reader.ReadBoolean(),
            BombTarget = new IntVector2(reader.ReadInt32(), reader.ReadInt32())
        };
    }
}
