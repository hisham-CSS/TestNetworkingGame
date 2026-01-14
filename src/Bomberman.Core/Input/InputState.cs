namespace Bomberman.Core.Input;

using Bomberman.Core; // For IntVector2

/// <summary>
/// Represents the input state for a single player for a specific frame.
/// This struct is serialized and sent over the network.
/// </summary>
public struct InputState
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
}
