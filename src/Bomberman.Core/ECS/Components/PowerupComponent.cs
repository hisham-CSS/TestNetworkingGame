namespace Bomberman.Core.ECS.Components;

public struct PowerupComponent
{
    public enum PowerupType { None, Range, Capacity }
    public PowerupType Type;
}
