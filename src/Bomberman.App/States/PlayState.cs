using System;
using System.IO;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Bomberman.Core;
using Bomberman.Core.Game;
using Bomberman.Core.Rollback;
using Bomberman.Net;

namespace Bomberman.App.States
{
    public class PlayState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        
        private GameSession _gameSession;
        private int _localPlayerId;
        private bool _isHost; // Derived from ID 0
        
        private double _accumulator = 0.0;
        private const double FixedTimeStep = 1.0 / 60.0;

        private bool _pendingBombInput = false;
        private KeyboardState _previousKeyboardState;

        public PlayState(GameContext context, GameStateManager manager, int localPlayerId, int playerCount, int seed)
        {
            _context = context;
            _manager = manager;
            _localPlayerId = localPlayerId;
            _isHost = _localPlayerId == 0;

            _gameSession = new GameSession(_localPlayerId, playerCount, seed);
            
            // Enable Validated Simulation if networked
            if (_context.Network != null)
            {
                _gameSession.RollbackSystem.SimulateNetworked = true;
                SetupLogging();
            }
        }

        private void SetupLogging()
        {
             string logFile = $"debug_log_player_{_localPlayerId}.txt";
             string role = _isHost ? "Host" : "Client";
             File.WriteAllText(logFile, $"--- {role} Start ---\n");
             
             if (_gameSession.Simulation != null)
             {
                _gameSession.Simulation.Log = (msg) => {
                    string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Frame {_gameSession.CurrentFrame}] {msg}\n";
                    File.AppendAllText(logFile, line);
                    // Console.Write(line); // Optional spam
                };
             }
        }

        public void Enter()
        {
            Console.WriteLine($"[PlayState] Enter. P{_localPlayerId}");
            if (_context.Network != null)
            {
                _context.Network.OnInputReceived += HandleInputReceived;
                _context.Network.OnDiscoveryRequestReceived += HandleDiscoveryRequest;
            }
            _previousKeyboardState = Keyboard.GetState();
        }

        public void Exit()
        {
             Console.WriteLine("[PlayState] Exit");
             if (_context.Network != null)
             {
                 _context.Network.OnInputReceived -= HandleInputReceived;
                 _context.Network.OnDiscoveryRequestReceived -= HandleDiscoveryRequest;
                 // Don't close network, might go back to Lobby or used here? 
                 // Actually if we leave PlayState (Escape), we go to Menu, so Close() happens manually or in flow.
             }
        }

        public void Update(GameTime gameTime)
        {
            if (_context.Network != null) _context.Network.Update();

            var kState = Keyboard.GetState();
            if (kState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                // Clean exit
                if (_context.Network != null)
                {
                    _context.Network.Close();
                    _context.Network = null;
                }
                
                // Save Replay
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string replayPath = Path.Combine("Replays", $"replay_{timestamp}.json");
                _gameSession.SaveReplay(replayPath);

                _manager.ChangeState(new MenuState(_context, _manager));
                return;
            }

            // Fixed Update Loop
            _accumulator += gameTime.ElapsedGameTime.TotalSeconds;

            // Input Latching
            if (kState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space))
            {
                _pendingBombInput = true;
            }

            while (_accumulator >= FixedTimeStep)
            {
                StepSimulation(kState);
                _accumulator -= FixedTimeStep;
            }

            _previousKeyboardState = kState;
        }

        private void StepSimulation(KeyboardState keyboardState)
        {
            // Capture Local Input
            IntVector2 movement = IntVector2.Zero;
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) movement.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) movement.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) movement.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) movement.X += 1;

            bool placeBomb = _pendingBombInput;
            _pendingBombInput = false;

             // Calculate explicit bomb target
             IntVector2 bombTarget = new IntVector2(0, 0);
             if (placeBomb)
             {
                 IntVector2 myPos = IntVector2.Zero;
                 if (_gameSession.Simulation != null)
                 {
                     var world = _gameSession.Simulation.World;
                     var pPool = world.Players;
                     for(int i=0; i<pPool.Count; i++)
                     {
                         if (pPool.Get(i).PlayerId == _localPlayerId)
                         {
                             var e = pPool.GetEntity(i);
                             if (world.Transforms.Has(e))
                                myPos = world.Transforms.Get(e).Position;
                             break;
                         }
                     }
                 }
                 
                 int pixelX = myPos.X / Simulation.SubpixelScale;
                 int pixelY = myPos.Y / Simulation.SubpixelScale;
                 
                 int centerX = pixelX + 12;
                 int centerY = pixelY + 12;
                 bombTarget = new IntVector2(centerX / 32, centerY / 32);
             }

            InputState localInput = new InputState { Movement = movement, PlaceBomb = placeBomb, BombTarget = bombTarget };
            _gameSession.Update(localInput);

            // Network Send
            if (_context.Network != null && _gameSession.TryBuildOutgoingBundle(out var bundle))
            {
                _context.Network.SendInput(bundle);
            }
        }

        private void HandleInputReceived(int pid, int startFrame, InputState[] inputs, IntVector2 remotePos, int remoteHash)
        {
            _gameSession.HandleRemoteInput(pid, startFrame, inputs, remotePos, remoteHash);

            // Host Relay
            if (_isHost && _context.Network != null && pid != 0)
            {
                 byte[] packet = NetworkProtocol.CreateInputPacket(pid, startFrame, inputs, remotePos, remoteHash);
                 foreach(var client in _context.Network.ConnectedClients)
                 {
                     _context.Network.RelayPacket(client, packet);
                 }
            }
        }

        private void HandleDiscoveryRequest(IPEndPoint sender, string header, int cur, int max)
        {
             if (_isHost && _context.Network != null)
            {
                // We assume we know player count here? 
                // PlayState constructor knows 'playerCount'.
                _context.Network.SendDiscoveryResponse(sender, "Local Game", _context.Network.ConnectedClients.Count() + 1, _gameSession.TotalPlayers);
            }
        }

        // --- Drawing ---

        public void Draw(GameTime gameTime)
        {
            _context.Game.GraphicsDevice.Clear(Color.CornflowerBlue);
            _context.SpriteBatch.Begin(samplerState: Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp);

            if (_gameSession.Simulation != null)
            {
                DrawWorld(_gameSession.Simulation.World);
            }

            _context.SpriteBatch.End();
        }

        private void DrawWorld(World world)
        {
            // Reuse logic from Game1.DrawGame (IntVector2 refactored vers)
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

        private Vector2 ToVec2(IntVector2 v)
        {
            return new Vector2(v.X, v.Y) / (float)Simulation.SubpixelScale;
        }

        private void DrawRect(Vector2 pos, Vector2 size, Color color)
        {
             _context.SpriteBatch.Draw(_context.PixelTexture, new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y), color);
        }

        private TransformComponent FindTransform(Entity entity, List<Entity> transformEntities, List<TransformComponent> transforms)
        {
            for(int i=0; i<transformEntities.Count; i++)
            {
                if(transformEntities[i].Equals(entity))
                    return transforms[i];
            }
            return new TransformComponent();
        }
    }
}
