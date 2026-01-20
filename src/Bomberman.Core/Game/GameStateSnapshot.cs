using System;
using Bomberman.Core.ECS.Pools;
using System.Collections.Generic;
using Chronos.Core;

namespace Bomberman.Core.Game
{
    /// <summary>
    /// Represents a complete capture of the game state at a specific frame.
    /// Used for rollback restoration and state serialization.
    /// </summary>
    public class GameStateSnapshot : IDeterministicState
    {
        public int Frame { get; set; }
        public uint NextEntityId { get; set; }
        
        private object[] _states;

        /// <summary>
        /// Captures the current state of the World.
        /// </summary>
        public GameStateSnapshot(int frame, World world)
        {
            Frame = frame;
            NextEntityId = world.NextEntityId;
            _states = new object[world.AllPools.Count];
            
            for(int i = 0; i < world.AllPools.Count; i++)
            {
                _states[i] = world.AllPools[i].CaptureState();
            }
        }

        public int CalculateHash()
        {
            return StateHasher.Hash(this);
        }

        /// <summary>
        /// Restores the World to the state captured in this snapshot.
        /// </summary>
        public void Restore(World world)
        {
            world.Clear();
            world.NextEntityId = NextEntityId;

            // Assumes World structure is identical to when Snapshot was created
            for(int i = 0; i < world.AllPools.Count; i++)
            {
                if (i < _states.Length)
                {
                    world.AllPools[i].RestoreState(_states[i]);
                }
            }
        }

        /// <summary>
        /// Retrieves the component state for a specific type from the snapshot.
        /// </summary>
        public (List<Entity> entities, List<T> components) GetState<T>() where T : struct
        {
            if (World.ComponentIndexMap.TryGetValue(typeof(T), out int index))
            {
                if (index < _states.Length)
                {
                     return ((List<Entity>, List<T>))_states[index];
                }
            }
            // Optimization: Return cached empty lists
            return (EmptyCache<Entity>.List, EmptyCache<T>.List);
        }
    

        // --- Serialization Logic (Helpers) ---
        
        public class SnapshotDto
        {
             public int Frame { get; set; }
             public uint NextEntityId { get; set; }
             public uint RngState { get; set; }
             public List<PoolDto> Pools { get; set; } = new List<PoolDto>();
        }

        public class PoolDto
        {
            public string ComponentType { get; set; } = "";
            public List<Entity> Entities { get; set; } = new List<Entity>();
            public string JsonComponents { get; set; } = "";
        }

        // Static Helper to Serialize directly from a World instance
        public static byte[] SerializeWorld(int frame, World world, uint rngState)
        {
             var dto = new SnapshotDto
            {
                Frame = frame,
                NextEntityId = world.NextEntityId,
                RngState = rngState
            };

            var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };

            foreach(var pool in world.AllPools)
            {
                var state = pool.CaptureState(); // (List<Entity>, List<T>)
                
                dynamic tuple = state;
                var entities = (List<Entity>)tuple.Item1;
                var components = tuple.Item2; // List<T>
                
                string jsonComponents = System.Text.Json.JsonSerializer.Serialize(components, options);
                
                dto.Pools.Add(new PoolDto
                {
                    ComponentType = pool.ComponentType.FullName,
                    Entities = entities,
                    JsonComponents = jsonComponents
                });
            }

            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(dto, options);
        }

        public static int RestoreFromBytes(World world, Bomberman.Core.Game.DeterministicRandom rng, byte[] data)
        {
            var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            var dto = System.Text.Json.JsonSerializer.Deserialize<SnapshotDto>(data, options);
            if (dto == null) return 0;

            if (rng != null) rng.State = dto.RngState;

            world.Clear();
            world.NextEntityId = dto.NextEntityId;
            
            foreach(var poolDto in dto.Pools)
            {
                // Find matching pool
                var pool = world.AllPools.Find(p => p.ComponentType.FullName == poolDto.ComponentType);
                if (pool != null)
                {
                    Type listType = typeof(List<>).MakeGenericType(pool.ComponentType);
                    object components = System.Text.Json.JsonSerializer.Deserialize(poolDto.JsonComponents, listType, options);
                    
                    Type tupleType = typeof(ValueTuple<,>).MakeGenericType(typeof(List<Entity>), listType);
                    object stateTuple = Activator.CreateInstance(tupleType, poolDto.Entities, components);
                    
                    pool.RestoreState(stateTuple);
                }
            }

            return dto.Frame;
        }
    }

    internal static class EmptyCache<T>
    {
        public static readonly List<T> List = new List<T>();
    }
}
