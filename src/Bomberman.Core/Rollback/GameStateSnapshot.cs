using System;
using Bomberman.Core.ECS.Pools;
using System.Collections.Generic;

using Bomberman.Core;

namespace Bomberman.Core.Rollback
{
    public class GameStateSnapshot
    {
        public int Frame { get; set; }
        public uint NextEntityId { get; set; }
        
        private object[] _states;

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
    

        // --- Serialization Logic ---
        // We use a DTO structure to handle the generic pools.
        
        public class SnapshotDto
        {
             public int Frame { get; set; }
             public uint NextEntityId { get; set; }
             public uint RngState { get; set; }
             public List<PoolDto> Pools { get; set; } = new List<PoolDto>();
        }

        public class PoolDto
        {
            // We store the Component Type Name to match it back to the correct pool index/type
            public string ComponentType { get; set; } = "";
            public List<Entity> Entities { get; set; } = new List<Entity>();
            // We serialize the components list as a raw JSON Element or string because it's polymorphic from here
            public string JsonComponents { get; set; } = "";
        }

        public byte[] Serialize()
        {
             // We can't implement instance Serialize() easily because we lack type info for _states.
             // The Host creates a FRESH snapshot for the Sync. So we can Serialize FROM World directly.
            // Note: Use 0 for RNG state here if we are serializing internally, OR pass it in if available. 
            // Instance Serialize() is deprecated anyway.
            throw new NotImplementedException("Use SerializeWorld static method instead.");
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
                
                // We need to serialize the Component List part.
                // Reflection to get Item2?
                // Or add a `SerializeState()` method to IComponentPool?
                
                // Let's stick to adding Serialize to IComponentPool for cleanliness.
                // But for now, using dynamic/reflection to avoid touching Core interfaces too much.
                
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

            // Restore RNG
            if (rng != null) rng.State = dto.RngState;

            world.Clear();
            world.NextEntityId = dto.NextEntityId;
            
            foreach(var poolDto in dto.Pools)
            {
                // Find matching pool
                var pool = world.AllPools.Find(p => p.ComponentType.FullName == poolDto.ComponentType);
                if (pool != null)
                {
                    // Deserialize the component list
                    // We need to deserialize to List<T> where T is pool.ComponentType
                    Type listType = typeof(List<>).MakeGenericType(pool.ComponentType);
                    object components = System.Text.Json.JsonSerializer.Deserialize(poolDto.JsonComponents, listType, options);
                    
                    // Reconstruct state tuple
                    // (List<Entity>, List<T>)
                    // We can't cast to (List<Entity>, List<T>) easily because T is runtime.
                    // But `RestoreState` takes `object`.
                    // The `ComponentPool<T>.RestoreState` expects `(List<Entity>, List<T>)` which is `ValueTuple<...>`.
                    
                    // Create tuple dynamically
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

