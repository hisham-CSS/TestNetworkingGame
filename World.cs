using System;
using System.Collections.Generic;
namespace Bomberman
{
    public class World
    {
        public uint NextEntityId { get; set; } = 0;
        
        public ComponentPool<TransformComponent> Transforms { get; } = new();
        public ComponentPool<PlayerComponent> Players { get; } = new();
        public ComponentPool<BombComponent> Bombs { get; } = new();
        public ComponentPool<ExplosionComponent> Explosions { get; } = new();
        public ComponentPool<TileComponent> Tiles { get; } = new();
        public ComponentPool<PowerupComponent> Powerups { get; } = new();

        public void Clear()
        {
            Transforms.Clear();
            Players.Clear();
            Bombs.Clear();
            Explosions.Clear();
            Tiles.Clear();
            Powerups.Clear();
        }
        
        public Entity CreateEntity()
        {
            return new Entity(NextEntityId++);
        }
    }
}
