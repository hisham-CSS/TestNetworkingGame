using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Bomberman
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
                    Vector2 pos = world.Transforms.Get(e).Position;
                    // Quantize to avoid float issues, though strictly we should be deterministic
                    hash = Combine(hash, (int)(pos.X * 100));
                    hash = Combine(hash, (int)(pos.Y * 100));
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
                    Vector2 pos = world.Transforms.Get(e).Position;
                    hash = Combine(hash, (int)Math.Round(pos.X)); // Bombs snap to grid usually
                    hash = Combine(hash, (int)Math.Round(pos.Y));
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

        // Hash from Snapshot
        public static int Hash(GameStateSnapshot snap)
        {
            int hash = 0;

                // 1. Players
            for (int i = 0; i < snap.Players.Count; i++)
            {
                var p = snap.Players[i];
                hash = Combine(hash, (int)p.PlayerId);
                hash = Combine(hash, p.Alive ? 1 : 0);
                hash = Combine(hash, p.BombRange); // Hash Stats
                hash = Combine(hash, p.BombCapacity);
                
                // Need to find transform for this player
                // Iterate transform entities to find matching ID
                // Note: Entity struct equality uses Index
                Entity pEntity = snap.PlayerEntities[i];
                
                int tIndex = -1;
                for(int k=0; k<snap.TransformEntities.Count; k++)
                {
                    if (snap.TransformEntities[k].Index == pEntity.Index)
                    {
                        tIndex = k;
                        break;
                    }
                }
                
                if (tIndex != -1)
                {
                   Vector2 pos = snap.Transforms[tIndex].Position;
                   hash = Combine(hash, (int)(pos.X * 100));
                   hash = Combine(hash, (int)(pos.Y * 100));
                }
            }

             // 2. Bombs
            hash = Combine(hash, snap.Bombs.Count);
            for (int i = 0; i < snap.Bombs.Count; i++)
            {
                var b = snap.Bombs[i];
                hash = Combine(hash, (int)b.OwnerId);
                hash = Combine(hash, b.Timer);
                
                 Entity bEntity = snap.BombEntities[i];
                 int tIndex = -1;
                 for(int k=0; k<snap.TransformEntities.Count; k++)
                 {
                    if (snap.TransformEntities[k].Index == bEntity.Index)
                    {
                        tIndex = k;
                        break;
                    }
                 }
                
                 if (tIndex != -1)
                {
                   Vector2 pos = snap.Transforms[tIndex].Position;
                   hash = Combine(hash, (int)Math.Round(pos.X));
                   hash = Combine(hash, (int)Math.Round(pos.Y));
                }
            }

            // 3. Tiles
            hash = Combine(hash, snap.Tiles.Count);
            // Hash state of tiles (Destroyed) to ensure crate sync
            for(int i=0; i<snap.Tiles.Count; i++)
            {
                if (snap.Tiles[i].Type == TileComponent.TileType.Destructible)
                {
                    hash = Combine(hash, snap.Tiles[i].Destroyed ? 1 : 0);
                }
            }
            
            // 4. Explosions
            hash = Combine(hash, snap.Explosions.Count);

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
