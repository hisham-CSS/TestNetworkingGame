using System;
using System.IO;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.Net;
using Bomberman.Core.Game;
using Bomberman.Rollback;

namespace Bomberman.App.States
{
    /// <summary>
    /// Handles the pre-game lobby.
    /// Manages player connections, slot assignment, and the "Ready" status before starting the match.
    /// </summary>
    public class LobbyState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private bool _isHost;

        // Lobby Data
        private int _connectedPlayerCount = 1;
        private int _totalPlayersForGame = 2;
        private int _networkSeed;
        private int _localPlayerId = -1; // -1 for client initially
        private float _joinRetryTimer = 0;
        private KeyboardState _prevKeyboard;
        
        // Stable Slots: [0]=Host(P0), [1]=P1, [2]=P2, [3]=P3
        // Host is always implicitly in slot 0 if hosting.
        // We only really need to track clients for slots 1,2,3.
        // But let's keep it simple: Size 4.
        private IPEndPoint?[] _lobbySlots = new IPEndPoint?[4];
        
        private Dictionary<int, bool> _playerReady = new Dictionary<int, bool>();
        private bool _amIReady = false;

        public LobbyState(GameContext context, GameStateManager manager, bool isHost, IPEndPoint? hostEndpoint)
        {
            _context = context;
            _manager = manager;
            _isHost = isHost;
            
            if (_isHost)
            {
                _localPlayerId = 0;
                _networkSeed = new Random().Next();
                // Reserve Slot 0 for Host
                _connectedPlayerCount = 1;
            }
            else if (hostEndpoint != null && _context.Network != null)
            {
                // Client: Connect to selected host
                _context.Network.Connect(hostEndpoint.Address.ToString(), hostEndpoint.Port);
            }
        }

        public void Enter()
        {
            _context.Logger.Info($"[LobbyState] Enter. Host={_isHost}");
            _prevKeyboard = _context.Input.GetKeyboard();

            if (_context.Network != null)
            {
                _context.Network.OnJoinRequestRaw += HandleJoinRequest;
                _context.Network.OnWelcomeReceived += HandleWelcome;
                _context.Network.OnLobbyUpdateReceived += HandleLobbyUpdate;
                _context.Network.OnStartGameReceived += HandleStartGame;
                _context.Network.OnDiscoveryRequestReceived += HandleDiscoveryRequest;
                _context.Network.OnDisconnected += HandleDisconnected;
                _context.Network.OnLobbyReadyReceived += HandleLobbyReady;
                _context.Network.OnStateSyncReceived += HandleStateSync;
            }

            if (!_isHost)
            {
                // Client: Send initial join request
                _context.Network?.SendJoinRequest();
                _context.Logger.Info("Sent Join Request...");
                _joinRetryTimer = 1.0f;
            }
        }

        public void Exit()
        {
             _context.Logger.Info("[LobbyState] Exit");
             if (_context.Network != null)
             {
                 _context.Network.OnJoinRequestRaw -= HandleJoinRequest;
                 _context.Network.OnWelcomeReceived -= HandleWelcome;
                 _context.Network.OnLobbyUpdateReceived -= HandleLobbyUpdate;
                 _context.Network.OnStartGameReceived -= HandleStartGame;
                 _context.Network.OnDiscoveryRequestReceived -= HandleDiscoveryRequest;
                 _context.Network.OnDisconnected -= HandleDisconnected;
                 _context.Network.OnLobbyReadyReceived -= HandleLobbyReady;
                 _context.Network.OnStateSyncReceived -= HandleStateSync;
             }
        }

        private void HandleDisconnected(IPEndPoint sender, string reason)
        {
             if (!_isHost)
             {
                 _context.Logger.Info($"[Lobby] Host Disconnected: {reason}");
                 ReturnToMenu(reason);
             }
             else
             {
                  _context.Logger.Info($"[Lobby] Client {sender} Disconnected: {reason}");
                  
                  // Find and remove from slot
                  for(int i=1; i<4; i++) // Slot 0 is Host, ignore
                  {
                      if (_lobbySlots[i] != null && _lobbySlots[i].Equals(sender))
                      {
                          _lobbySlots[i] = null;
                          break;
                      }
                  }

                  // Recalculate count
                  int count = 1; // Host
                  for(int i=1; i<4; i++) if (_lobbySlots[i] != null) count++;
                  _connectedPlayerCount = count;

                  int mask = 0;
                  for(int i=0; i<4; i++) if (i==0 || _lobbySlots[i] != null) mask |= (1 << i);
                  _context.Network?.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame, mask);
             }
        }
        
        private void ReturnToMenu(string reason = "")
        {
             if (_context.Network != null)
            {
                _context.Network.Close();
                _context.Network = null;
            }
            
            if (!string.IsNullOrEmpty(reason))
            {
                _manager.ChangeState(_context.StateFactory.CreatePrompt(reason, () => 
                {
                    _manager.ChangeState(_context.StateFactory.CreateMenu());
                }));
            }
            else
            {
                _manager.ChangeState(_context.StateFactory.CreateMenu());
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_context.Network != null) _context.Network.Update();
            
            // Use Input Service so tests can Mock it
            var kState = _context.Input.GetKeyboard();
            if (kState.IsKeyDown(Keys.Escape))
            {
                // Back to Menu
                // Host should close server
                if (_isHost)
                {
                    _context.Network?.Close();
                   // Re-open as client/browser? 
                   // Or just clear network.
                   _context.Network = null;
                }
                else
                {
                    // Client leaves
                    _context.Network?.Close();
                    _context.Network = null;
                }
                _manager.ChangeState(_context.StateFactory.CreateMenu());
                return;
            }

            if (!_isHost && _localPlayerId == -1 && _context.Network != null)
            {
                _joinRetryTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_joinRetryTimer <= 0)
                {
                    _context.Network.SendJoinRequest();
                    _context.Logger.Info("Resending Join Request...");
                    _joinRetryTimer = 1.0f;
                }
            }

            if (_isHost)
            {
                int prevPlayers = _totalPlayersForGame;
                if (kState.IsKeyDown(Keys.D2)) _totalPlayersForGame = 2;
                if (kState.IsKeyDown(Keys.D3)) _totalPlayersForGame = 3;
                if (kState.IsKeyDown(Keys.D4)) _totalPlayersForGame = 4;

                if (prevPlayers != _totalPlayersForGame)
                {
                    int mask = 0;
                    for(int i=0; i<4; i++) if (i==0 || _lobbySlots[i] != null) mask |= (1 << i);
                    _context.Network?.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame, mask);
                }

                if (kState.IsKeyDown(Keys.Enter))
                {
                    bool allReady = true;
                    // Enforce that everyone connected must be Ready
                    for (int i = 0; i < _connectedPlayerCount; i++)
                    {
                        if (!_playerReady.ContainsKey(i) || !_playerReady[i]) allReady = false;
                    }

                    if (_connectedPlayerCount >= _totalPlayersForGame && allReady)
                    {
                         _context.Network?.BroadcastStartGame(_networkSeed, _totalPlayersForGame);
                         StartGame(_networkSeed, _totalPlayersForGame);
                    }
                    else
                    {
                         // Feedback?
                    }
                }
            }
            
            // Ready Toggle
            if (kState.IsKeyDown(Keys.Space) && !_prevKeyboard.IsKeyDown(Keys.Space))
            {
                _amIReady = !_amIReady;
                if (_localPlayerId != -1)
                {
                    _playerReady[_localPlayerId] = _amIReady;
                    _context.Network?.SendLobbyReady(_localPlayerId, _amIReady);
                }
            }
            _prevKeyboard = kState;
        }

        private void HandleLobbyReady(int pid, bool isReady)
        {
            _playerReady[pid] = isReady;
            if (_isHost)
            {
                // Relay to everyone so they see the status
                _context.Network?.BroadcastLobbyReady(pid, isReady);
            }
        }

        private void HandleStateSync(byte[] data)
        {
             if (_isHost) return; // Should not happen
             
             try
             {
                 _context.Logger.Info($"[LobbyState] Received State Sync ({data.Length} bytes). Joining Game...");
                 
                 // 1. Create Game Session
                 var session = new GameSession(_localPlayerId, _totalPlayersForGame, _networkSeed);
                 
                 // 2. Restore State (Snapshot + RNG)
                 int frame = GameStateSnapshot.RestoreFromBytes(session.Simulation!.World, session.Simulation!.Rng, data);
                 
                 // 3. Sync Clock
                 session.RollbackSystem.SyncToFrame(frame);
                 
                 // 4. Transition to PlayState
                 // Use the new constructor for restored sessions
                 _manager.ChangeState(_context.StateFactory.CreatePlay(session, _localPlayerId));
             }
             catch (Exception e)
             {
                 _context.Logger.Error($"[LobbyState] CRASH during State Sync: {e}", e);
                 ReturnToMenu($"Join Failed: {e.Message}");
             }
        }

        private void StartGame(int seed, int totalPlayers)
        {
             // Transition to PlayState
             // We need to pass the lobby slots so PlayState knows who is who
             _manager.ChangeState(_context.StateFactory.CreatePlay(_localPlayerId, totalPlayers, seed, _lobbySlots));
        }

        // --- Network Handlers ---

        private void HandleJoinRequest(IPEndPoint sender)
        {
            if (!_isHost || _context.Network == null) return;

             int existingSlot = -1;
             
             // Check if already in a slot
             for(int i=1; i<4; i++)
             {
                 if (_lobbySlots[i] != null && _lobbySlots[i].Equals(sender))
                 {
                     existingSlot = i;
                     break;
                 }
             }

             if (existingSlot != -1)
             {
                 // Client is already connected (retry)
                 _context.Logger.Info($"[LobbyState] Resending Welcome to existing client in Slot {existingSlot} ({sender})");
                 
                 _context.Network.SendWelcome(sender, existingSlot, _networkSeed, _totalPlayersForGame);
                 _context.Network.SendLobbyReadyTo(sender, 0, _amIReady); 
                 
                 // Re-broadcast update just in case
                 int mask = 0;
                 for(int i=0; i<4; i++) if (i==0 || _lobbySlots[i] != null) mask |= (1 << i);
                 _context.Network.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame, mask); 
                 return;
             }

             // New Connection
             // Find Empty Slot
             int freeSlot = -1;
             // Limit search to _totalPlayersForGame? 
             // Yes, we shouldn't fill slots > totalPlayers.
             for(int i=1; i<_totalPlayersForGame && i < 4; i++)
             {
                 if (_lobbySlots[i] == null)
                 {
                     freeSlot = i;
                     break;
                 }
             }

             if (freeSlot != -1)
             {
                    _lobbySlots[freeSlot] = sender;
                    _context.Network.AddClient(sender);
                    
                    // Recalculate count
                    int count = 1; 
                    for(int i=1; i<4; i++) if (_lobbySlots[i] != null) count++;
                    _connectedPlayerCount = count;

                    int newId = freeSlot;
                    
                    _context.Network.SendWelcome(sender, newId, _networkSeed, _totalPlayersForGame);
                    
                    int mask = 0;
                    for(int i=0; i<4; i++) if (i==0 || _lobbySlots[i] != null) mask |= (1 << i);
                    _context.Network.BroadcastLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame, mask);

                    // Sync existing ready states to new client
                    foreach(var kvp in _playerReady)
                    {
                        if (kvp.Value)
                        {
                            _context.Network.SendLobbyReadyTo(sender, kvp.Key, true);
                        }
                    }

                    _context.Logger.Info($"Client Assigned to Slot {newId} ({sender})");
                }
                else
                {
                    _context.Logger.Info($"[LobbyState] Rejecting {sender}. No Slots Available or Lobby Full.");
                    _context.Network.SendDisconnect(sender, "Lobby is Full");
                }
            }
        

        private void HandleWelcome(int assignedId, int seed, int totalPlayers)
        {
            if (_isHost) return;
            if (_localPlayerId == -1)
            {
                _localPlayerId = assignedId;
                _networkSeed = seed;
                _totalPlayersForGame = totalPlayers;
                _context.Logger.Info($"Joined as Player {_localPlayerId}. seed={_networkSeed}");
            }
        }

        private void HandleLobbyUpdate(int connectedCount, int totalPlayers, int slotMask)
        {
            if (_isHost) return;
            _connectedPlayerCount = connectedCount;
            _totalPlayersForGame = totalPlayers;
            
            // Sync slots from mask
            for(int i=1; i<4; i++)
            {
                bool occupied = (slotMask & (1 << i)) != 0;
                if (occupied)
                {
                    if (_lobbySlots[i] == null)
                        _lobbySlots[i] = new IPEndPoint(IPAddress.None, 0); 
                }
                else
                {
                    _lobbySlots[i] = null;
                }
            }
        }

        private void HandleStartGame(int seed, int totalPlayers)
        {
            if (_isHost) return;
            _networkSeed = seed;
            _totalPlayersForGame = totalPlayers;
            StartGame(seed, totalPlayers);
        }

        private void HandleDiscoveryRequest(IPEndPoint sender, string header, int cur, int max)
        {
            if (_isHost && _context.Network != null)
            {
                _context.Network.SendDiscoveryResponse(sender, "Local Game", _connectedPlayerCount, _totalPlayersForGame);
            }
        }

        // --- Drawing ---

        public void Draw(GameTime gameTime)
        {
             _context.Renderer.ClearScreen(Color.CornflowerBlue);
             _context.Renderer.BeginDraw();

             int centerX = _context.Game.WindowWidth / 2;
             
             // If client and waiting for welcome (local ID -1), show connecting
             if (!_isHost && _localPlayerId == -1)
             {
                 _context.Renderer.DrawTextCentered("CONNECTING...", centerX, 300, Color.White, 4);
                 _context.Renderer.EndDraw();
                 return;
             }

             _context.Renderer.DrawTextCentered("LOBBY", centerX, 30, Color.Red, 8);
             
             _context.Renderer.DrawTextCentered($"PLAYERS: {_connectedPlayerCount} / {_totalPlayersForGame}", centerX, 70, Color.White, 3);
             
             // Instructions
             if (_isHost)
             {
                 _context.Renderer.DrawTextCentered("ADJUST PLAYER COUNT:  [2]   [3]   [4]", centerX, 100, Color.Yellow, 2);
             }
             
             // Slots
             int startY = 200;
             int slotHeight = 15;
             
             for(int i=0; i<_totalPlayersForGame; i++)
             {
                 string slotInfo = $"SLOT {i+1}:   ";
                 Color c = Color.DarkGray;
                 
                 // Check occupancy: Host (0) is always occupied if we are here. Others check slots.
                 bool occupied = (i == 0) || (_lobbySlots[i] != null);
                 
                 if (occupied)
                 {
                     bool ready = _playerReady.ContainsKey(i) && _playerReady[i];
                     slotInfo += ready ? "READY" : "NOT READY";
                     c = ready ? Color.Lime : Color.Orange;
                     
                     if (i == _localPlayerId) slotInfo += "  (YOU)";
                 }
                 else
                 {
                     slotInfo += "EMPTY";
                 }
                 
                 _context.Renderer.DrawTextCentered(slotInfo, centerX, startY + (i*slotHeight), c, 2);
             }

             // Footer Controls
             string statusMsg = "";
             Color statusColor = Color.Gray;

            bool allReady = true;
            for (int i = 0; i < _connectedPlayerCount; i++) if (!_playerReady.ContainsKey(i) || !_playerReady[i]) allReady = false;

            if (_isHost)
            {
                 if (_connectedPlayerCount >= _totalPlayersForGame && allReady)
                 {
                     statusMsg = "PRESS [ENTER] TO START GAME";
                     statusColor = Color.Green;
                 }
                 else
                 {
                     statusMsg = "WAITING FOR PLAYERS TO READY UP...";
                 }
             }
             else
             {
                if (_connectedPlayerCount >= _totalPlayersForGame && allReady)
                    statusMsg = "ALL PLAYERS READY. WAITING FOR HOST TO START...";
                else
                    statusMsg = "WAITING FOR PLAYERS TO READY UP...";
            }
             
             _context.Renderer.DrawTextCentered(statusMsg, centerX, 350, statusColor, 2);

             string readyMsg = _amIReady ? "PRESS [SPACE] TO UNREADY" : "PRESS [SPACE] TO READY UP";
             _context.Renderer.DrawTextCentered(readyMsg, centerX, 400, _amIReady ? Color.Cyan : Color.Magenta, 2);

             _context.Renderer.EndDraw();
        }
    }
}
