using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Bomberman.Core.Game;

namespace Bomberman.Core
{
    public class Simulation
    {
        public World World { get; private set; }
        public Action<string>? Log; // Debug Logger

        private const int MapWidth = 15;
        private const int MapHeight = 13;
        private const int TileSize = 32;
        public const int SubpixelScale = 100; // 1 unit = 0.01 pixel
        private const int ScaledTileSize = TileSize * SubpixelScale;

        // Speed: 150 pixels/sec -> 15000 units/sec
        // At 60 FPS: 250 units/frame
        private const int PlayerSpeedPerFrame = 250; 

        public Simulation(int seed, int playerCount)
        {
            World = new World();
            GenerateMap(seed);
            SpawnPlayers(playerCount);
        }

        private void GenerateMap(int seed)
        {
            // Use DeterministicRandom
            var random = new DeterministicRandom(seed);

            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    Entity tile = World.CreateEntity();
                    var transform = new TransformComponent
                    {
                        Position = new IntVector2(x * ScaledTileSize, y * ScaledTileSize),
                        Size = new IntVector2(ScaledTileSize, ScaledTileSize)
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
            // Spawn points in Grid Coordinates
            var spawnPoints = new[]
            {
                new Point(1, 1),
                new Point(MapWidth - 2, 1),
                new Point(1, MapHeight - 2),
                new Point(MapWidth - 2, MapHeight - 2)
            };

            for (int i = 0; i < count; i++) 
            {
                var player = World.CreateEntity();
                World.Players.Add(player, new PlayerComponent { PlayerId = (uint)i, Alive = true, BombRange = 1, BombCapacity = 1 });
                
                // 24x24 pixels -> 2400x2400 units
                int playerSize = 24 * SubpixelScale;
                
                // Center in tile: TileStart + (TileSize - PlayerSize)/2
                // But simplified: Just place top-left for now relative to tile?
                // Old code: spawnPoints[i] * TileSize
                // That puts (0,0) of player at (0,0) of tile.
                // If player is smaller (24) and tile is 32, it's top-left aligned in the tile?
                // Visuals might look offset, but logic was consistent.
                // Let's keep it simple: Position = Grid * ScaledTileSize.
                // Centering adjustment: (3200 - 2400) / 2 = 400 offset.
                
                int startX = spawnPoints[i].X * ScaledTileSize + (ScaledTileSize - playerSize) / 2;
                int startY = spawnPoints[i].Y * ScaledTileSize + (ScaledTileSize - playerSize) / 2;

                World.Transforms.Add(player, new TransformComponent 
                { 
                    Position = new IntVector2(startX, startY), 
                    Size = new IntVector2(playerSize, playerSize)
                });
            }
        }

        public void Update(InputState[] inputs, float dt)
        {
            // Ignore dt for movement if we assume fixed step, BUT we should verify.
            // Just use PlayerSpeedPerFrame.
            UpdatePlayers(inputs);
            UpdateBombs();
            UpdateExplosions();
            CheckDamage();
        }

        private void CheckDamage()
        {
            var players = World.Players.GetAll();
            var playerTransforms = World.Transforms.GetAll(); 
            var playerEntities = World.Players.GetEntities();
            var playerTransformEntities = World.Transforms.GetEntities();
            
            var explosions = World.Explosions.GetAll();
            if (explosions.Count == 0) return;
            var explosionTransforms = World.Transforms.GetAll();
            var explosionEntities = World.Transforms.GetEntities();
            var explosionCompEntities = World.Explosions.GetEntities(); 

            // Optimization: Cache explosion rects (in scaled units)
            // Use MonoGame Rectangle (int based) which fits perfectly!
            List<Rectangle> expRects = new List<Rectangle>();
            for(int i=0; i<explosions.Count; i++) 
            {
                 var entity = explosionCompEntities[i];
                 for(int t=0; t<explosionEntities.Count; t++) {
                     if (explosionEntities[t].Equals(entity)) {
                         var trans = explosionTransforms[t];
                         // Shrink 4 pixels = 400 units
                         int shrink = 4 * SubpixelScale;
                         expRects.Add(new Rectangle(trans.Position.X + shrink, trans.Position.Y + shrink, trans.Size.X - shrink*2, trans.Size.Y - shrink*2));
                         break;
                     }
                 }
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Alive) continue;

                var pEntity = playerEntities[i];
                TransformComponent pTrans = new TransformComponent();
                bool found = false;
                for(int t=0; t<playerTransformEntities.Count; t++) {
                    if (playerTransformEntities[t].Equals(pEntity)) {
                        pTrans = playerTransforms[t];
                        found = true;
                        break;
                    }
                }
                if (!found) continue;

                Rectangle pRect = new Rectangle(pTrans.Position.X, pTrans.Position.Y, pTrans.Size.X, pTrans.Size.Y);

                foreach(var eRect in expRects)
                {
                    if (pRect.Intersects(eRect))
                    {
                        var p = players[i];
                        p.Alive = false;
                        World.Players.Set(i, p);
                        Console.WriteLine($"Player {p.PlayerId} Died!");
                        break;
                    }
                }
            }
        }

        private void UpdatePlayers(InputState[] inputs)
        {
            var players = World.Players.GetAll();
            var playerEntities = World.Players.GetEntities();
            var transforms = World.Transforms.GetAll();
            var transformEntities = World.Transforms.GetEntities();

            for (int id = 0; id < inputs.Length; id++)
            {
                int pIndex = -1;
                for(int k=0; k<players.Count; k++)
                {
                    if (players[k].PlayerId == id && players[k].Alive)
                    {
                        pIndex = k;
                        break;
                    }
                }
                
                if (pIndex == -1) continue; 

                var input = inputs[id];
                var playerEntity = playerEntities[pIndex];

                int transformIndex = -1;
                for(int t=0; t<transformEntities.Count; t++) {
                    if (transformEntities[t].Equals(playerEntity)) {
                        transformIndex = t;
                        break;
                    }
                }
                
                if (transformIndex == -1) continue;

                var transform = transforms[transformIndex];

                // Movement (Integer)
                IntVector2 velocity = input.Movement * PlayerSpeedPerFrame;
                if (velocity != IntVector2.Zero)
                {
                    transform.Position = MoveWithCollision(transform, velocity);
                }

                transforms[transformIndex] = transform;

                // Powerup Pickup Collision
                var powerups = World.Powerups.GetAll();
                var powerupEntities = World.Powerups.GetEntities();
                var powerupTransforms = World.Transforms.GetAll();
                var powerupTransformEntities = World.Transforms.GetEntities();
                
                Rectangle playerRect = new Rectangle(transform.Position.X, transform.Position.Y, transform.Size.X, transform.Size.Y);

                List<Entity> eatenPowerups = new List<Entity>();

                for(int p=0; p<powerups.Count; p++)
                {
                    int pTransIdx = -1;
                    for(int pt=0; pt<powerupTransformEntities.Count; pt++) {
                         if (powerupTransformEntities[pt].Equals(powerupEntities[p])) {
                             pTransIdx = pt;
                             break;
                         }
                    }
                    if (pTransIdx == -1) continue;

                    var pTrans = powerupTransforms[pTransIdx];
                    Rectangle pRect = new Rectangle(pTrans.Position.X, pTrans.Position.Y, pTrans.Size.X, pTrans.Size.Y);

                    if (playerRect.Intersects(pRect))
                    {
                        var playerComp = players[pIndex];
                        if (powerups[p].Type == PowerupComponent.PowerupType.Range) playerComp.BombRange++;
                        else if (powerups[p].Type == PowerupComponent.PowerupType.Capacity) playerComp.BombCapacity++;
                        
                        World.Players.Set(pIndex, playerComp); 
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
                    TryPlaceBomb(input.BombTarget, players[pIndex], playerEntity);
                }
            }
        }

        private IntVector2 MoveWithCollision(TransformComponent transform, IntVector2 velocity)
        {
            IntVector2 currentPos = transform.Position;

            // Resolve X
            IntVector2 newPos = transform.Position + new IntVector2(velocity.X, 0);
            if (CheckCollision(newPos, transform.Size, currentPos))
            {
                // Simple slide
            }
            else
            {
                transform.Position = newPos;
            }

            // Resolve Y
            newPos = transform.Position + new IntVector2(0, velocity.Y);
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

        private bool CheckCollision(IntVector2 _pos, IntVector2 _size, IntVector2 _currentPos)
        {
            Rectangle playerRect = new Rectangle(_pos.X, _pos.Y, _size.X, _size.Y);
            Rectangle currentRect = new Rectangle(_currentPos.X, _currentPos.Y, _size.X, _size.Y);
            
            var tiles = World.Tiles.GetAll();
            var tileTransforms = World.Transforms.GetAll();

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Type == TileComponent.TileType.Solid || (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed))
                {
                    var tileTrans = tileTransforms[i];
                    Rectangle tileRect = new Rectangle(tileTrans.Position.X, tileTrans.Position.Y, tileTrans.Size.X, tileTrans.Size.Y);
                    
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
                          Rectangle bombRect = new Rectangle(bombTrans.Position.X, bombTrans.Position.Y, bombTrans.Size.X, bombTrans.Size.Y);
                          
                          // Walking-off-bomb logic
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

        private void TryPlaceBomb(Point targetGrid, PlayerComponent player, Entity playerEntity)
        {
            int gridX = targetGrid.X;
            int gridY = targetGrid.Y;
            
            IntVector2 snapPos = new IntVector2(gridX * ScaledTileSize, gridY * ScaledTileSize);

            // Check boundaries
            if (gridX < 0 || gridX >= MapWidth || gridY < 0 || gridY >= MapHeight) return;

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
                         if(allTransforms[t].Position == snapPos) 
                         {
                             return; // Bomb exists
                         }
                         break;
                     }
                 }
            }

            // Spawn Bomb
            Entity bomb = World.CreateEntity();
            World.Bombs.Add(bomb, new BombComponent { Timer = 180, MaxTimer = 180, Range = player.BombRange, OwnerId = player.PlayerId });
            World.Transforms.Add(bomb, new TransformComponent 
            { 
                Position = snapPos, 
                Size = new IntVector2(ScaledTileSize, ScaledTileSize) 
            });
            
            Log?.Invoke($"[Bomb] P{player.PlayerId} at {gridX},{gridY}");
        }

        private void UpdateBombs()
        {
            var bombList = World.Bombs.GetAll();
            var bombEntities = World.Bombs.GetEntities();
            
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

            foreach (var explosion in explosions)
            {
                Explode(explosion.entity, explosion.component);
                World.Bombs.Remove(explosion.entity);
                World.Transforms.Remove(explosion.entity);
            }
        }

        private void Explode(Entity bombEntity, BombComponent bombComp)
        {
             var transformEntities = World.Transforms.GetEntities();
             var allTransforms = World.Transforms.GetAll();
             IntVector2 bombPos = IntVector2.Zero;
             
             for(int t=0; t<transformEntities.Count; t++) {
                 if(transformEntities[t].Equals(bombEntity)) {
                     bombPos = allTransforms[t].Position;
                     break;
                 }
             }

             SpawnExplosion(bombPos);

             IntVector2[] dirs = { new IntVector2(0, -1), new IntVector2(0, 1), new IntVector2(-1, 0), new IntVector2(1, 0) };
             
             foreach(var dir in dirs)
             {
                 for(int r=1; r<=bombComp.Range; r++)
                 {
                     IntVector2 checkPos = bombPos + (dir * r * ScaledTileSize);
                     if(ExplosionHit(checkPos)) break; 
                     SpawnExplosion(checkPos);
                 }
             }
        }
        
        private bool ExplosionHit(IntVector2 pos)
        {
            Rectangle checkRect = new Rectangle(pos.X + 2*SubpixelScale, pos.Y + 2*SubpixelScale, ScaledTileSize - 4*SubpixelScale, ScaledTileSize - 4*SubpixelScale);
            
            var tiles = World.Tiles.GetAll();
            var tileTransforms = World.Transforms.GetAll();

            for (int i = 0; i < tiles.Count; i++)
            {
                var tPos = tileTransforms[i].Position;
                Rectangle tileRect = new Rectangle(tPos.X, tPos.Y, ScaledTileSize, ScaledTileSize);
                
                if (checkRect.Intersects(tileRect))
                {
                    if (tiles[i].Type == TileComponent.TileType.Solid) return true; 
                    
                    if (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed)
                    {
                         var tile = tiles[i];
                        tile.Destroyed = true; 
                        World.Tiles.Set(i, tile);
                        
                        if (tile.HiddenPowerup != PowerupComponent.PowerupType.None)
                        {
                            SpawnPowerup(tileTransforms[i].Position, tile.HiddenPowerup);
                        }

                        return true; 
                    }
                }
            }
            return false;
        }

        private void SpawnPowerup(IntVector2 pos, PowerupComponent.PowerupType type)
        {
            var p = World.CreateEntity();
            World.Powerups.Add(p, new PowerupComponent { Type = type });
            World.Transforms.Add(p, new TransformComponent 
            { 
                Position = pos + new IntVector2(8*SubpixelScale, 8*SubpixelScale), 
                Size = new IntVector2(16*SubpixelScale, 16*SubpixelScale) 
            });
        }

        private void SpawnExplosion(IntVector2 pos)
        {
            var exp = World.CreateEntity();
            World.Explosions.Add(exp, new ExplosionComponent { Timer = 30, MaxTimer = 30 });
            World.Transforms.Add(exp, new TransformComponent 
            { 
                Position = pos, 
                Size = new IntVector2(ScaledTileSize, ScaledTileSize) 
            });
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
