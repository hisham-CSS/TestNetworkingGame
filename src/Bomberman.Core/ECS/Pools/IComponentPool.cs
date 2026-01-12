namespace Bomberman.Core.ECS.Pools;

using System;

public interface IComponentPool
{
    Type ComponentType { get; }
    object CaptureState();
    void RestoreState(object state);
    void Clear();
}
