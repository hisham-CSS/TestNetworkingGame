using System;
using System.Collections.Generic;
using Bomberman.Core.ECS.Components;
using Bomberman.Core.Input;
using Bomberman.Core;

namespace Bomberman.Core.Game.Systems
{
    /// <summary>
    /// System responsible for handling bomb placement logic and bomb timers.
    /// </summary>
    public class BombSystem
    {
        private World _world;
        private Action<string>? _logger;
        private int _mapWidth;
        private int _mapHeight;
        private int _scaledTileSize;

        /// <summary>
        /// Event data describing an explosion that just occurred.
        /// </summary>
        public struct ExplosionEvent
        {
            /// <summary>Center position of the explosion.</summary>
            public IntVector2 Position;
            /// <summary>Radius of the explosion in tiles.</summary>
            public int Range;
        }

        public BombSystem(World world, int mapWidth, int mapHeight, int scaledTileSize, Action<string>? logger = null)
        {
            _world = world;
            _mapWidth = mapWidth;
            _mapHeight = mapHeight;
            _scaledTileSize = scaledTileSize;
            _logger = logger;
        }

        /// <summary>
        /// Attempts to spawn a bomb at the target grid location for the specified player.
        /// Validates placement against capacity limits and existing bombs.
        /// </summary>
        public void TryPlaceBomb(IntVector2 targetGrid, PlayerComponent player)
        {
            int gridX = targetGrid.X;
            int gridY = targetGrid.Y;
            
            IntVector2 snapPos = new IntVector2(gridX * _scaledTileSize, gridY * _scaledTileSize);

            if (gridX < 0 || gridX >= _mapWidth || gridY < 0 || gridY >= _mapHeight) return;

             var bombs = _world.Bombs.GetAll();
            var bombEntities = _world.Bombs.GetEntities();
            var transformEntities = _world.Transforms.GetEntities();
            var allTransforms = _world.Transforms.GetAll();
            
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
            Entity bomb = _world.CreateEntity();
            _world.Bombs.Add(bomb, new BombComponent { Timer = 180, MaxTimer = 180, Range = player.BombRange, OwnerId = player.PlayerId });
            _world.Transforms.Add(bomb, new TransformComponent 
            { 
                Position = snapPos, 
                Size = new IntVector2(_scaledTileSize, _scaledTileSize) 
            });
            
            _logger?.Invoke($"[Bomb] P{player.PlayerId} at {gridX},{gridY}");
        }

        public List<ExplosionEvent> Update()
        {
            var bombList = _world.Bombs.GetAll();
            var bombEntities = _world.Bombs.GetEntities();
            var transformEntities = _world.Transforms.GetEntities();
            var allTransforms = _world.Transforms.GetAll();
            
            List<(Entity entity, BombComponent component, IntVector2 pos)> explosions = new();

            for (int i = 0; i < bombList.Count; i++)
            {
                var bomb = bombList[i];
                bomb.Timer--;
                _world.Bombs.Set(i, bomb);

                if (bomb.Timer <= 0)
                {
                   // Find Position
                   IntVector2 pos = IntVector2.Zero;
                   for(int t=0; t<transformEntities.Count; t++) {
                       if(transformEntities[t].Equals(bombEntities[i])) {
                           pos = allTransforms[t].Position;
                           break;
                       }
                   }
                   explosions.Add((bombEntities[i], bomb, pos));
                }
            }

            List<ExplosionEvent> events = new List<ExplosionEvent>();
            foreach (var explosion in explosions)
            {
                _world.Bombs.Remove(explosion.entity);
                _world.Transforms.Remove(explosion.entity);
                
                events.Add(new ExplosionEvent { Position = explosion.pos, Range = explosion.component.Range });
            }
            return events;
        }
    }
}
