using System;

public struct Entity : IEquatable<Entity>
{
    public uint Index { get; }
    public byte Generation { get; }
    
    public Entity(uint index, byte generation)
    {
        Index = index;
        Generation = generation;
    }
    
    public override bool Equals(object? obj) => obj is Entity e && Equals(e);
    public bool Equals(Entity other) => Index == other.Index && Generation == other.Generation;
    public override int GetHashCode() => Index.GetHashCode() ^ Generation.GetHashCode();
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}