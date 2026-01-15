namespace Bomberman.Core.ECS.Components;

/// <summary>
/// Identifies an entity as a player and tracks their game state stats.
/// </summary>
public struct PlayerComponent
{
    public uint PlayerId;
    public bool Alive;
    /// <summary>Current explosion range powerup level.</summary>
    public int BombRange;
    /// <summary>Current maximum bombs powerup level.</summary>
    public int BombCapacity;
}
