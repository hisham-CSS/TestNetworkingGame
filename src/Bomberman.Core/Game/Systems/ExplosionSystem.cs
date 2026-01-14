using System;
using System.Collections.Generic;
using Bomberman.Core.ECS.Components;
using Bomberman.Core;

namespace Bomberman.Core.Game.Systems
{
    /// <summary>
    /// System responsible for spawning explosion entities and handling tile destruction.
    /// </summary>
    public class ExplosionSystem
    {
        private World _world;
        private int _scaledTileSize;
        private int _subpixelScale;

        public ExplosionSystem(World world, int scaledTileSize, int subpixelScale)
        {
            _world = world;
            _scaledTileSize = scaledTileSize;
            _subpixelScale = subpixelScale;
        }

        /// <summary>
        /// Creates explosion entities radiating from the origin.
        /// Handles propagation logic (stopping at solid blocks/walls).
        /// </summary>
        public void TriggerExplosion(IntVector2 origin, int range)
        {
             SpawnExplosion(origin);

             IntVector2[] dirs = { new IntVector2(0, -1), new IntVector2(0, 1), new IntVector2(-1, 0), new IntVector2(1, 0) };
             
             foreach(var dir in dirs)
             {
                 for(int r=1; r<=range; r++)
                 {
                     IntVector2 checkPos = origin + (dir * r * _scaledTileSize);
                     if(ExplosionHit(checkPos)) break; 
                     SpawnExplosion(checkPos);
                 }
             }
        }

        private void SpawnExplosion(IntVector2 pos)
        {
            var exp = _world.CreateEntity();
            _world.Explosions.Add(exp, new ExplosionComponent { Timer = 30, MaxTimer = 30 });
            _world.Transforms.Add(exp, new TransformComponent 
            { 
                Position = pos, 
                Size = new IntVector2(_scaledTileSize, _scaledTileSize) 
            });
        }

        private bool ExplosionHit(IntVector2 pos)
        {
            IntRect checkRect = new IntRect(pos.X + 2*_subpixelScale, pos.Y + 2*_subpixelScale, _scaledTileSize - 4*_subpixelScale, _scaledTileSize - 4*_subpixelScale);
            
            var tiles = _world.Tiles.GetAll();
            var tileTransforms = _world.Transforms.GetAll();

            for (int i = 0; i < tiles.Count; i++)
            {
                var tPos = tileTransforms[i].Position;
                IntRect tileRect = new IntRect(tPos.X, tPos.Y, _scaledTileSize, _scaledTileSize);
                
                if (checkRect.Intersects(tileRect))
                {
                    if (tiles[i].Type == TileComponent.TileType.Solid) return true; 
                    
                    if (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed)
                    {
                         var tile = tiles[i];
                        tile.Destroyed = true; 
                        _world.Tiles.Set(i, tile);
                        
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
            var p = _world.CreateEntity();
            _world.Powerups.Add(p, new PowerupComponent { Type = type });
            _world.Transforms.Add(p, new TransformComponent 
            { 
                Position = pos + new IntVector2(8*_subpixelScale, 8*_subpixelScale), 
                Size = new IntVector2(16*_subpixelScale, 16*_subpixelScale) 
            });
        }

        public void Update()
        {
             var list = _world.Explosions.GetAll();
             var entities = _world.Explosions.GetEntities();
             
             List<Entity> toRemove = new List<Entity>();

             for(int i = 0; i < list.Count; i++)
             {
                 var exp = list[i];
                 exp.Timer--;
                 _world.Explosions.Set(i, exp);
                 
                 if(exp.Timer <= 0)
                 {
                     toRemove.Add(entities[i]);
                 }
             }
             
             foreach(var entity in toRemove)
             {
                 _world.Explosions.Remove(entity);
                 _world.Transforms.Remove(entity);
             }
        }
    }
}
