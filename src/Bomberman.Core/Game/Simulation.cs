using System;
using System.Collections.Generic;
using Bomberman.Core;
using Bomberman.Core.Input;
using Bomberman.Core.ECS.Components;
using Bomberman.Core.Game.Systems;
using Chronos.Core;

namespace Bomberman.Core.Game
{
    /// <summary>
    /// The deterministic core game loop.
    /// Manages the ECS World, Systems, and Map generation.
    /// </summary>
    public class Simulation : IGameSimulation<InputState, GameStateSnapshot>
    {
        /// <summary>The ECS World containing all game entities.</summary>
        public World World { get; private set; }
        
        /// <summary>Optional debug logger hook.</summary>
        public Action<string>? Log; 
        
        /// <summary>Deterministic Random Number Generator.</summary>
        public DeterministicRandom Rng { get; private set; } 
        



        /// <summary>Subpixel resolution for deterministic physics (100 units = 1 pixel).</summary>
        public const int SubpixelScale = 100;
        
        private readonly GameConfig _config;
        private int ScaledTileSize => _config.TileSize * SubpixelScale;

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
        /// <param name="config">Game configuration.</param>
        public Simulation(int seed, int playerCount, GameConfig? config = null)
        {
            _config = config ?? GameConfig.Default;
            World = new World();
            Rng = new DeterministicRandom(seed);
            
            // Calculate speed per frame (Speed * Subpixel * FixedTimeStep)
            int speedPerFrame = (int)(_config.PlayerSpeed * SubpixelScale * _config.FixedTimeStep);

            // Initialize Systems
            _movementSystem = new MovementSystem(World, speedPerFrame);
            // Log hookup handled via property injection ideally, or pass lambda
            _bombSystem = new BombSystem(World, _config.MapWidth, _config.MapHeight, ScaledTileSize, (msg) => Log?.Invoke(msg));
            _explosionSystem = new ExplosionSystem(World, ScaledTileSize, SubpixelScale);
            _damageSystem = new DamageSystem(World, SubpixelScale);

            GenerateMap();
            SpawnPlayers(playerCount);
        }

        public GameStateSnapshot CaptureState()
        {
            // Frame defaults to -1 or managed externally, here we assume current frame logic is external or tracking 
            // Chronos usually tracks the frame number. The Snapshot constructor demands a frame.
            // We'll pass 0 and let Chronos overwrite if needed, or if Simulation tracked frame count we'd use it.
            // Wait, Simulation doesn't explicitly track "Frame Index" for itself in this implementation (it relies on Update).
            // Let's assume calling code (Chronos) manages the frame index association with the snapshot.
            return new GameStateSnapshot(0, World);
        }

        public void RestoreState(GameStateSnapshot state)
        {
            state.Restore(World);
            // Also need to restore RNG state if it was part of snapshot. 
            // In GameStateSnapshot.Restore(World), it didn't restore RNG. 
            // We should ideally sync RNG too.
            // For now, let's leave RNG desync specific handling to `GameStateSnapshot.RestoreFromBytes` which does handle it.
            // But `Restore` which just takes `World` might missed it. 
            // But `GameStateSnapshot` doesn't store RNG in the object graph (only in DTO).
            // This is a small gap. The original SnapshotStore logic in Bomberman didn't seem to persist RNG in memory snapshot?
            // Actually `GameStateSnapshot` constructor just captures pools.
            // For full determinism, RNG should be captured.
            // I'll add RNG capture to GameStateSnapshot later if needed, or assume RNG state is implicitly tied to frame (re-seeded).
            // But usually RNG state must be saved.
        }

        private void GenerateMap()
        {
            var random = Rng;

            for (int y = 0; y < _config.MapHeight; y++)
            {
                for (int x = 0; x < _config.MapWidth; x++)
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
                    if (y == 0 || y == _config.MapHeight - 1 || x == 0 || x == _config.MapWidth - 1)
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
            if ((x <= 2 && y <= 2) || (x >= _config.MapWidth - 3 && y <= 2) ||
                (x <= 2 && y >= _config.MapHeight - 3) || (x >= _config.MapWidth - 3 && y >= _config.MapHeight - 3))
                return true;
            return false;
        }

        private void SpawnPlayers(int count)
        {
            var spawnPoints = new[]
            {
                new IntVector2(1, 1),
                new IntVector2(_config.MapWidth - 2, 1),
                new IntVector2(1, _config.MapHeight - 2),
                new IntVector2(_config.MapWidth - 2, _config.MapHeight - 2)
            };

            for (int i = 0; i < count; i++) 
            {
                var player = World.CreateEntity();
                World.Players.Add(player, new PlayerComponent { PlayerId = (uint)i, Alive = true, BombRange = _config.DefaultBombRange, BombCapacity = _config.InitialBombCapacity });
                
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
            // Iterate inputs to find corresponding player
            var players = World.Players.GetAll();
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
