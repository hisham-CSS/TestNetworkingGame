using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Bomberman.Core
{
    public struct RenderItem
    {
        public Vector2 Position;
        public Vector2 Size;
        public int Variant;   // tile type / powerup type
        public bool Flag;     // destroyed (tiles) / alive (players)
    }

    /// <summary>
    /// An immutable, render-ready capture of the world for one frame. The simulation thread builds
    /// it; the render thread reads it. Decoupling rendering from the live World is what lets the two
    /// run on different threads safely (the View never touches mutable simulation state).
    /// </summary>
    public sealed class RenderSnapshot
    {
        public int Frame { get; }
        public List<RenderItem> Tiles { get; } = new();
        public List<RenderItem> Bombs { get; } = new();
        public List<RenderItem> Powerups { get; } = new();
        public List<RenderItem> Explosions { get; } = new();
        public List<RenderItem> Players { get; } = new();

        private RenderSnapshot(int frame) { Frame = frame; }

        public static RenderSnapshot From(World world, int frame)
        {
            var snap = new RenderSnapshot(frame);
            var tE = world.Transforms.GetEntities();
            var tC = world.Transforms.GetAll();

            TransformComponent Find(Entity e)
            {
                for (int i = 0; i < tE.Count; i++) if (tE[i].Equals(e)) return tC[i];
                return new TransformComponent();
            }

            var tiles = world.Tiles.GetAll(); var tilesE = world.Tiles.GetEntities();
            for (int i = 0; i < tiles.Count; i++) { var tr = Find(tilesE[i]); snap.Tiles.Add(new RenderItem { Position = tr.Position, Size = tr.Size, Variant = (int)tiles[i].Type, Flag = tiles[i].Destroyed }); }

            var bombs = world.Bombs.GetAll(); var bombsE = world.Bombs.GetEntities();
            for (int i = 0; i < bombs.Count; i++) { var tr = Find(bombsE[i]); snap.Bombs.Add(new RenderItem { Position = tr.Position, Size = tr.Size }); }

            var pus = world.Powerups.GetAll(); var pusE = world.Powerups.GetEntities();
            for (int i = 0; i < pus.Count; i++) { var tr = Find(pusE[i]); snap.Powerups.Add(new RenderItem { Position = tr.Position, Size = tr.Size, Variant = (int)pus[i].Type }); }

            var exps = world.Explosions.GetAll(); var expsE = world.Explosions.GetEntities();
            for (int i = 0; i < exps.Count; i++) { var tr = Find(expsE[i]); snap.Explosions.Add(new RenderItem { Position = tr.Position, Size = tr.Size }); }

            var pls = world.Players.GetAll(); var plsE = world.Players.GetEntities();
            for (int i = 0; i < pls.Count; i++) { var tr = Find(plsE[i]); snap.Players.Add(new RenderItem { Position = tr.Position, Size = tr.Size, Variant = (int)pls[i].PlayerId, Flag = pls[i].Alive }); }

            return snap;
        }
    }
}
