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
            _world.AddTransform(player, new TransformComponent { X = 32, Y = 32 });
            _world.AddPlayer(player, new PlayerComponent { PlayerId = 0, Alive = true });
            base.Initialize();
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
    
            // Get the player
            var entities = _world.GetEntities();
            if (entities.Count > 0)
            {
                var player = entities[0];
                var transform = _world.GetTransform(player);
                var playerComp = _world.GetPlayer(player);
                
                if (transform.HasValue)
                {
                    var newTransform = transform.Value;
                    
                    // Handle input
                    if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))
                        newTransform.Y -= 2;
                    if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))
                        newTransform.Y += 2;
                    if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))
                        newTransform.X -= 2;
                    if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right))
                        newTransform.X += 2;
                    
                    // Clamp to grid
                    newTransform.X = Math.Max(0, Math.Min(newTransform.X, 416 - 32));
                    newTransform.Y = Math.Max(0, Math.Min(newTransform.Y, 416 - 32));
                    
                    _world.AddTransform(player, newTransform);

                    // Handle bomb placement
                    if (keyboardState.IsKeyDown(Keys.Space))
                    {
                        // Check if there's already a bomb at this location
                        bool bombExists = false;
                        foreach (var entity in entities)
                        {
                            var bombTransform = _world.GetTransform(entity);
                            var bomb = _world.GetBomb(entity);
                            if (bomb.HasValue && bombTransform.HasValue)
                            {
                                if (bombTransform.Value.X == newTransform.X && bombTransform.Value.Y == newTransform.Y)
                                {
                                    bombExists = true;
                                    break;
                                }
                            }
                        }
                        
                        if (!bombExists)
                        {
                            var bomb = _world.CreateEntity();
                            _world.AddTransform(bomb, new TransformComponent { X = newTransform.X, Y = newTransform.Y });
                            _world.AddBomb(bomb, new BombComponent { OwnerId = 0, Timer = 180, MaxTimer = 180 });
                        }
                    }
                }
            }

            // Update bombs
            var bombsToExplode = new List<Entity>();
            foreach (var entity in entities)
            {
                var bomb = _world.GetBomb(entity);
                if (bomb.HasValue)
                {
                    var updatedBomb = bomb.Value;
                    updatedBomb.Timer--;
                    
                    if (updatedBomb.Timer <= 0)
                    {
                        bombsToExplode.Add(entity);
                    }
                    else
                    {
                        _world.AddBomb(entity, updatedBomb);
                    }
                }
            }

            // Explode bombs
            foreach (var bombEntity in bombsToExplode)
            {
                var bombTransform = _world.GetTransform(bombEntity);
                if (bombTransform.HasValue)
                {
                    // Create explosion at bomb location
                    var explosion = _world.CreateEntity();
                    _world.AddTransform(explosion, bombTransform.Value);
                    _world.AddExplosion(explosion, new ExplosionComponent { Timer = 30, MaxTimer = 30 });
                }
                
                _world.RemoveBomb(bombEntity);
            }

            // Update explosions
            var explosionsToRemove = new List<Entity>();
            foreach (var entity in entities)
            {
                var explosion = _world.GetExplosion(entity);
                if (explosion.HasValue)
                {
                    var updatedExplosion = explosion.Value;
                    updatedExplosion.Timer--;
                    
                    if (updatedExplosion.Timer <= 0)
                    {
                        explosionsToRemove.Add(entity);
                    }
                    else
                    {
                        _world.AddExplosion(entity, updatedExplosion);
                    }
                }
            }

            // Remove expired explosions
            foreach (var entity in explosionsToRemove)
            {
                _world.RemoveExplosion(entity);
            }
    
            base.Update(gameTime);
        }
                    

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    
            const int tileSize = 32;
            const int gridSize = 13;
    
            // Draw grid
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    Color color = ((x + y) % 2 == 0) ? Color.DarkGray : Color.Black;
                    DrawRectangle(x * tileSize, y * tileSize, tileSize, tileSize, color);
                }
            }

            // Draw bombs
            foreach (var entity in _world.GetEntities())
            {
                var bomb = _world.GetBomb(entity);
                if (bomb.HasValue)
                {
                    var transform = _world.GetTransform(entity);
                    if (transform.HasValue)
                    {
                        DrawRectangle(transform.Value.X, transform.Value.Y, tileSize, tileSize, Color.Black);
                        DrawRectangle(transform.Value.X + 8, transform.Value.Y + 8, 16, 16, Color.Yellow);
                    }
                }
            }

            // Draw explosions
            foreach (var entity in _world.GetEntities())
            {
                var explosion = _world.GetExplosion(entity);
                if (explosion.HasValue)
                {
                    var transform = _world.GetTransform(entity);
                    if (transform.HasValue)
                    {
                        DrawRectangle(transform.Value.X, transform.Value.Y, tileSize, tileSize, Color.OrangeRed);
                    }
                }
            }
                
            // Drawdon player
            foreach (var entity in _world.GetEntities())
            {
                var transform = _world.GetTransform(entity);
                var player = _world.GetPlayer(entity);
                
                if (transform.HasValue && player.HasValue && player.Value.Alive)
                {
                    DrawRectangle(transform.Value.X, transform.Value.Y, tileSize, tileSize, Color.Red);
                }
            }
            
            _spriteBatch.End();
            base.Draw(gameTime);
        }


        private void DrawRectangle(int x, int y, int width, int height, Color color)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
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
