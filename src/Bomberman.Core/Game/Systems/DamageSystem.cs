using System;
using System.Collections.Generic;
using Bomberman.Core.ECS.Components;
using Bomberman.Core;

namespace Bomberman.Core.Game.Systems
{
    public class DamageSystem
    {
         private World _world;
         private int _subpixelScale;
         
         public DamageSystem(World world, int subpixelScale)
         {
             _world = world;
             _subpixelScale = subpixelScale;
         }

         public void Update()
         {
            var players = _world.Players.GetAll();
            var playerTransforms = _world.Transforms.GetAll(); 
            var playerEntities = _world.Players.GetEntities();
            var playerTransformEntities = _world.Transforms.GetEntities();
            
            var explosions = _world.Explosions.GetAll();
            if (explosions.Count == 0) return;
            var explosionTransforms = _world.Transforms.GetAll();
            var explosionEntities = _world.Transforms.GetEntities();
            var explosionCompEntities = _world.Explosions.GetEntities(); 

            // Optimization: Cache explosion rects (in scaled units)
            List<IntRect> expRects = new List<IntRect>();
            for(int i=0; i<explosions.Count; i++) 
            {
                 var entity = explosionCompEntities[i];
                 for(int t=0; t<explosionEntities.Count; t++) {
                     if (explosionEntities[t].Equals(entity)) {
                         var trans = explosionTransforms[t];
                         // Shrink 4 pixels = 400 units
                         int shrink = 4 * _subpixelScale;
                         expRects.Add(new IntRect(trans.Position.X + shrink, trans.Position.Y + shrink, trans.Size.X - shrink*2, trans.Size.Y - shrink*2));
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

                IntRect pRect = new IntRect(pTrans.Position.X, pTrans.Position.Y, pTrans.Size.X, pTrans.Size.Y);

                foreach(var eRect in expRects)
                {
                    if (pRect.Intersects(eRect))
                    {
                        var p = players[i];
                        p.Alive = false;
                        _world.Players.Set(i, p);
                        // Log?
                        break;
                    }
                }
            }
         }
    }
}
