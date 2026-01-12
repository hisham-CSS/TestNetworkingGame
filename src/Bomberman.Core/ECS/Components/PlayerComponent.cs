namespace Bomberman.Core.ECS.Components;

public struct PlayerComponent
{
    public uint PlayerId;
    public bool Alive;
    public int BombRange;
    public int BombCapacity;
}
