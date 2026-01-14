namespace Bomberman.Core
{
    /// <summary>
    /// Centralized configuration for core game mechanics and map settings.
    /// </summary>
    public class GameConfig
    {
        // Map Settings
        public int MapWidth { get; set; } = 15;
        public int MapHeight { get; set; } = 13;
        public int TileSize { get; set; } = 32;

        // Player Settings
        public float PlayerSpeed { get; set; } = 120f;
        public int InitialLives { get; set; } = 3;

        // Bomb Settings
        public float BombFuseTime { get; set; } = 3.0f;
        public int DefaultBombRange { get; set; } = 1;
        public int InitialBombCapacity { get; set; } = 1;
        
        // Simulation
        public double FixedTimeStep { get; set; } = 1.0 / 60.0;
        
        public static GameConfig Default => new GameConfig();
    }
}
