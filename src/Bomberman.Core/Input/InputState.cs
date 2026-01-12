namespace Bomberman.Core.Input;

using Bomberman.Core; // For IntVector2

public struct InputState
{
    public IntVector2 Movement; // Input direction (-1, 0, 1)
    public bool PlaceBomb;
    public IntVector2 BombTarget; // Explicit Grid Coordinate

    public bool Equals(InputState other)
    {
        return Movement == other.Movement && PlaceBomb == other.PlaceBomb && BombTarget == other.BombTarget;
    }
}
