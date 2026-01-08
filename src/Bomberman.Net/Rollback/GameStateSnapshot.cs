using System;
using System.Collections.Generic;

using Bomberman.Core;

namespace Bomberman.Net
{
    public class GameStateSnapshot
    {
        public int Frame { get; set; }
        public uint NextEntityId { get; set; }
        
        private Dictionary<Type, object> _componentStates = new Dictionary<Type, object>();

        public GameStateSnapshot(int frame, World world)
        {
            Frame = frame;
            NextEntityId = world.NextEntityId;

            foreach(var pool in world.AllPools)
            {
                _componentStates[pool.ComponentType] = pool.CaptureState();
            }
        }

        public void Restore(World world)
        {
            world.Clear();
            world.NextEntityId = NextEntityId;

            foreach(var pool in world.AllPools)
            {
                if (_componentStates.TryGetValue(pool.ComponentType, out var state))
                {
                    pool.RestoreState(state);
                }
            }
        }

        public (List<Entity> entities, List<T> components) GetState<T>() where T : struct
        {
            if (_componentStates.TryGetValue(typeof(T), out var state))
            {
                return ((List<Entity>, List<T>))state;
            }
            return (new List<Entity>(), new List<T>());
        }
    }
}
