namespace Bomberman.Core;

using System;

public struct Entity : IEquatable<Entity>
{
    public uint Index { get; set; }
    
    public Entity(uint index)
    {
        Index = index;
    }
    
    public override bool Equals(object? obj) => obj is Entity e && Equals(e);
    public bool Equals(Entity other) => Index == other.Index;
    public override int GetHashCode() => Index.GetHashCode();
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}