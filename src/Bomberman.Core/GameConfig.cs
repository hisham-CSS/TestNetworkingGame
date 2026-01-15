namespace Bomberman.Core
{
    /// <summary>
    /// Centralized configuration for core game mechanics and map settings.
    /// </summary>
    public class GameConfig
    {
        // --- Map Settings ---
        
        /// <summary>Width of the map in tiles.</summary>
        public int MapWidth { get; set; } = 15;
        /// <summary>Height of the map in tiles.</summary>
        public int MapHeight { get; set; } = 13;
        /// <summary>Size of a single tile in pixels (e.g., 32x32).</summary>
        public int TileSize { get; set; } = 32;

        // --- Player Settings ---
        
        /// <summary>Player movement speed in pixels per second.</summary>
        public float PlayerSpeed { get; set; } = 120f;
        /// <summary>Number of lives a player starts with.</summary>
        public int InitialLives { get; set; } = 3;

        // --- Bomb Settings ---
        
        /// <summary>Time in seconds before a bomb explodes.</summary>
        public float BombFuseTime { get; set; } = 3.0f;
        /// <summary>Default explosion radius in tiles (1 = 3x3 cross).</summary>
        public int DefaultBombRange { get; set; } = 1;
        /// <summary>Default number of concurrent bombs a player can place.</summary>
        public int InitialBombCapacity { get; set; } = 1;
        
        // --- Simulation ---
        
        /// <summary>Fixed time step for physics updates (default 60Hz).</summary>
        public double FixedTimeStep { get; set; } = 1.0 / 60.0;
        
        public static GameConfig Default => new GameConfig();
    }
}
