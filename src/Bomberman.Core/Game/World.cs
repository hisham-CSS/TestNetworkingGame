using System;
using Bomberman.Core.ECS.Components;
using Bomberman.Core.ECS.Pools;
using System.Collections.Generic;
namespace Bomberman.Core
{
    /// <summary>
    /// The central container for all Entities and Components in the ECS architecture.
    /// Manages Component Pools and Entity creation.
    /// </summary>
    public class World
    {
        /// <summary>
        /// The counter for assigning unique IDs to new entities.
        /// </summary>
        public uint NextEntityId { get; set; } = 0;
        
        /// <summary>
        /// A list of all registered component pools, used for iterating during state capture/restore.
        /// </summary>
        public List<IComponentPool> AllPools { get; } = new List<IComponentPool>();

        public ComponentPool<TransformComponent> Transforms { get; }
        public ComponentPool<PlayerComponent> Players { get; }
        public ComponentPool<BombComponent> Bombs { get; }
        public ComponentPool<ExplosionComponent> Explosions { get; }
        public ComponentPool<TileComponent> Tiles { get; }
        public ComponentPool<PowerupComponent> Powerups { get; }

        /// <summary>
        /// Maps component types to their index in the <see cref="AllPools"/> list.
        /// </summary>
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

        /// <summary>
        /// Clears all components from all pools. Does NOT reset NextEntityId.
        /// </summary>
        public void Clear()
        {
            foreach(var pool in AllPools)
            {
                pool.Clear();
            }
        }
        
        /// <summary>
        /// Creates a new Entity with a unique ID.
        /// </summary>
        /// <returns>A new Entity struct.</returns>
        public Entity CreateEntity()
        {
            return new Entity(NextEntityId++);
        }
    }
}
