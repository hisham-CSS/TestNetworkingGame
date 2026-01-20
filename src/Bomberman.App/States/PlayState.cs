using System;
using System.IO;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Bomberman.App.Rendering;
using Bomberman.Core;
using Bomberman.Core.Input;
using Bomberman.Core.ECS.Components;
using Bomberman.Core.Game;
using Chronos.Rollback;
using Chronos.Net;

namespace Bomberman.App.States
{
    /// <summary>
    /// The core gameplay state.
    /// Manages the GameSession, handles the game loop (Update), integrates with the RollbackSystem,
    /// and delegates rendering to WorldRenderer.
    /// </summary>
    public class PlayState : Bomberman.App.States.IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private WorldRenderer _worldRenderer;
        
        private GameSession _gameSession;
        private int _localPlayerId;
        private bool _isHost; // Derived from ID 0
        
        private double _accumulator = 0.0;
        private const double FixedTimeStep = 1.0 / 60.0;


        private bool _isReplayView = false;
        private bool _showDebugOverlay = false;
        private IPEndPoint?[] _clientSlots;
        
        // Rate Limiting for StateSync
        private System.Collections.Generic.Dictionary<int, double> _lastSyncSent = new System.Collections.Generic.Dictionary<int, double>();

        public PlayState(GameContext context, GameStateManager manager, int localPlayerId, int playerCount, int seed, IPEndPoint?[]? lobbySlots = null)
        {
            _context = context;
            _manager = manager;
            _worldRenderer = new WorldRenderer(_context.Renderer);
            _localPlayerId = localPlayerId;
            _isHost = _localPlayerId == 0;

            _gameSession = new GameSession(_localPlayerId, playerCount, seed);
            
            // Initialize Slots from Lobby
            if (_isHost)
            {
                if (lobbySlots != null)
                {
                    // Copy slots directly. Note: lobbySlots has size 4 usually.
                    // _clientSlots expects size (playerCount - 1).
                    // Mapping: P1 -> Index 0.
                    _clientSlots = new IPEndPoint?[Math.Max(playerCount - 1, 0)];
                    for(int i=0; i<_clientSlots.Length; i++)
                    {
                        // Player i+1
                        int pid = i + 1;
                        if (pid < lobbySlots.Length)
                        {
                            _clientSlots[i] = lobbySlots[pid];
                        }
                    }
                }
                else
                {
                    _clientSlots = new IPEndPoint?[Math.Max(playerCount - 1, 0)];
                }
            }
            else
            {
                _clientSlots = new IPEndPoint?[0];
            }
            
            // Enable Validated Simulation if networked
            if (_context.Network != null)
            {
                _gameSession.RollbackSystem.SimulateNetworked = true;
                SetupLogging();
            }
        }

        public PlayState(GameContext context, GameStateManager manager, GameSession session)
        {
            _context = context;
            _manager = manager;
            _worldRenderer = new WorldRenderer(_context.Renderer);
            _gameSession = session;
            _localPlayerId = 0; // Default view for replay
            _isHost = false; 
            _isReplayView = true;
            _clientSlots = new IPEndPoint?[0];
        }

        public PlayState(GameContext context, GameStateManager manager, GameSession restoredSession, int localId)
        {
            _context = context;
            _manager = manager;
            _worldRenderer = new WorldRenderer(_context.Renderer);
            _gameSession = restoredSession;
            _localPlayerId = localId; 
            _isHost = (localId == 0);
            _isReplayView = false;
            
            if (_isHost)
            {
                _clientSlots = new IPEndPoint?[_gameSession.TotalPlayers - 1];
            }
            else
            {
                _clientSlots = new IPEndPoint?[0];
            }
            
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



        private KeyboardState _prevKeyboard;

        public void Enter()
        {
            _context.Logger.Info($"[PlayState] Enter. P{_localPlayerId} Replay={_isReplayView}");
            if (_context.Network != null)
            {
                _context.Network.OnInputReceived += HandleInputReceived;
                _context.Network.OnDiscoveryRequestReceived += HandleDiscoveryRequest;
                _context.Network.OnDisconnected += HandleDisconnected;
                _context.Network.OnJoinRequestRaw += HandleJoinRequest;
                _context.Network.OnStateSyncReceived += HandleStateSyncReceived;
                
                // Host: Snapshot clients to map to PlayerIDs
                if (_isHost)
                {
                   // Slots are already set in constructor from LobbyState.
                   // We trust them.
                }
            }
        }

        public void Exit()
        {
             _context.Logger.Info("[PlayState] Exit");
             if (_context.Network != null)
             {
                 _context.Network.OnInputReceived -= HandleInputReceived;
                 _context.Network.OnDiscoveryRequestReceived -= HandleDiscoveryRequest;
                 _context.Network.OnDisconnected -= HandleDisconnected;
                 _context.Network.OnJoinRequestRaw -= HandleJoinRequest;
                 _context.Network.OnStateSyncReceived -= HandleStateSyncReceived;
             }
        }
        
        private void HandleDisconnected(IPEndPoint sender, string reason)
        {
            if (_isHost)
            {
                // Find slot
                for(int i=0; i<_clientSlots.Length; i++)
                {
                    if (_clientSlots[i] is IPEndPoint slot && slot.Equals(sender))
                    {
                        _clientSlots[i] = null;
                        int pid = i + 1;
                        _context.Logger.Info($"[PlayState] Player {pid} Disconnected: {reason}");
                        _gameSession.DisconnectPlayer(pid);
                        _context.Network?.RemoveClient(sender);
                        break;
                    }
                }
            }
            else
            {
                // Client: If we receive Disconnect, it's likely from Host (or we timed out host)
                _context.Logger.Info($"[PlayState] Disconnected from Host: {reason}");
                // Force return to menu
                _manager.ChangeState(_context.StateFactory.CreateMenu());
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_context.Network != null) _context.Network.Update();

            // Menu/Escape Check
            if (_context.Input.IsMenuCancel())
            {
                // Clean exit
                if (_context.Network != null)
                {
                    _context.Network.Close();
                    _context.Network = null;
                }
                
                // Save Replay only if NOT viewing one
                if (!_isReplayView)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string replayDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Replays");
                    if (!Directory.Exists(replayDir)) Directory.CreateDirectory(replayDir);
                    
                    string replayPath = Path.Combine(replayDir, $"replay_{timestamp}.json");
                    _gameSession.SaveReplay(replayPath);
                }

                _manager.ChangeState(_context.StateFactory.CreateMenu());
                return;
            }

            if (_context.Input.IsDebugToggle())
            {
                _showDebugOverlay = !_showDebugOverlay;
            }
            
            // Lag Simulation Controls
            if (_context.Network != null && _context.Network.Transport is SimulatedLagTransport lagTransport)
            {
                if (_context.Input.GetKeyboard().IsKeyDown(Keys.F5) && !_prevKeyboard.IsKeyDown(Keys.F5))
                {
                    lagTransport.LatencyMs = Math.Max(0, lagTransport.LatencyMs - 50);
                }
                if (_context.Input.GetKeyboard().IsKeyDown(Keys.F6) && !_prevKeyboard.IsKeyDown(Keys.F6))
                {
                    lagTransport.LatencyMs += 50;
                }
            }

            _prevKeyboard = _context.Input.GetKeyboard();
            
            // Fixed Update Loop
            _accumulator += gameTime.ElapsedGameTime.TotalSeconds;

            // Input Latching (Removed, handled by Service)

            // Synchronization Logic (Catch-Up & Slow-Down) - Client Only
            int maxSteps = 1;

            if (!_isHost && _context.Network != null && !_isReplayView)
            {
                // We assume Player 0 is Host
                int hostFrame = _gameSession.RollbackSystem.GetLastConfirmedFrame(0);
                int myFrame = _gameSession.CurrentFrame;
                
                maxSteps = _gameSession.RollbackSystem.CalculateTargetSteps(myFrame, hostFrame);
            }
            
            int stepsTaken = 0;

            while (_accumulator >= FixedTimeStep && stepsTaken < maxSteps)
            {
                StepSimulation();
                _accumulator -= FixedTimeStep;
                stepsTaken++;
                
                bool isGameOver = _gameSession.Simulation != null && _gameSession.Simulation.IsGameOver;
                bool isReplayEnd = _gameSession.RollbackSystem.IsReplayFinished;

                if (isGameOver || isReplayEnd)
                {
                    int winner = -1;
                    if (_gameSession.Simulation != null) winner = _gameSession.Simulation.WinnerId;

                    // Transition to Game Over (Flag replay status)
                    _manager.ChangeState(_context.StateFactory.CreateGameOver(_gameSession, winner, _isReplayView, isGameOver));
                    return;
                }
            }
            
            // If we have remaining steps/inputs but no accumulated time, we still process them to catch up.
            // This effectively "creates time" to synchronize with the host.
            
            if (stepsTaken < maxSteps)
            {
                // Force run remaining steps
                while (stepsTaken < maxSteps)
                {
                     StepSimulation();
                     // Do NOT subtract accumulator, we are creating time from nothing (Catching up)
                     stepsTaken++;
                     
                      bool isGameOver = _gameSession.Simulation != null && _gameSession.Simulation.IsGameOver;
                      if (isGameOver) 
                      {
                            int winner = _gameSession.Simulation!.WinnerId;
                             _manager.ChangeState(_context.StateFactory.CreateGameOver(_gameSession, winner, _isReplayView, isGameOver));
                            return;
                      }
                }
            }            
            
            // Clamp accumulator if spiraling?
            if (_accumulator > FixedTimeStep * 2) _accumulator = FixedTimeStep;
        }

        private void StepSimulation()
        {
            // Capture Local Input via Service
            var input = _context.Input.GetGameInput(_localPlayerId);
            
            IntVector2 movement = input.Movement;
            bool placeBomb = input.PlaceBomb; // Already pulsed by Service

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
                _context.Network.SendInput(bundle.PlayerId, bundle.Frame, bundle.RedundantHistory, bundle.LocalPosX, bundle.LocalPosY, bundle.LocalStateHash);
            }
        }

        private void HandleInputReceived(int pid, int startFrame, InputState[] inputs, int remoteX, int remoteY, int remoteHash)
        {
            IntVector2 remotePos = new IntVector2(remoteX, remoteY);
            var result = _gameSession.HandleRemoteInput(pid, startFrame, inputs, remotePos, remoteHash);

            if (result == InputResult.TooOld && _isHost && _context.Network != null)
            {
                // Player is desynced beyond recovery (lagged out). Force Resync.
                // We need the endpoint for this PID.
                if (pid > 0 && pid <= _clientSlots.Length && _clientSlots[pid-1] != null)
                {
                    // Use Wall Clock time for sync throttling
                    double nowSec = DateTime.Now.TimeOfDay.TotalSeconds;
                    
                    if (!_lastSyncSent.ContainsKey(pid) || (nowSec - _lastSyncSent[pid]) > 1.0)
                    {
                        var endpoint = _clientSlots[pid-1];
                        _context.Logger.Warning($"[PlayState] P{pid} inputs too old (Freeze detected). Forcing Resync to Frame {_gameSession.CurrentFrame}.");
                        
                        byte[] snapshot = GameStateSnapshot.SerializeWorld(_gameSession.CurrentFrame, _gameSession.Simulation!.World, _gameSession.Simulation.Rng.State);
                        _context.Network.SendStateSync(endpoint!, snapshot);
                        
                        _lastSyncSent[pid] = nowSec;
                        
                        
                        _lastSyncSent[pid] = nowSec;
                    }
                }
            }

            // Host Relay
            if (_isHost && _context.Network != null && pid != 0)
            {
                 byte[] packet = Chronos.Net.Protocol.NetworkProtocol<InputState>.CreateInputPacket(pid, startFrame, inputs, remotePos.X, remotePos.Y, remoteHash);
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
                 // PlayState knows the total player count from its constructor/session.
                _context.Network.SendDiscoveryResponse(sender, "Local Game", _context.Network.ConnectedClients.Count() + 1, _gameSession.TotalPlayers);
            }
        }

        private void HandleJoinRequest(IPEndPoint sender)
        {
            if (!_isHost || _context.Network == null) return;
            
            _context.Logger.Info($"[PlayState] Join Request from {sender}");

            int assignedId = -1;
            
            // Check if already in a slot
            for(int i=0; i<_clientSlots.Length; i++)
            {
                if (_clientSlots[i] is IPEndPoint slot && slot.Equals(sender))
                {
                    assignedId = i + 1;
                    break;
                }
            }

            if (assignedId == -1)
            {
                // Find free slot
                for(int i=0; i<_clientSlots.Length; i++)
                {
                    if (_clientSlots[i] == null)
                    {
                        _clientSlots[i] = sender;
                        assignedId = i + 1;
                        _context.Network.AddClient(sender);
                        break;
                    }
                }

                if (assignedId == -1)
                {
                     _context.Logger.Info($"[PlayState] Server Full. Rejecting {sender}");
                     _context.Network.SendDisconnect(sender, "Server is Full");
                     return;
                }
            }

            // Sync State
            // 1. Send Welcome (so they get their ID)
            // Note: We use the CURRENT seed, but they will overwrite World anyway.
            _context.Network.SendWelcome(sender, assignedId, (int)_gameSession.Simulation!.Rng.State, _gameSession.TotalPlayers);

            // 2. Serialize World
            byte[] snapshot = GameStateSnapshot.SerializeWorld(_gameSession.CurrentFrame, _gameSession.Simulation!.World, _gameSession.Simulation.Rng.State);
            
            // 3. Send State Sync
            _context.Logger.Info($"[PlayState] Sending StateSync ({snapshot.Length} bytes) to P{assignedId} ({sender})");
            _context.Network.SendStateSync(sender, snapshot);
        }

        private void HandleStateSyncReceived(byte[] snapshotData)
        {
             if (_gameSession.Simulation == null) return;
             
             // Restore
             int frame = GameStateSnapshot.RestoreFromBytes(_gameSession.Simulation.World, _gameSession.Simulation.Rng, snapshotData);
             
             _context.Logger.Info($"[PlayState] Received StateSync (Jump to Frame {frame})");
             
             // Sync Rollback System
             _gameSession.RollbackSystem.SyncToFrame(frame);
        }

        // --- Drawing ---

        public void Draw(GameTime gameTime)
        {
            _context.Renderer.ClearScreen(Color.CornflowerBlue);
            _context.Renderer.BeginDraw();

            if (_gameSession.Simulation != null)
            {
                _worldRenderer.DrawWorld(_gameSession.Simulation.World);
            }

            if (_showDebugOverlay)
            {
                DrawDebugStats();
            }

            _context.Renderer.EndDraw();
        }

        private void DrawDebugStats()
        {
            int y = 10;
            int x = 10;
            Color c = Color.Yellow;

            // Ping
            int ping = _context.Network != null ? _context.Network.LastPingMs : 0;
            _context.Renderer.DrawText($"Ping: {ping}ms", x, y, c, 2);
            y += 20;

            // Local Frame
            int currentFrame = _gameSession.RollbackSystem.CurrentFrame;
            _context.Renderer.DrawText($"Frame: {currentFrame}", x, y, c, 2);
            y += 20;

            // Frame Advantage / Delays
            // Show for each remote player
            for(int i=0; i<_clientSlots.Length; i++)
            {
                 int pid = i + 1;
                 if (_clientSlots[i] != null) // If slot occupied
                 {
                      int lastConfirmed = _gameSession.RollbackSystem.GetLastConfirmedFrame(pid);
                      int advantage = currentFrame - lastConfirmed;
                      string status = advantage > 0 ? $"+{advantage}" : $"{advantage}";
                      _context.Renderer.DrawText($"P{pid} Adv: {status}", x, y, c, 2);
                      y += 20;
                 }
            }

            if (_context.Network != null && _context.Network.Transport is SimulatedLagTransport lag)
            {
                y += 20;
                _context.Renderer.DrawText($"Simulated Lag: {lag.LatencyMs}ms (F5/F6)", x, y, Color.Orange, 2);
            }
        }
    }
}
