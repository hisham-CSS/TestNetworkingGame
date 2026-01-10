using System;
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
    }

    internal static class EmptyCache<T>
    {
        public static readonly List<T> List = new List<T>();
    }
}

