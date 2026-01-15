namespace Bomberman.Core.ECS.Components;

/// <summary>
/// Represents a collectible item.
/// </summary>
public struct PowerupComponent
{
    public enum PowerupType { None, Range, Capacity }
    public PowerupType Type;
}
