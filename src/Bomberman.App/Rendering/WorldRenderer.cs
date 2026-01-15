using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Bomberman.Core;
using Bomberman.Core.ECS.Components;
using Bomberman.Core.Game;

namespace Bomberman.App.Rendering
{
    /// <summary>
    /// Responsible for rendering the game world entities.
    /// </summary>
    public class WorldRenderer
    {
        private readonly IRenderer _renderer;
        
        // Could be injected or passed, usually constant 32.
        private const int LocalSubpixelScale = 1000; 

        public WorldRenderer(IRenderer renderer)
        {
            _renderer = renderer;
        }

        /// <summary>
        /// Iterates through the game world entities (Tiles, Bombs, Powerups, Players)
        /// and renders them using the injected IRenderer.
        /// </summary>
        /// <param name="world">The ECS world containing entities and components.</param>
        public void DrawWorld(World world)
        {
            // Cache common components
            var transformEntities = world.Transforms.GetEntities();
            var transforms = world.Transforms.GetAll();

            // 1. Tiles
            var tiles = world.Tiles.GetAll();
            var tileEntities = world.Tiles.GetEntities();
            for(int i=0; i<tiles.Count; i++)
            {
                var trans = FindTransform(tileEntities[i], transformEntities, transforms);
                Vector2 pos = ToVec2(trans.Position);
                Vector2 size = ToVec2(trans.Size);

                // Base Floor
                DrawRect(pos + new Vector2(1,1), size - new Vector2(2,2), Color.Gray);

                if (tiles[i].Type == TileComponent.TileType.Solid) 
                    DrawRect(pos + new Vector2(1,1), size - new Vector2(2,2), Color.DarkGray);
                else if (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed) 
                    DrawRect(pos + new Vector2(1,1), size - new Vector2(2,2), Color.SaddleBrown);
            }

            // 2. Bombs
            var bombs = world.Bombs.GetAll();
            var bombEntities = world.Bombs.GetEntities();
            for(int i=0; i<bombs.Count; i++)
            {
                var trans = FindTransform(bombEntities[i], transformEntities, transforms);
                float pulse = (bombs[i].Timer % 20) / 20f;
                Color bColor = Color.Lerp(Color.Red, Color.DarkRed, pulse);
                Vector2 pos = ToVec2(trans.Position);
                Vector2 size = ToVec2(trans.Size);
                DrawRect(pos + new Vector2(4, 4), size - new Vector2(8,8), bColor);
            }

            // 3. Powerups
            var powerups = world.Powerups.GetAll();
            var powerupEntities = world.Powerups.GetEntities();
            for(int i=0; i<powerups.Count; i++)
            {
                var trans = FindTransform(powerupEntities[i], transformEntities, transforms);
                 Color pColor = Color.White;
                 if (powerups[i].Type == PowerupComponent.PowerupType.Range) pColor = Color.Yellow;
                 if (powerups[i].Type == PowerupComponent.PowerupType.Capacity) pColor = Color.Black;
                 DrawRect(ToVec2(trans.Position), ToVec2(trans.Size), pColor);
            }

            // 4. Explosions
            var expList = world.Explosions.GetAll();
            var expEntities = world.Explosions.GetEntities();
            for(int i=0; i<expList.Count; i++)
            {
                var trans = FindTransform(expEntities[i], transformEntities, transforms);
                DrawRect(ToVec2(trans.Position), ToVec2(trans.Size), Color.OrangeRed);
            }

            // 5. Players
            var players = world.Players.GetAll();
            var playerEntities = world.Players.GetEntities();
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Alive) continue; 
                var trans = FindTransform(playerEntities[i], transformEntities, transforms);
                Vector2 pos = ToVec2(trans.Position);
                Vector2 size = ToVec2(trans.Size);
                
                Color[] playerColors = new Color[] { Color.White, Color.Blue, Color.Red, Color.Green };
                DrawRect(pos, size, playerColors[i % playerColors.Length]);
                
                // Eyes
                Vector2 eyeOffset = new Vector2(4, 6);
                DrawRect(pos + eyeOffset, new Vector2(4, 6), Color.Black);
                DrawRect(pos + new Vector2(size.X - eyeOffset.X - 4, eyeOffset.Y), new Vector2(4, 6), Color.Black);
            }
        }

        private void DrawRect(Vector2 pos, Vector2 size, Color color)
        {
             _renderer.DrawTexture(new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y), color);
        }

        private Vector2 ToVec2(IntVector2 v)
        {
            // Use Simulation.SubpixelScale for conversion
            return new Vector2(v.X, v.Y) / (float)Simulation.SubpixelScale;
        }

        private TransformComponent FindTransform(Entity entity, List<Entity> transformEntities, List<TransformComponent> transforms)
        {
            // Linear search for performance simplicity in this scale
            for(int i=0; i<transformEntities.Count; i++)
            {
                if(transformEntities[i].Equals(entity))
                    return transforms[i];
            }
            return new TransformComponent(); // Empty/Zero
        }
    }
}
