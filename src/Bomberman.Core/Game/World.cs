using System;
using System.Collections.Generic;
namespace Bomberman.Core
{
    public class World
    {
        public uint NextEntityId { get; set; } = 0;
        
        public List<IComponentPool> AllPools { get; } = new List<IComponentPool>();

        public ComponentPool<TransformComponent> Transforms { get; }
        public ComponentPool<PlayerComponent> Players { get; }
        public ComponentPool<BombComponent> Bombs { get; }
        public ComponentPool<ExplosionComponent> Explosions { get; }
        public ComponentPool<TileComponent> Tiles { get; }
        public ComponentPool<PowerupComponent> Powerups { get; }

        public static readonly Dictionary<Type, int> ComponentIndexMap = new();

        public World()
        {
            Transforms = Register(new ComponentPool<TransformComponent>());
            Players = Register(new ComponentPool<PlayerComponent>());
            Bombs = Register(new ComponentPool<BombComponent>());
            Explosions = Register(new ComponentPool<ExplosionComponent>());
            Tiles = Register(new ComponentPool<TileComponent>());
            Powerups = Register(new ComponentPool<PowerupComponent>());
        }

        private T Register<T>(T pool) where T : IComponentPool
        {
            if (!ComponentIndexMap.ContainsKey(typeof(T)))
            {
                ComponentIndexMap[typeof(T)] = AllPools.Count; // Store index before adding
            }
            AllPools.Add(pool);
            return pool;
        }

        public void Clear()
        {
            foreach(var pool in AllPools)
            {
                pool.Clear();
            }
        }
        
        public Entity CreateEntity()
        {
            return new Entity(NextEntityId++);
        }
    }
}
