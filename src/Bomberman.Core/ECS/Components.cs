using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Bomberman.Core
{
    /// <summary>Packed, value-type storage for one component kind (parallel id/data lists).</summary>
    public class ComponentPool<T> where T : struct
    {
        private List<T> _components = new();
        private List<Entity> _entities = new();
        public int Count => _components.Count;

        public void Add(Entity entity, T component) { _entities.Add(entity); _components.Add(component); }
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
                    _entities[index]   = _entities[_entities.Count - 1];
                }
                _components.RemoveAt(_components.Count - 1);
                _entities.RemoveAt(_entities.Count - 1);
            }
        }
    }

    public struct TransformComponent { public Vector2 Position; public Vector2 Size; }
    public struct PlayerComponent { public uint PlayerId; public bool Alive; public int BombRange; public int BombCapacity; }
    public struct BombComponent { public int Timer; public int MaxTimer; public int Range; public uint OwnerId; }
    public struct ExplosionComponent { public int Timer; public int MaxTimer; }

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
}
