using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Bomberman
{
    public class Game1 : Game
    {
        private Texture2D _pixelTexture;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private World _world;

        private bool _spacePressed = false;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window size to 416x416 (13x32 tiles)
            _graphics.PreferredBackBufferWidth = 416;
            _graphics.PreferredBackBufferHeight = 416;
        }

        protected override void Initialize()
        {
            _world = new World();
            // Create a player
            var player = _world.CreateEntity();
            _world.Players.Add(player, new PlayerComponent { PlayerId = 0, Alive = true });
            _world.Transforms.Add(player, new TransformComponent { GridX = 1, GridY = 1 });

            // Create tiles (13x13 grid)
            for (int y = 0; y < 13; y++)
            {
                for (int x = 0; x < 13; x++)
                {
                    var tile = _world.CreateEntity();
                    var tileType = DetermineTileType(x, y);
                    _world.Tiles.Add(tile, new TileComponent { Type = tileType, Destroyed = false });
                    _world.Transforms.Add(tile, new TransformComponent { GridX = x, GridY = y });
                }
            }

            base.Initialize();
        }

        private TileComponent.TileType DetermineTileType(int x, int y)
        {
            // Solid walls on edges and even grid positions
            if (x == 0 || x == 12 || y == 0 || y == 12)
                return TileComponent.TileType.Solid;
            
            if (x % 2 == 0 && y % 2 == 0)
                return TileComponent.TileType.Solid;
            
            // Keep spawn corners clear
            if ((x == 1 && y == 1) || (x == 1 && y == 11) || (x == 11 && y == 1) || (x == 11 && y == 11))
                return TileComponent.TileType.Empty;
            
            // 70% destructible
            return new Random().NextDouble() < 0.7 ? TileComponent.TileType.Destructible : TileComponent.TileType.Empty;
        }
  
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Create a 1x1 white pixel texture for drawing rectangles
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
             var keyboardState = Keyboard.GetState();
    
            // Get player
            var players = _world.Players.GetAll();
            if (players.Count > 0)
            {
                var playerTransforms = _world.Transforms.GetAll();
                var transform = playerTransforms[0];
                
                // Handle movement with collision
                int newX = transform.GridX;
                int newY = transform.GridY;
                
                if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))
                    if (IsWalkable(transform.GridX, transform.GridY - 1))
                        newY--;
                
                if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))
                    if (IsWalkable(transform.GridX, transform.GridY + 1))
                        newY++;
                
                if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))
                    if (IsWalkable(transform.GridX - 1, transform.GridY))
                        newX--;
                
                if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right))
                    if (IsWalkable(transform.GridX + 1, transform.GridY))
                        newX++;
                
                transform.GridX = newX;
                transform.GridY = newY;
                playerTransforms[0] = transform;
                
                // Handle bomb placement (grid-snapped)
                if (keyboardState.IsKeyDown(Keys.Space) && !_spacePressed)
                {
                    _spacePressed = true;
                    
                    // Check if bomb already exists at this location
                    bool bombExists = false;
                    var bombTransforms = _world.Transforms.GetAll();
                    var bombs = _world.Bombs.GetAll();
                    
                    for (int i = 0; i < bombs.Count; i++)
                    {
                        if (bombTransforms[i].GridX == transform.GridX && bombTransforms[i].GridY == transform.GridY)
                        {
                            bombExists = true;
                            break;
                        }
                    }
                    
                    if (!bombExists)
                    {
                        var bomb = _world.CreateEntity();
                        _world.Bombs.Add(bomb, new BombComponent { Timer = 180, MaxTimer = 180 });
                        _world.Transforms.Add(bomb, transform);
                    }
                }
                else if (!keyboardState.IsKeyDown(Keys.Space))
                {
                    _spacePressed = false;
                }
            }
            
            // Update bomb timers
            var bombList = _world.Bombs.GetAll();
            var bombTransformList = _world.Transforms.GetAll();
            
            for (int i = 0; i < bombList.Count; i++)
            {
                var bomb = bombList[i];
                bomb.Timer--;
                bombList[i] = bomb;
                
                if (bomb.Timer <= 0)
                {
                    // Create explosion at bomb location
                    var explosion = _world.CreateEntity();
                    _world.Explosions.Add(explosion, new ExplosionComponent { Timer = 30, MaxTimer = 30 });
                    _world.Transforms.Add(explosion, bombTransformList[i]);
                    
                    // Remove bomb (by swapping with last)
                    if (i < bombList.Count - 1)
                    {
                        bombList[i] = bombList[bombList.Count - 1];
                        bombTransformList[i] = bombTransformList[bombTransformList.Count - 1];
                    }
                    bombList.RemoveAt(bombList.Count - 1);
                    bombTransformList.RemoveAt(bombTransformList.Count - 1);
                }
            }
            
            // Update explosion timers
            var explosionList = _world.Explosions.GetAll();
            var explosionTransformList = _world.Transforms.GetAll();
            
            for (int i = 0; i < explosionList.Count; i++)
            {
                var explosion = explosionList[i];
                explosion.Timer--;
                explosionList[i] = explosion;
                
                if (explosion.Timer <= 0)
                {
                    // Remove explosion
                    if (i < explosionList.Count - 1)
                    {
                        explosionList[i] = explosionList[explosionList.Count - 1];
                        explosionTransformList[i] = explosionTransformList[explosionTransformList.Count - 1];
                    }
                    explosionList.RemoveAt(explosionList.Count - 1);
                    explosionTransformList.RemoveAt(explosionTransformList.Count - 1);
                }
            }
    
            base.Update(gameTime);
        }
                    

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    
            const int tileSize = 32;

            // Draw tiles (iterate sequentially through contiguous array)
            var tiles = _world.Tiles.GetAll();
            var tileTransforms = _world.Transforms.GetAll();
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var transform = tileTransforms[i];
        
                Color color = tile.Type switch
                {
                    TileComponent.TileType.Solid => Color.DarkGray,
                    TileComponent.TileType.Destructible => Color.Brown,
                    _ => Color.Black
                };
                DrawRectangle(transform.GridX * tileSize, transform.GridY * tileSize, tileSize, tileSize, color);
            }

            // Draw bombs
            var bombs = _world.Bombs.GetAll();
            var bombTransforms = _world.Transforms.GetAll();
                
            for (int i = 0; i < bombs.Count; i++)
            {
                var transform = bombTransforms[i];
                DrawRectangle(transform.GridX * tileSize, transform.GridY * tileSize, tileSize, tileSize, Color.Black);
                DrawRectangle(transform.GridX * tileSize + 8, transform.GridY * tileSize + 8, 16, 16, Color.Yellow);
            }
                
            // Draw explosions
            var explosions = _world.Explosions.GetAll();
            var explosionTransforms = _world.Transforms.GetAll();
                
            for (int i = 0; i < explosions.Count; i++)
            {
                var transform = explosionTransforms[i];
                DrawRectangle(transform.GridX * tileSize, transform.GridY * tileSize, tileSize, tileSize, Color.OrangeRed);
            }
                
            // Draw player
            var players = _world.Players.GetAll();
            var playerTransforms = _world.Transforms.GetAll();
                
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Alive)
                    continue;
                    
                var transform = playerTransforms[i];
                DrawRectangle(transform.GridX * tileSize, transform.GridY * tileSize, tileSize, tileSize, Color.Red);
            }
                
            _spriteBatch.End();
            base.Draw(gameTime);
        }


        private void DrawRectangle(int x, int y, int width, int height, Color color)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
        }

        private bool IsWalkable(int gridX, int gridY)
        {
            if (gridX < 0 || gridX >= 13 || gridY < 0 || gridY >= 13)
                return false;
            
            // Check if there's a solid or destructible wall
            var tiles = _world.Tiles.GetAll();
            var tileTransforms = _world.Transforms.GetAll();
            
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tileTransforms[i].GridX == gridX && tileTransforms[i].GridY == gridY)
                {
                    if (tiles[i].Type == TileComponent.TileType.Solid || tiles[i].Type == TileComponent.TileType.Destructible)
                        return false;
                }
            }
            
            // Check if there's a bomb
            var bombs = _world.Bombs.GetAll();
            var bombTransforms = _world.Transforms.GetAll();
            
            for (int i = 0; i < bombs.Count; i++)
            {
                if (bombTransforms[i].GridX == gridX && bombTransforms[i].GridY == gridY)
                    return false;
            }
            
            return true;
        }
    }

    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new Game1())
                game.Run();
        }
    }
}
