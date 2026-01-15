namespace Bomberman.Core.ECS.Components;

/// <summary>
/// Represents a planted bomb in the game world.
/// </summary>
public struct BombComponent
{
    /// <summary>Time remaining until explosion (ticks or seconds).</summary>
    public int Timer;
    public int MaxTimer;
    /// <summary>Explosion radius in tiles.</summary>
    public int Range;
    /// <summary>The ID of the player who placed this bomb.</summary>
    public uint OwnerId; 
}
