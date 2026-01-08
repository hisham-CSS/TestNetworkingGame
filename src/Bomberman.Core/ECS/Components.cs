namespace Bomberman.Core;

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

public interface IComponentPool
{
    Type ComponentType { get; }
    object CaptureState();
    void RestoreState(object state);
    void Clear();
}

public class ComponentPool<T> : IComponentPool where T : struct
{
    private List<T> _components = new();
    private List<Entity> _entities = new();
    
    public int Count => _components.Count;
    public Type ComponentType => typeof(T);

    public object CaptureState()
    {
        return (new List<Entity>(_entities), new List<T>(_components));
    }

    public void RestoreState(object state)
    {
        var tuple = ((List<Entity>, List<T>))state;
        _entities = new List<Entity>(tuple.Item1);
        _components = new List<T>(tuple.Item2);
    }
    
    public void Add(Entity entity, T component)
    {
        _entities.Add(entity);
        _components.Add(component);
    }
    
    public void Clear()
    {
        _components.Clear();
        _entities.Clear();
    }

    public void SetAll(List<Entity> entities, List<T> components)
    {
        _entities = new List<Entity>(entities);
        _components = new List<T>(components);
    }
    
    public T Get(int index) => _components[index];
    public Entity GetEntity(int index) => _entities[index];
    
    public void Set(int index, T component) => _components[index] = component;
    
    public List<T> GetAll() => _components;
    public List<Entity> GetEntities() => _entities;
    
    public void Remove(Entity entity)
    {
        int index = _entities.IndexOf(entity);
        if (index != -1)
        {
            if (index < _components.Count - 1)
            {
                _components[index] = _components[_components.Count - 1];
                _entities[index] = _entities[_entities.Count - 1];
            }
            _components.RemoveAt(_components.Count - 1);
            _entities.RemoveAt(_entities.Count - 1);
        }
    }

    public bool Has(Entity entity) => _entities.Contains(entity);

    public T Get(Entity entity)
    {
        int index = _entities.IndexOf(entity);
        if (index == -1) throw new KeyNotFoundException($"Entity {entity.Index} not in pool {typeof(T).Name}");
        return _components[index];
    }
}

public struct TransformComponent
{
    public IntVector2 Position; // Continuous position in simulation units (e.g. 1/100th pixel)
    public IntVector2 Size;     // AABB Size
}

public struct PlayerComponent
{
    public uint PlayerId;
    public bool Alive;
    public int BombRange;
    public int BombCapacity;
}

public struct InputState
{
    public IntVector2 Movement; // Input direction (-1, 0, 1)
    public bool PlaceBomb;
    public Point BombTarget; // Explicit Grid Coordinate

    public bool Equals(InputState other)
    {
        return Movement == other.Movement && PlaceBomb == other.PlaceBomb && BombTarget == other.BombTarget;
    }
}

public struct BombComponent
{
    public int Timer;
    public int MaxTimer;
    public int Range;
    public uint OwnerId; 
}

public struct ExplosionComponent
{
    public int Timer;
    public int MaxTimer;
}

public struct PowerupComponent
{
    public enum PowerupType { None, Range, Capacity }
    public PowerupType Type;
}

public struct TileComponent
{
    public enum TileType { Solid, Destructible, Empty }
    public TileType Type;
    public bool Destroyed;
    public PowerupComponent.PowerupType HiddenPowerup;
}