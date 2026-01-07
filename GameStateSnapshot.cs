using System;
using System.Collections.Generic;

namespace Bomberman
{
    public class GameStateSnapshot
    {
        public int Frame { get; set; }

        public uint NextEntityId { get; set; }

        public List<PlayerComponent> Players { get; set; }
        public List<Entity> PlayerEntities { get; set; }

        public List<TransformComponent> Transforms { get; set; }
        public List<Entity> TransformEntities { get; set; }

        public List<BombComponent> Bombs { get; set; }
        public List<Entity> BombEntities { get; set; }

        public List<ExplosionComponent> Explosions { get; set; }
        public List<Entity> ExplosionEntities { get; set; }

        public List<TileComponent> Tiles { get; set; }
        public List<Entity> TileEntities { get; set; }

        public List<PowerupComponent> Powerups { get; set; }
        public List<Entity> PowerupEntities { get; set; }

        public GameStateSnapshot(int frame, World world)
        {
            Frame = frame;
            NextEntityId = world.NextEntityId;

            // Deep Copy Players
            Players = new List<PlayerComponent>(world.Players.GetAll());
            PlayerEntities = new List<Entity>(world.Players.GetEntities());

            // Deep Copy Transforms
            Transforms = new List<TransformComponent>(world.Transforms.GetAll());
            TransformEntities = new List<Entity>(world.Transforms.GetEntities());

            // Deep Copy Bombs
            Bombs = new List<BombComponent>(world.Bombs.GetAll());
            BombEntities = new List<Entity>(world.Bombs.GetEntities());

            // Deep Copy Explosions
            Explosions = new List<ExplosionComponent>(world.Explosions.GetAll());
            ExplosionEntities = new List<Entity>(world.Explosions.GetEntities());

            // Deep Copy Tiles
            Tiles = new List<TileComponent>(world.Tiles.GetAll());
            TileEntities = new List<Entity>(world.Tiles.GetEntities());

            // Deep Copy Powerups
            Powerups = new List<PowerupComponent>(world.Powerups.GetAll());
            PowerupEntities = new List<Entity>(world.Powerups.GetEntities());
        }

        public void Restore(World world)
        {
            world.Clear();
            world.NextEntityId = NextEntityId;

            world.Players.SetAll(PlayerEntities, Players);
            world.Transforms.SetAll(TransformEntities, Transforms);
            world.Bombs.SetAll(BombEntities, Bombs);
            world.Explosions.SetAll(ExplosionEntities, Explosions);
            world.Tiles.SetAll(TileEntities, Tiles);
            world.Powerups.SetAll(PowerupEntities, Powerups);
        }
    }
}
