using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Bomberman
{
    public class Simulation
    {
        public World World { get; private set; }
        private const int MapWidth = 15;
        private const int MapHeight = 13;
        private const int TileSize = 32;

        public Simulation(int seed)
        {
            World = new World();
            GenerateMap(seed);
            SpawnPlayers();
        }

        private void GenerateMap(int seed)
        {
            var random = new Random(seed);

            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    Entity tile = World.CreateEntity();
                    var transform = new TransformComponent
                    {
                        Position = new Vector2(x * TileSize, y * TileSize),
                        Size = new Vector2(TileSize, TileSize)
                    };
                    
                    TileComponent.TileType type = TileComponent.TileType.Empty;
                    PowerupComponent.PowerupType hiddenPowerup = PowerupComponent.PowerupType.None;

                    // Borders
                    if (y == 0 || y == MapHeight - 1 || x == 0 || x == MapWidth - 1)
                    {
                        type = TileComponent.TileType.Solid;
                    }
                    // Fixed Pillars (Checkerboard)
                    else if (x % 2 == 0 && y % 2 == 0)
                    {
                        type = TileComponent.TileType.Solid;
                    }
                    // Random Crates (avoiding spawn corners)
                    else if (!IsSpawnZone(x, y) && random.NextDouble() < 0.6)
                    {
                        type = TileComponent.TileType.Destructible;
                        
                        // Seed Powerup (e.g. 30% chance)
                        if (random.NextDouble() < 0.3)
                        {
                            // 50/50 split for now
                            hiddenPowerup = random.NextDouble() < 0.5 ? PowerupComponent.PowerupType.Range : PowerupComponent.PowerupType.Capacity;
                        }
                    }

                    World.Tiles.Add(tile, new TileComponent { Type = type, HiddenPowerup = hiddenPowerup });
                    World.Transforms.Add(tile, transform);
                }
            }
        }

        private bool IsSpawnZone(int x, int y)
        {
            // Corners: (1,1), (13,1), (1,11), (13,11)
            // Safe zone radius of 2 tiles roughly
            if ((x <= 2 && y <= 2) || (x >= MapWidth - 3 && y <= 2) ||
                (x <= 2 && y >= MapHeight - 3) || (x >= MapWidth - 3 && y >= MapHeight - 3))
                return true;
            return false;
        }

        private void SpawnPlayers()
        {
            var spawnPoints = new[]
            {
                new Vector2(1, 1),
                new Vector2(MapWidth - 2, 1),
                new Vector2(1, MapHeight - 2),
                new Vector2(MapWidth - 2, MapHeight - 2)
            };

            for (int i = 0; i < 1; i++) // Just 1 player for now, extensible to 4
            {
                var player = World.CreateEntity();
                World.Players.Add(player, new PlayerComponent { PlayerId = (uint)i, Alive = true, BombRange = 1, BombCapacity = 1 });
                World.Transforms.Add(player, new TransformComponent 
                { 
                    Position = spawnPoints[i] * TileSize, 
                    Size = new Vector2(24, 24) // Slightly smaller than tile for ease of movement
                });
            }
        }

        public void Update(InputState[] inputs, float dt)
        {
            UpdatePlayers(inputs, dt);
            UpdateBombs();
            UpdateExplosions();
            CheckPlayerDeaths(); // lose condition: an explosion overlapping a player kills it
        }

        /// <summary>True while at least one player is still alive (game continues).</summary>
        public bool AnyPlayerAlive()
        {
            var players = World.Players.GetAll();
            for (int i = 0; i < players.Count; i++) if (players[i].Alive) return true;
            return false;
        }

        /// <summary>Lose condition: if an active explosion overlaps a living player, the player dies.</summary>
        private void CheckPlayerDeaths()
        {
            // TODO (LA1 - Lose condition): if an active explosion overlaps a living player,
            //  set that player's Alive = false (World.Players.Set). Use Rectangle.Intersects.
        }

        private void UpdatePlayers(InputState[] inputs, float dt)
        {
            var players = World.Players.GetAll();
            var playerEntities = World.Players.GetEntities();
            var transforms = World.Transforms.GetAll();
            var transformEntities = World.Transforms.GetEntities();

            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Alive || i >= inputs.Length) continue;

                var input = inputs[i];
                var playerEntity = playerEntities[i];

                // Find Transform (inefficient linear search, optimize later with a map or cache)
                int transformIndex = -1;
                for(int t=0; t<transformEntities.Count; t++) {
                    if (transformEntities[t].Equals(playerEntity)) {
                        transformIndex = t;
                        break;
                    }
                }
                
                if (transformIndex == -1) continue;

                var transform = transforms[transformIndex];

                // Movement
                float speed = 150f;
                Vector2 velocity = input.Movement * speed * dt;
                if (velocity != Vector2.Zero)
                {
                    transform.Position = MoveWithCollision(transform, velocity);
                }

                transforms[transformIndex] = transform;

                // TODO (LA1 - Gameplay): powerup pickup.
                //  - Build the player's AABB (Rectangle) from its transform.
                //  - For each powerup, find its transform and test Intersects.
                //  - On overlap: Range -> BombRange++, Capacity -> BombCapacity++ (World.Players.Set),
                //    then remove the powerup entity (from Powerups and Transforms).

                // Bomb Placement
                if (input.PlaceBomb)
                {
                    TryPlaceBomb(transform.Position, players[i], playerEntity);
                }
            }
        }

        private Vector2 MoveWithCollision(TransformComponent transform, Vector2 velocity)
        {
            // Current position rect for "Am I inside a bomb?" check
            Vector2 currentPos = transform.Position;

            // Resolve X
            Vector2 newPos = transform.Position + new Vector2(velocity.X, 0);
            if (CheckCollision(newPos, transform.Size, currentPos))
            {
                // Simple slide
            }
            else
            {
                transform.Position = newPos;
            }

            // Resolve Y
            newPos = transform.Position + new Vector2(0, velocity.Y);
            if (CheckCollision(newPos, transform.Size, currentPos))
            {
                // Simple slide
            }
            else
            {
                transform.Position = newPos;
            }

            return transform.Position;
        }

        private bool CheckCollision(Vector2 _pos, Vector2 _size, Vector2 _currentPos)
        {
            Rectangle playerRect = new Rectangle((int)_pos.X, (int)_pos.Y, (int)_size.X, (int)_size.Y);
            Rectangle currentRect = new Rectangle((int)_currentPos.X, (int)_currentPos.Y, (int)_size.X, (int)_size.Y);
            
            var tiles = World.Tiles.GetAll();
            var tileTransforms = World.Transforms.GetAll();

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Type == TileComponent.TileType.Solid || (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed))
                {
                    var tileTrans = tileTransforms[i];
                    Rectangle tileRect = new Rectangle((int)tileTrans.Position.X, (int)tileTrans.Position.Y, (int)tileTrans.Size.X, (int)tileTrans.Size.Y);
                    
                    if (playerRect.Intersects(tileRect))
                        return true;
                }
            }
            
            var bombs = World.Bombs.GetAll();
            var bombEntities = World.Bombs.GetEntities();
             var transformEntities = World.Transforms.GetEntities();
             var allTransforms = World.Transforms.GetAll();
             
             for(int i=0; i<bombs.Count; i++)
             {
                 var bombEntity = bombEntities[i];
                 for(int t=0; t<transformEntities.Count; t++)
                 {
                     if(transformEntities[t].Equals(bombEntity))
                     {
                         var bombTrans = allTransforms[t];
                          Rectangle bombRect = new Rectangle((int)bombTrans.Position.X, (int)bombTrans.Position.Y, (int)bombTrans.Size.X, (int)bombTrans.Size.Y);
                          
                          // Fix: If we are CURRENTLY intersecting this bomb, ignore collision.
                          // This allows walking off the bomb, but prevents walking back onto it.
                          if (currentRect.Intersects(bombRect))
                          {
                              continue; 
                          }

                          if (playerRect.Intersects(bombRect))
                            return true; 
                         break;
                     }
                 }
             }

            return false;
        }

        private void TryPlaceBomb(Vector2 playerPosition, PlayerComponent player, Entity playerEntity)
        {
            // TODO (LA1 - Gameplay): place a bomb for this player.
            //  1. Respect BombCapacity (count this owner's active bombs first).
            //  2. Snap the bomb to the tile grid under the player's center.
            //  3. Don't stack two bombs on the same tile.
            //  4. Spawn a bomb entity (World.Bombs.Add + World.Transforms.Add) with
            //     Timer = 180, Range = player.BombRange, OwnerId = player.PlayerId.
        }

        private void UpdateBombs()
        {
            // TODO (LA1 - Gameplay): tick bombs and trigger explosions.
            //  - Decrement each bomb's Timer (World.Bombs.Set).
            //  - When Timer <= 0, call Explode(entity, bomb), then remove the bomb
            //    (collect expired bombs first to avoid mutating the pool while iterating).
        }

        private void Explode(Entity bombEntity, BombComponent bombComp)
        {
            // TODO (LA1 - Gameplay): explosion propagation.
            //  - Spawn a center explosion at the bomb position (SpawnExplosion).
            //  - For each cardinal direction, walk outward up to bombComp.Range:
            //      if ExplosionHit(pos) is true (wall/crate) stop; otherwise SpawnExplosion(pos).
        }
        
        private bool ExplosionHit(Vector2 pos)
        {
            // TODO (LA1 - Gameplay): return true if the blast is blocked at pos.
            //  - Solid tile -> return true.
            //  - Destructible crate -> mark Destroyed, drop HiddenPowerup if any, return true.
            //  - Otherwise the blast continues -> return false.
            return false;
        }

        private void SpawnPowerup(Vector2 pos, PowerupComponent.PowerupType type)
        {
            var p = World.CreateEntity();
            World.Powerups.Add(p, new PowerupComponent { Type = type });
            // Powerup is slightly smaller than tile
            World.Transforms.Add(p, new TransformComponent { Position = pos + new Vector2(8, 8), Size = new Vector2(16, 16) });
        }

        private void SpawnExplosion(Vector2 pos)
        {
            var exp = World.CreateEntity();
            World.Explosions.Add(exp, new ExplosionComponent { Timer = 30, MaxTimer = 30 });
            World.Transforms.Add(exp, new TransformComponent { Position = pos, Size = new Vector2(TileSize, TileSize) });
        }
        
        private void UpdateExplosions()
        {
             var list = World.Explosions.GetAll();
             var entities = World.Explosions.GetEntities();
             
             List<Entity> toRemove = new List<Entity>();

             for(int i = 0; i < list.Count; i++)
             {
                 var exp = list[i];
                 exp.Timer--;
                 World.Explosions.Set(i, exp);
                 
                 if(exp.Timer <= 0)
                 {
                     toRemove.Add(entities[i]);
                 }
             }
             
             foreach(var entity in toRemove)
             {
                 World.Explosions.Remove(entity);
                 World.Transforms.Remove(entity);
             }
        }
    }
}
