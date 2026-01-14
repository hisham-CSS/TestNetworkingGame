using System;
using System.Collections.Generic;
using Bomberman.Core.ECS.Components;
using Bomberman.Core.Input;
using Bomberman.Core;

namespace Bomberman.Core.Game.Systems
{
    /// <summary>
    /// System responsible for moving players based on input and handling collision detection with the map.
    /// </summary>
    public class MovementSystem
    {
        private World _world;
        private int _playerSpeedPerFrame;

        public MovementSystem(World world, int playerSpeedPerFrame)
        {
            _world = world;
            _playerSpeedPerFrame = playerSpeedPerFrame;
        }

        /// <summary>
        /// Updates player positions based on their inputs.
        /// Handles collision with solid blocks, destructible blocks, and bombs.
        /// Also handles powerup collection.
        /// </summary>
        public void Update(InputState[] inputs)
        {
            var players = _world.Players.GetAll();
            var playerEntities = _world.Players.GetEntities();
            var transforms = _world.Transforms.GetAll();
            var transformEntities = _world.Transforms.GetEntities();

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

                 IntVector2 velocity = input.Movement * _playerSpeedPerFrame;
                if (velocity != IntVector2.Zero)
                {
                    transform.Position = MoveWithCollision(transform, velocity);
                    transforms[transformIndex] = transform;
                    _world.Transforms.Set(transformIndex, transform);
                }

                // Powerup Pickup Collision
                var powerups = _world.Powerups.GetAll();
                var powerupEntities = _world.Powerups.GetEntities();
                var powerupTransforms = _world.Transforms.GetAll();
                var powerupTransformEntities = _world.Transforms.GetEntities();
                
                IntRect playerRect = new IntRect(transform.Position.X, transform.Position.Y, transform.Size.X, transform.Size.Y);

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
                    IntRect pRect = new IntRect(pTrans.Position.X, pTrans.Position.Y, pTrans.Size.X, pTrans.Size.Y);

                    if (playerRect.Intersects(pRect))
                    {
                        var playerComp = players[pIndex];
                        if (powerups[p].Type == PowerupComponent.PowerupType.Range) playerComp.BombRange++;
                        else if (powerups[p].Type == PowerupComponent.PowerupType.Capacity) playerComp.BombCapacity++;
                        
                        _world.Players.Set(pIndex, playerComp); 
                        eatenPowerups.Add(powerupEntities[p]);
                    }
                }

                 foreach(var ep in eatenPowerups)
                 {
                     _world.Powerups.Remove(ep);
                     _world.Transforms.Remove(ep);
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
            IntRect playerRect = new IntRect(_pos.X, _pos.Y, _size.X, _size.Y);
            IntRect currentRect = new IntRect(_currentPos.X, _currentPos.Y, _size.X, _size.Y);
            
            var tiles = _world.Tiles.GetAll();
            var tileTransforms = _world.Transforms.GetAll();

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Type == TileComponent.TileType.Solid || (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed))
                {
                    var tileTrans = tileTransforms[i];
                    IntRect tileRect = new IntRect(tileTrans.Position.X, tileTrans.Position.Y, tileTrans.Size.X, tileTrans.Size.Y);
                    
                    if (playerRect.Intersects(tileRect))
                        return true;
                }
            }
            
            var bombs = _world.Bombs.GetAll();
            var bombEntities = _world.Bombs.GetEntities();
             var transformEntities = _world.Transforms.GetEntities();
             var allTransforms = _world.Transforms.GetAll();
             
             for(int i=0; i<bombs.Count; i++)
             {
                 var bombEntity = bombEntities[i];
                 for(int t=0; t<transformEntities.Count; t++)
                 {
                     if(transformEntities[t].Equals(bombEntity))
                     {
                         var bombTrans = allTransforms[t];
                          IntRect bombRect = new IntRect(bombTrans.Position.X, bombTrans.Position.Y, bombTrans.Size.X, bombTrans.Size.Y);
                          
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
    }
}
