namespace Bomberman.Core.ECS.Components;

using Bomberman.Core; // For IntVector2

/// <summary>
/// Spatial presence in the game world.
/// </summary>
public struct TransformComponent
{
    /// <summary>Continuous position in simulation units (e.g. 1/100th pixel).</summary>
    public IntVector2 Position;
    /// <summary>AABB Size.</summary>
    public IntVector2 Size;
}
