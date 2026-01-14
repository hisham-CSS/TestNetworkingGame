namespace Bomberman.Core;

using System;

/// <summary>
/// Represents a lightweight identifier for an entity in the ECS world.
/// </summary>
public struct Entity : IEquatable<Entity>
{
    /// <summary>
    /// The unique index of the entity.
    /// </summary>
    public uint Index { get; set; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Entity"/> struct.
    /// </summary>
    /// <param name="index">The unique index.</param>
    public Entity(uint index)
    {
        Index = index;
    }
    
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Entity e && Equals(e);
    
    /// <inheritdoc/>
    public bool Equals(Entity other) => Index == other.Index;
    
    /// <inheritdoc/>
    public override int GetHashCode() => Index.GetHashCode();
    
    /// <summary>
    /// Compares two entities for equality.
    /// </summary>
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    
    /// <summary>
    /// Compares two entities for inequality.
    /// </summary>
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}