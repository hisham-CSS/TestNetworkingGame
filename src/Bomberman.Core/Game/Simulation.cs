using System;
using Bomberman.Core;
using Bomberman.Core.Input;
using Bomberman.Core.ECS.Components;
using System.Collections.Generic;
using Bomberman.Core.Game.Systems;

namespace Bomberman.Core.Game
{
    /// <summary>
    /// The deterministic core game loop.
    /// Manages the ECS World, Systems, and Map generation.
    /// </summary>
    public class Simulation
    {
        /// <summary>The ECS World containing all game entities.</summary>
        public World World { get; private set; }
        
        /// <summary>Optional debug logger hook.</summary>
        public Action<string>? Log; 
        
        /// <summary>Deterministic Random Number Generator.</summary>
        public DeterministicRandom Rng { get; private set; }

        public const int SubpixelScale = 100; // 1 unit = 0.01 pixel
        private const int MapWidth = 15;
        private const int MapHeight = 13;
        private const int TileSize = 32;
        private const int ScaledTileSize = TileSize * SubpixelScale;
        private const int PlayerSpeedPerFrame = 250; 

        // Systems
        private MovementSystem _movementSystem;
        private BombSystem _bombSystem;
        private ExplosionSystem _explosionSystem;
        private DamageSystem _damageSystem;

        /// <summary>
        /// Initializes a new simulation.
        /// </summary>
        /// <param name="seed">Seed for Map Generation and RNG.</param>
        /// <param name="playerCount">Number of players to spawn.</param>
        public Simulation(int seed, int playerCount)
        {
            World = new World();
            Rng = new DeterministicRandom(seed);
            
            // Initialize Systems
            _movementSystem = new MovementSystem(World, PlayerSpeedPerFrame);
            // Log hookup handled via property injection ideally, or pass lambda
            _bombSystem = new BombSystem(World, MapWidth, MapHeight, ScaledTileSize, (msg) => Log?.Invoke(msg));
            _explosionSystem = new ExplosionSystem(World, ScaledTileSize, SubpixelScale);
            _damageSystem = new DamageSystem(World, SubpixelScale);

            GenerateMap();
            SpawnPlayers(playerCount);
        }

        private void GenerateMap()
        {
            var random = Rng;

            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    Entity tile = World.CreateEntity();
                    var transform = new TransformComponent
                    {
                        Position = new IntVector2(x * ScaledTileSize, y * ScaledTileSize),
                        Size = new IntVector2(ScaledTileSize, ScaledTileSize)
                    };
                    
                    TileComponent.TileType type = TileComponent.TileType.Empty;
                    PowerupComponent.PowerupType hiddenPowerup = PowerupComponent.PowerupType.None;

                    // Borders
                    if (y == 0 || y == MapHeight - 1 || x == 0 || x == MapWidth - 1)
                        type = TileComponent.TileType.Solid;
                    else if (x % 2 == 0 && y % 2 == 0)
                        type = TileComponent.TileType.Solid;
                    else if (!IsSpawnZone(x, y) && random.NextDouble() < 0.6)
                    {
                        type = TileComponent.TileType.Destructible;
                        if (random.NextDouble() < 0.3)
                        {
                            hiddenPowerup = random.NextDouble() < 0.5 ? PowerupComponent.PowerupType.Range : PowerupComponent.PowerupType.Capacity;
                        }
                    }

                    World.Tiles.Add(tile, new TileComponent { Type = type, HiddenPowerup = hiddenPowerup });
                    World.Transforms.Add(tile, transform);
                }
            }
        }

        private bool IsSpawnZone(int x, int y)
        {
            if ((x <= 2 && y <= 2) || (x >= MapWidth - 3 && y <= 2) ||
                (x <= 2 && y >= MapHeight - 3) || (x >= MapWidth - 3 && y >= MapHeight - 3))
                return true;
            return false;
        }

        private void SpawnPlayers(int count)
        {
            var spawnPoints = new[]
            {
                new IntVector2(1, 1),
                new IntVector2(MapWidth - 2, 1),
                new IntVector2(1, MapHeight - 2),
                new IntVector2(MapWidth - 2, MapHeight - 2)
            };

            for (int i = 0; i < count; i++) 
            {
                var player = World.CreateEntity();
                World.Players.Add(player, new PlayerComponent { PlayerId = (uint)i, Alive = true, BombRange = 1, BombCapacity = 1 });
                
                int playerSize = 24 * SubpixelScale;
                int startX = spawnPoints[i].X * ScaledTileSize + (ScaledTileSize - playerSize) / 2;
                int startY = spawnPoints[i].Y * ScaledTileSize + (ScaledTileSize - playerSize) / 2;

                World.Transforms.Add(player, new TransformComponent 
                { 
                    Position = new IntVector2(startX, startY), 
                    Size = new IntVector2(playerSize, playerSize)
                });
            }
        }

        public bool IsGameOver { get; private set; }
        public int WinnerId { get; private set; } = -1; 

        public void Update(InputState[] inputs, float dt)
        {
            if (IsGameOver) return; 

            // 1. Movement & Collisions
            _movementSystem.Update(inputs);

            // 2. Input Actions (Bomb Placement)
            var players = World.Players.GetAll();
            // We need to map Input Index -> Player Component
            // Assumption: inputs[i] is for PlayerId == i
            // We iterate inputs to find corresponding player
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i].PlaceBomb)
                {
                    // Find player
                    for(int p=0; p<players.Count; p++)
                    {
                        if (players[p].PlayerId == i && players[p].Alive)
                        {
                            _bombSystem.TryPlaceBomb(inputs[i].BombTarget, players[p]);
                            break;
                        }
                    }
                }
            }

            // 3. Game Logic
            var bombEvents = _bombSystem.Update();
            foreach(var evt in bombEvents)
            {
                _explosionSystem.TriggerExplosion(evt.Position, evt.Range);
            }

            _explosionSystem.Update();
            _damageSystem.Update();
            
            CheckWinCondition();
        }

        private void CheckWinCondition()
        {
            var players = World.Players.GetAll();
            int aliveCount = 0;
            int lastAliveId = -1;

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Alive)
                {
                    aliveCount++;
                    lastAliveId = (int)players[i].PlayerId;
                }
            }

            if (aliveCount <= 1 && players.Count > 1)
            {
                IsGameOver = true;
                WinnerId = (aliveCount == 1) ? lastAliveId : -1;
                Log?.Invoke($"[Simulation] Game Over. Winner: {WinnerId}");
            }
        }
    }
}
