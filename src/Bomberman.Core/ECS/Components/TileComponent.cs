namespace Bomberman.Core.ECS.Components;

public struct TileComponent
{
    public enum TileType { Solid, Destructible, Empty }
    public TileType Type;
    public bool Destroyed;
    public PowerupComponent.PowerupType HiddenPowerup;
}
