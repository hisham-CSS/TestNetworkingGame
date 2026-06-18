using System;
using System.Collections.Generic;
namespace Bomberman.Core
{
    public class World
    {
        private uint _nextEntityId = 0;

        /// <summary>The id the next CreateEntity() will hand out. Captured/restored with snapshots so
        /// entity identity stays stable across a rollback or resync (Week 4-5).</summary>
        public uint NextEntityId { get => _nextEntityId; set => _nextEntityId = value; }
        
        public ComponentPool<TransformComponent> Transforms { get; } = new();
        public ComponentPool<PlayerComponent> Players { get; } = new();
        public ComponentPool<BombComponent> Bombs { get; } = new();
        public ComponentPool<ExplosionComponent> Explosions { get; } = new();
        public ComponentPool<TileComponent> Tiles { get; } = new();
        public ComponentPool<PowerupComponent> Powerups { get; } = new();
        
        public Entity CreateEntity()
        {
            return new Entity(_nextEntityId++);
        }

        /// <summary>Empties every pool. Used before restoring a snapshot.</summary>
        public void Clear()
        {
            Transforms.Clear(); Players.Clear(); Bombs.Clear();
            Explosions.Clear(); Tiles.Clear(); Powerups.Clear();
        }
    }
}
