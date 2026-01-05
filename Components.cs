public struct TransformComponent
{
    public int X;
    public int Y;
    public int VelocityX;
    public int VelocityY;
}

public struct PlayerComponent
{
    public uint PlayerId;
    public bool Alive;
}

public struct BombComponent
{
    public uint OwnerId;
    public int Timer;
    public int MaxTimer;
}

public struct ExplosionComponent
{
    public int Timer;
    public int MaxTimer;
}