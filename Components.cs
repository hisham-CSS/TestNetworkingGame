using System;
using System.Collections.Generic;

public class ComponentPool<T> where T : struct
{
    private List<T> _components = new();
    private List<Entity> _entities = new();
    
    public int Count => _components.Count;
    
    public void Add(Entity entity, T component)
    {
        _entities.Add(entity);
        _components.Add(component);
    }
    
    public T Get(int index) => _components[index];
    public Entity GetEntity(int index) => _entities[index];
    
    public void Set(int index, T component) => _components[index] = component;
    
    public List<T> GetAll() => _components;
    public List<Entity> GetEntities() => _entities;
}


public struct TransformComponent
{
    public int GridX;  // Grid position (0-12)
    public int GridY;  // Grid position (0-12)
}

public struct PlayerComponent
{
    public uint PlayerId;
    public bool Alive;
    public int InputX;
    public int InputY;
}

public struct BombComponent
{
    public int Timer;
    public int MaxTimer;
}

public struct ExplosionComponent
{
    public int Timer;
    public int MaxTimer;
}

public struct TileComponent
{
    public enum TileType { Solid, Destructible, Empty }
    public TileType Type;
    public bool Destroyed;
}