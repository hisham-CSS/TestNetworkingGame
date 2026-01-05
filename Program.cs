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

        // Define spawn points for up to 4 players
        private static readonly (int x, int y)[] SpawnPoints = new[]
        {
            (1, 1),      // Top-left
            (11, 1),     // Top-right
            (1, 11),     // Bottom-left
            (11, 11)     // Bottom-right
        };

        // Input smoothing
        private int _inputX = 0;  // -1, 0, or 1
        private int _inputY = 0;  // -1, 0, or 1
        private double _moveTimer = 0;
        private const double MoveDelay = 0.15;  // 150ms between moves

        // Random for map generation (fixed seed for consistent maps)
        private System.Random _mapRandom = new System.Random(42);

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
            // Create a player at spawn point 0
            var player = _world.CreateEntity();
            _world.Players.Add(player, new PlayerComponent { PlayerId = 0, Alive = true, InputX = _inputX, InputY = _inputY });
            _world.Transforms.Add(player, new TransformComponent { GridX = SpawnPoints[0].x, GridY = SpawnPoints[0].y });

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
            // Solid walls only on edges
            if (x == 0 || x == 12 || y == 0 || y == 12)
                return TileComponent.TileType.Solid;
            
            // Keep spawn corners and surrounding areas clear (2x2 around each corner)
            if ((x <= 2 && y <= 2) || (x >= 10 && y <= 2) || (x <= 2 && y >= 10) || (x >= 10 && y >= 10))
                return TileComponent.TileType.Empty;
            
            // 40% destructible in other areas (leaving 60% empty for movement)
            return _mapRandom.NextDouble() < 0.4 ? TileComponent.TileType.Destructible : TileComponent.TileType.Empty;
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

            // Update input buffer (what direction is the player trying to go?)
            _inputX = 0;
            _inputY = 0;
            
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))
                _inputY = -1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))
                _inputY = 1;
            
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))
                _inputX = -1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right))
                _inputX = 1;

            // Update movement timer
            _moveTimer += gameTime.ElapsedGameTime.TotalSeconds;
            
            if (_moveTimer >= MoveDelay)
            {
                _moveTimer = 0;
                
                var players = _world.Players.GetAll();
                if (players.Count > 0)
                {
                    var playerTransforms = _world.Transforms.GetAll();
                    var transform = playerTransforms[0];
                    
                    // Try to move in the input direction
                    if (_inputX != 0 && IsWalkable(transform.GridX + _inputX, transform.GridY))
                        transform.GridX += _inputX;
                    else if (_inputY != 0 && IsWalkable(transform.GridX, transform.GridY + _inputY))
                        transform.GridY += _inputY;
                    
                    playerTransforms[0] = transform;

                    // Handle bomb placement (grid-snapped)
                    if (keyboardState.IsKeyDown(Keys.Space) && !_spacePressed)
                    {
                        _spacePressed = true;
                        
                        // Check if bomb already exists at this location
                        bool bombExists = false;
                        var bombs = _world.Bombs.GetAll();
                        var bombTransforms = _world.Bombs.GetAll(); // This is wrong - we need bomb transforms!
                        
                        // FIX: We need to track which transforms belong to bombs
                        // For now, let's just place the bomb
                        var bomb = _world.CreateEntity();
                        _world.Bombs.Add(bomb, new BombComponent { Timer = 180, MaxTimer = 180 });
                        _world.Transforms.Add(bomb, transform);
                    }
                    else if (!keyboardState.IsKeyDown(Keys.Space))
                    {
                        _spacePressed = false;
                    }
                }
            }

            // Update bomb timers and create explosions
            var bombList = _world.Bombs.GetAll();
            var bombTransformList = _world.Transforms.GetAll();
            
            // We need to track bomb indices to get their transforms
            // This is a limitation of the current architecture
            // For now, we'll iterate through bombs and assume they're in order
            var bombEntities = _world.Bombs.GetEntities();
            
            for (int i = 0; i < bombList.Count; i++)
            {
                var bomb = bombList[i];
                bomb.Timer--;
                bombList[i] = bomb;
                
                if (bomb.Timer <= 0)
                {
                    // Find the transform for this bomb
                    var bombEntity = bombEntities[i];
                    var transformEntities = _world.Transforms.GetEntities();
                    var transforms = _world.Transforms.GetAll();
                    
                    TransformComponent bombTransform = new TransformComponent { GridX = 0, GridY = 0 };
                    for (int j = 0; j < transformEntities.Count; j++)
                    {
                        if (transformEntities[j].Equals(bombEntity))
                        {
                            bombTransform = transforms[j];
                            break;
                        }
                    }
                    
                    // Create explosion at bomb location
                    var explosion = _world.CreateEntity();
                    _world.Explosions.Add(explosion, new ExplosionComponent { Timer = 30, MaxTimer = 30 });
                    _world.Transforms.Add(explosion, bombTransform);
                    
                    // Remove bomb (by swapping with last)
                    if (i < bombList.Count - 1)
                    {
                        bombList[i] = bombList[bombList.Count - 1];
                        bombEntities[i] = bombEntities[bombEntities.Count - 1];
                    }
                    bombList.RemoveAt(bombList.Count - 1);
                    bombEntities.RemoveAt(bombEntities.Count - 1);
                }
            }

            // Update explosion timers
            var explosionList = _world.Explosions.GetAll();
            var explosionEntities = _world.Explosions.GetEntities();
            var transformEntities2 = _world.Transforms.GetEntities();
            var transforms2 = _world.Transforms.GetAll();
        
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
                        explosionEntities[i] = explosionEntities[explosionEntities.Count - 1];
                    }
                    explosionList.RemoveAt(explosionList.Count - 1);
                    explosionEntities.RemoveAt(explosionEntities.Count - 1);
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
            var bombEntities = _world.Bombs.GetEntities();
            var transformEntities = _world.Transforms.GetEntities();
            var transforms = _world.Transforms.GetAll();
                
            for (int i = 0; i < bombs.Count; i++)
            {
                var bombEntity = bombEntities[i];
                // Find the transform for this bomb
                for (int j = 0; j < transformEntities.Count; j++)
                {
                    if (transformEntities[j].Equals(bombEntity))
                    {
                        var transform = transforms[j];
                        DrawRectangle(transform.GridX * tileSize, transform.GridY * tileSize, tileSize, tileSize, Color.Black);
                        DrawRectangle(transform.GridX * tileSize + 8, transform.GridY * tileSize + 8, 16, 16, Color.Yellow);
                        break;
                    }
                }
            }
                
            // Draw explosions
            var explosions = _world.Explosions.GetAll();
            var explosionEntities = _world.Explosions.GetEntities();
                
            for (int i = 0; i < explosions.Count; i++)
            {
                var explosionEntity = explosionEntities[i];
                // Find the transform for this explosion
                for (int j = 0; j < transformEntities.Count; j++)
                {
                    if (transformEntities[j].Equals(explosionEntity))
                    {
                        var transform = transforms[j];
                        DrawRectangle(transform.GridX * tileSize, transform.GridY * tileSize, tileSize, tileSize, Color.OrangeRed);
                        break;
                    }
                }
            }
                
            // Draw player
            var players = _world.Players.GetAll();
            var playerEntities = _world.Players.GetEntities();
                
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Alive)
                    continue;
                    
                var playerEntity = playerEntities[i];
                // Find the transform for this player
                for (int j = 0; j < transformEntities.Count; j++)
                {
                    if (transformEntities[j].Equals(playerEntity))
                    {
                        var transform = transforms[j];
                        DrawRectangle(transform.GridX * tileSize, transform.GridY * tileSize, tileSize, tileSize, Color.Red);
                        break;
                    }
                }
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
            var bombEntities = _world.Bombs.GetEntities();
            var transformEntities = _world.Transforms.GetEntities();
            var transforms = _world.Transforms.GetAll();
            
            for (int i = 0; i < bombs.Count; i++)
            {
                var bombEntity = bombEntities[i];
                for (int j = 0; j < transformEntities.Count; j++)
                {
                    if (transformEntities[j].Equals(bombEntity))
                    {
                        if (transforms[j].GridX == gridX && transforms[j].GridY == gridY)
                            return false;
                        break;
                    }
                }
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
