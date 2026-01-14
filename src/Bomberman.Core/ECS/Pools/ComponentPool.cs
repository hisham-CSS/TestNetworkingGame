namespace Bomberman.Core.ECS.Pools;

using System;
using System.Collections.Generic;
using Bomberman.Core.ECS; // For Entity? Wait, Entity is in root Core or Core.ECS? Entity was in Components.cs? No, line 2 in Step 266 said Entity.cs exists!

/// <summary>
/// A dense storage pool for ECS components of a specific type.
/// Uses a sparse set approach (Entity ID -> Index) for O(1) lookups and O(1) iteration.
/// </summary>
/// <typeparam name="T">The component struct type.</typeparam>
public class ComponentPool<T> : IComponentPool where T : struct
{
    private List<T> _components = new();
    private List<Entity> _entities = new();
    private Dictionary<Entity, int> _entityToIndex = new();
    
    public int Count => _components.Count;
    public Type ComponentType => typeof(T);

    /// <summary>
    /// Captures the entire state of the pool (Entities and Components).
    /// Used for Rollback snapshots.
    /// </summary>
    /// <returns>A tuple of (List{Entity}, List{T}) boxed as object.</returns>
    public object CaptureState()
    {
        return (new List<Entity>(_entities), new List<T>(_components));
    }

    /// <summary>
    /// Restores the state of the pool from a snapshot.
    /// Rebuilds the internal lookup dictionary.
    /// </summary>
    /// <param name="state">The boxed state object returned from CaptureState.</param>
    public void RestoreState(object state)
    {
        var tuple = ((List<Entity>, List<T>))state;
        _entities = new List<Entity>(tuple.Item1);
        _components = new List<T>(tuple.Item2);
        
        // Rebuild Dictionary
        _entityToIndex.Clear();
        for (int i = 0; i < _entities.Count; i++)
        {
            _entityToIndex[_entities[i]] = i;
        }
    }
    
    /// <summary>
    /// Adds or updates a component for the specified entity.
    /// </summary>
    public void Add(Entity entity, T component)
    {
        if (_entityToIndex.ContainsKey(entity)) 
        {
             // Component already exists, update it.
             int index = _entityToIndex[entity];
             _components[index] = component;
             return;
        }
        
        _entityToIndex[entity] = _entities.Count;
        _entities.Add(entity);
        _components.Add(component);
    }
    
    /// <summary>
    /// Removes all components and entities from the pool.
    /// </summary>
    public void Clear()
    {
        _components.Clear();
        _entities.Clear();
        _entityToIndex.Clear();
    }

    /// <summary>
    /// Bulk sets the state of the pool (used for testing or initialization).
    /// </summary>
    public void SetAll(List<Entity> entities, List<T> components)
    {
        _entities = new List<Entity>(entities);
        _components = new List<T>(components);
        
        _entityToIndex.Clear();
        for (int i = 0; i < _entities.Count; i++)
        {
            _entityToIndex[_entities[i]] = i;
        }
    }
    
    public T Get(int index) => _components[index];
    public Entity GetEntity(int index) => _entities[index];
    
    public void Set(int index, T component) => _components[index] = component;
    
    public List<T> GetAll() => _components;
    public List<Entity> GetEntities() => _entities;
    
    /// <summary>
    /// Removes a component for the specified entity.
    /// Uses Swap-and-Pop to maintain dense packing.
    /// </summary>
    public void Remove(Entity entity)
    {
        if (_entityToIndex.TryGetValue(entity, out int index))
        {
            // Swap with last
            int lastIndex = _entities.Count - 1;
            
            if (index != lastIndex)
            {
                Entity lastEntity = _entities[lastIndex];
                T lastComponent = _components[lastIndex];
                
                _entities[index] = lastEntity;
                _components[index] = lastComponent;
                
                _entityToIndex[lastEntity] = index;
            }
            
            _entities.RemoveAt(lastIndex);
            _components.RemoveAt(lastIndex);
            _entityToIndex.Remove(entity);
        }
    }

    /// <summary>
    /// Checks if the entity has a component in this pool.
    /// </summary>
    public bool Has(Entity entity) => _entityToIndex.ContainsKey(entity);

    /// <summary>
    /// Retrieves the component for the specified entity.
    /// Throws KeyNotFoundException if the entity is not in the pool.
    /// </summary>
    public T Get(Entity entity)
    {
        if (!_entityToIndex.TryGetValue(entity, out int index)) 
            throw new KeyNotFoundException($"Entity {entity.Index} not in pool {typeof(T).Name}");
        return _components[index];
    }
}
