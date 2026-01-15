namespace Bomberman.Core.ECS.Components;

/// <summary>
/// Represents a static or destructible grid object.
/// </summary>
public struct TileComponent
{
    public enum TileType { Solid, Destructible, Empty }
    public TileType Type;
    public bool Destroyed;
    /// <summary>Type of powerup hidden under this tile (if destructible).</summary>
    public PowerupComponent.PowerupType HiddenPowerup;
}
