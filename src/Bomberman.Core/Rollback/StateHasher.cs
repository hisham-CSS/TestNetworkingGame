using System;
using Bomberman.Core.ECS.Components;
using System.Collections.Generic;

using Bomberman.Core;

namespace Bomberman.Core.Rollback
{
    public static class StateHasher
    {
        // Simple Jenkins One-at-a-time hash or similar lightweight checksum
        public static int Hash(World world)
        {
            int hash = 0;

            // 1. Players (Id, Position, Alive)
            var players = world.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players.Get(i);
                hash = Combine(hash, (int)p.PlayerId);
                hash = Combine(hash, p.Alive ? 1 : 0);
                hash = Combine(hash, p.BombRange);
                hash = Combine(hash, p.BombCapacity);
                
                // Get Position
                Entity e = players.GetEntity(i);
                if (world.Transforms.Has(e))
                {
                    IntVector2 pos = world.Transforms.Get(e).Position;
                    hash = Combine(hash, pos.X);
                    hash = Combine(hash, pos.Y);
                }
            }

            // 2. Bombs (Owner, Timer, Position)
            var bombs = world.Bombs;
            hash = Combine(hash, bombs.Count); // Count matters
            for (int i = 0; i < bombs.Count; i++)
            {
                var b = bombs.Get(i);
                hash = Combine(hash, (int)b.OwnerId);
                hash = Combine(hash, b.Timer);
                
                Entity e = bombs.GetEntity(i);
                if (world.Transforms.Has(e))
                {
                    IntVector2 pos = world.Transforms.Get(e).Position;
                    hash = Combine(hash, pos.X);
                    hash = Combine(hash, pos.Y);
                }
            }

            // 3. Tiles (Destroyable or not) - Only need to hash count/existence of soft blocks
            // Assuming Hard Blocks never change, we only care about Soft Blocks (crates)
            var tiles = world.Tiles;
            hash = Combine(hash, tiles.Count);
            var tileList = tiles.GetAll();
            for(int i=0; i<tileList.Count; i++)
            {
                 if (tileList[i].Type == TileComponent.TileType.Destructible)
                {
                    hash = Combine(hash, tileList[i].Destroyed ? 1 : 0);
                }
            }
            
            // 4. Explosions (Timer)
            var explosions = world.Explosions;
            hash = Combine(hash, explosions.Count); 
            // Maybe hash individual explosion timers if specific sync needed, 
            // but count usually sufficient to catch "Explosion didn't happen"

            return hash;
        }

        public static int Hash(GameStateSnapshot snap)
        {
            int hash = 0;

            // 1. Transforms
            var transforms = snap.GetState<TransformComponent>();
            for(int i=0; i<transforms.components.Count; i++)
            {
                // Hash Position (Bitwise Deterministic)
                IntVector2 pos = transforms.components[i].Position;
                hash = Combine(hash, pos.X);
                hash = Combine(hash, pos.Y);
            }

            // 2. Players
            var players = snap.GetState<PlayerComponent>();
            for(int i=0; i<players.components.Count; i++)
            {
                var p = players.components[i];
                hash = Combine(hash, (int)p.PlayerId);
                hash = Combine(hash, p.Alive ? 1 : 0);
                hash = Combine(hash, p.BombRange);
                hash = Combine(hash, p.BombCapacity);
            }

            // 3. Bombs
            var bombs = snap.GetState<BombComponent>();
            hash = Combine(hash, bombs.components.Count);
            for(int i=0; i<bombs.components.Count; i++)
            {
                var b = bombs.components[i];
                hash = Combine(hash, (int)b.OwnerId);
                hash = Combine(hash, b.Timer);
            }
            
            // 4. Tiles
            var tiles = snap.GetState<TileComponent>();
            hash = Combine(hash, tiles.components.Count);
            for(int i=0; i<tiles.components.Count; i++)
            {
                if (tiles.components[i].Type == TileComponent.TileType.Destructible)
                {
                    hash = Combine(hash, tiles.components[i].Destroyed ? 1 : 0);
                }
            }
            
            // 5. Explosions
            var explosions = snap.GetState<ExplosionComponent>();
            hash = Combine(hash, explosions.components.Count);

            return hash;
        }

        private static int Combine(int hash, int value)
        {
            // Jenkins one-at-a-time hash mix
            hash += value;
            hash += (hash << 10);
            hash ^= (hash >> 6);
            return hash;
        }
        
        // Finalize (optional for simple checks, but good for distribution)
        public static int Finalize(int hash)
        {
            hash += (hash << 3);
            hash ^= (hash >> 11);
            hash += (hash << 15);
            return hash;
        }
    }
}
