namespace Bomberman.Core.ECS.Components;

using Bomberman.Core; // For IntVector2

public struct TransformComponent
{
    public IntVector2 Position; // Continuous position in simulation units (e.g. 1/100th pixel)
    public IntVector2 Size;     // AABB Size
}
