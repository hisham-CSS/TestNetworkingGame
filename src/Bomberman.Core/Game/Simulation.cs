using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Bomberman.Core
{
    public class Simulation : IGameSimulation<InputState>
    {
        public World World { get; private set; }

        /// <summary>Frames simulated so far. Advances each Update; restored with a snapshot. This is
        /// the single source of truth for "what frame are we on" (GameSession just reflects it).</summary>
        public int Frame { get; private set; }
        private const int MapWidth = 15;
        private const int MapHeight = 13;
        private const int TileSize = 32;

        public Simulation(int seed, int numPlayers = 1)
        {
            World = new World();
            GenerateMap(seed);
            SpawnPlayers(numPlayers);
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

        private void SpawnPlayers(int count)
        {
            var spawnPoints = new[]
            {
                new Vector2(1, 1),
                new Vector2(MapWidth - 2, 1),
                new Vector2(1, MapHeight - 2),
                new Vector2(MapWidth - 2, MapHeight - 2)
            };

            int n = Math.Clamp(count, 1, spawnPoints.Length);
            for (int i = 0; i < n; i++) // one player per corner (2 for networked lockstep)
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
            Frame++;
        }

        /// <summary>Snapshot the world at the current frame (Week 4).</summary>
        public GameStateSnapshot CaptureState() => GameStateSnapshot.Capture(World, Frame);

        /// <summary>Restore the world (and frame counter) from a snapshot (Week 4).</summary>
        public void RestoreState(GameStateSnapshot state)
        {
            state.Restore(World);
            Frame = state.Frame;
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

                // Powerup Pickup Collision
                // Naive O(N) check against all powerups
                var powerups = World.Powerups.GetAll();
                var powerupEntities = World.Powerups.GetEntities();
                var powerupTransforms = World.Transforms.GetAll();
                var powerupTransformEntities = World.Transforms.GetEntities();
                
                Rectangle playerRect = new Rectangle((int)transform.Position.X, (int)transform.Position.Y, (int)transform.Size.X, (int)transform.Size.Y);

                // Collect for removal to avoid concurrent modification
                List<Entity> eatenPowerups = new List<Entity>();

                for(int p=0; p<powerups.Count; p++)
                {
                    // Find transform for this powerup
                    // Optimization: In a real ECS we'd cache this or iterate differently
                    int pTransIdx = -1;
                    for(int pt=0; pt<powerupTransformEntities.Count; pt++) {
                        if (powerupTransformEntities[pt].Equals(powerupEntities[p])) {
                            pTransIdx = pt;
                            break;
                        }
                    }
                    if (pTransIdx == -1) continue;

                    var pTrans = powerupTransforms[pTransIdx];
                    Rectangle pRect = new Rectangle((int)pTrans.Position.X, (int)pTrans.Position.Y, (int)pTrans.Size.X, (int)pTrans.Size.Y);

                    if (playerRect.Intersects(pRect))
                    {
                        // Apply Effect
                        var playerComp = players[i];
                        if (powerups[p].Type == PowerupComponent.PowerupType.Range)
                        {
                            playerComp.BombRange++;
                        }
                        else if (powerups[p].Type == PowerupComponent.PowerupType.Capacity)
                        {
                            playerComp.BombCapacity++;
                        }
                        World.Players.Set(i, playerComp); // Update player stats

                        eatenPowerups.Add(powerupEntities[p]);
                    }
                }

                 foreach(var ep in eatenPowerups)
                 {
                     World.Powerups.Remove(ep);
                     World.Transforms.Remove(ep);
                 }


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
            // Snap center of player to grid
            Vector2 center = playerPosition + new Vector2(12, 12); // Assuming 24x24 player
            int gridX = (int)(center.X / TileSize);
            int gridY = (int)(center.Y / TileSize);
            
            Vector2 snapPos = new Vector2(gridX * TileSize, gridY * TileSize);

            // Check if bomb already exists there
            var bombs = World.Bombs.GetAll();
            var bombEntities = World.Bombs.GetEntities();
            var transformEntities = World.Transforms.GetEntities();
            var allTransforms = World.Transforms.GetAll();
            
            // Check Capacity
            int activeBombs = 0;
            for(int i=0; i<bombs.Count; i++)
            {
                if (bombs[i].OwnerId == player.PlayerId) activeBombs++;
            }

            if (activeBombs >= player.BombCapacity) return;

            for(int i=0; i<bombs.Count; i++)
            {
                var bombEntity = bombEntities[i];
                 for(int t=0; t<transformEntities.Count; t++)
                 {
                     if(transformEntities[t].Equals(bombEntity))
                     {
                         if(allTransforms[t].Position == snapPos) return; // Bomb exists
                         break;
                     }
                 }
            }

            // Spawn Bomb
            Entity bomb = World.CreateEntity();
            // Use Player Stats
            World.Bombs.Add(bomb, new BombComponent { Timer = 180, MaxTimer = 180, Range = player.BombRange, OwnerId = player.PlayerId });
            World.Transforms.Add(bomb, new TransformComponent { Position = snapPos, Size = new Vector2(TileSize, TileSize) });
        }

        private void UpdateBombs()
        {
            var bombList = World.Bombs.GetAll();
            var bombEntities = World.Bombs.GetEntities();
            
            // Snapshot phase: Collect bombs that need to explode
            // We store the Component (value type copy) and Entity (ID)
            List<(Entity entity, BombComponent component)> explosions = new List<(Entity, BombComponent)>();

            for (int i = 0; i < bombList.Count; i++)
            {
                var bomb = bombList[i];
                bomb.Timer--;
                World.Bombs.Set(i, bomb);

                if (bomb.Timer <= 0)
                {
                   explosions.Add((bombEntities[i], bomb));
                }
            }

            // Action phase: Explode and Remove
            // Since we extracted the data, we don't care about live indices anymore.
            // We just ask the world to remove the specific entities.
            foreach (var explosion in explosions)
            {
                Explode(explosion.entity, explosion.component);
                World.Bombs.Remove(explosion.entity);
                World.Transforms.Remove(explosion.entity);
            }
        }

        private void Explode(Entity bombEntity, BombComponent bombComp)
        {
             // Get Bomb Position
             var transformEntities = World.Transforms.GetEntities();
             var allTransforms = World.Transforms.GetAll();
             Vector2 bombPos = Vector2.Zero;
             
             for(int t=0; t<transformEntities.Count; t++) {
                 if(transformEntities[t].Equals(bombEntity)) {
                     bombPos = allTransforms[t].Position;
                     break;
                 }
             }

             // Create Center Explosion
             SpawnExplosion(bombPos);

             // Directions: Up, Down, Left, Right
             Vector2[] dirs = { new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0) };
             
             foreach(var dir in dirs)
             {
                 for(int r=1; r<=bombComp.Range; r++)
                 {
                     Vector2 checkPos = bombPos + (dir * r * TileSize);
                     if(ExplosionHit(checkPos)) break; // Stop propagation if hit something (wall/block)
                     SpawnExplosion(checkPos);
                 }
             }
        }
        
        private bool ExplosionHit(Vector2 pos)
        {
            // Check walls/boxes
            Rectangle checkRect = new Rectangle((int)pos.X + 2, (int)pos.Y + 2, TileSize - 4, TileSize - 4); // Small shrink to avoid edge cases
            
            var tiles = World.Tiles.GetAll();
            var tileTransforms = World.Transforms.GetAll();
            var tileEntities = World.Tiles.GetEntities();

            for (int i = 0; i < tiles.Count; i++)
            {
                var tPos = tileTransforms[i].Position;
                Rectangle tileRect = new Rectangle((int)tPos.X, (int)tPos.Y, TileSize, TileSize);
                
                if (checkRect.Intersects(tileRect))
                {
                    if (tiles[i].Type == TileComponent.TileType.Solid) return true; // Stop
                    
                    if (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed)
                    {
                         var tile = tiles[i];
                        tile.Destroyed = true; // Destroy it
                        World.Tiles.Set(i, tile);
                        
                        // Drop Powerup if seeded
                        if (tile.HiddenPowerup != PowerupComponent.PowerupType.None)
                        {
                            SpawnPowerup(tileTransforms[i].Position, tile.HiddenPowerup);
                        }

                        return true; // Stop after destroying one
                    }
                }
            }
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
