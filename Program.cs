using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Bomberman
{
    public class Game1 : Game
    {
        private Texture2D _pixelTexture = null!;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        
        private Simulation? _simulation;
        private KeyboardState _previousKeyboardState;
        
        private double _accumulator = 0.0;
        private const double FixedTimeStep = 1.0 / 60.0;

        // Replay System
        private InputRecorder _recorder = new InputRecorder();
        private bool _isRecording = false;
        private bool _isReplaying = false;
        private int _replayFrame = 0;
        private int randomSeed = 12345;

        // Networking
        private NetworkManager? _networkManager;
        private bool _isNetworked = false;
        private int _localPlayerId = 0; // 0 = Host, 1 = Client
        private Dictionary<int, Dictionary<int, InputState>> _remoteInputBuffer = new Dictionary<int, Dictionary<int, InputState>>(); // Frame -> PlayerId -> Input
        private Dictionary<int, InputState> _localInputBuffer = new Dictionary<int, InputState>(); // Frame -> LocalInput

        private int _currentFrame = 0;

        // Rollback System
        private Dictionary<int, GameStateSnapshot> _snapshotBuffer = new Dictionary<int, GameStateSnapshot>();
        private Dictionary<int, InputState> _lastConfirmedRemoteInputs = new Dictionary<int, InputState>();
        private Dictionary<int, int> _lastConfirmedRemoteFrame = new Dictionary<int, int>();
        private const int MaxSnapshotFrames = 120;
        private const int MaxPredictionFrames = 15; // Limit how far we can drift ahead of others // Keep 2 seconds of history at 60Hz

        // Game State
        private enum GameState { Menu, Lobby, Playing, Replaying, ServerBrowser }
        private GameState _state = GameState.Menu;
        private int _connectedPlayerCount = 1; // 1 = Self
        private int _totalPlayersForGame = 2; // Default
        private int _networkSeed = 12345;
        private int _menuSelection = 0; // 0 = Play (Local), 1 = Host, 2 = Join, 3 = Replay
        
        // Discovery
        private Dictionary<System.Net.IPEndPoint, (string name, int current, int max)> _foundServers = new Dictionary<System.Net.IPEndPoint, (string, int, int)>();
        private float _discoveryTimer = 0f;
        private int _browserSelection = 0;

        private float _joinRetryTimer = 0f;
        private bool _pendingBombInput = false;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window size to match Simulation (15x13 tiles at 32 pixels)
            _graphics.PreferredBackBufferWidth = 480;
            _graphics.PreferredBackBufferHeight = 416;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        protected override void Update(GameTime gameTime)
        {
            try
            {
                var keyboardState = Keyboard.GetState();
                
                // Network Update
                if (_networkManager != null) _networkManager.Update();

                switch (_state)
                {
                    case GameState.Menu:
                        UpdateMenu(keyboardState);
                        break;
                    case GameState.Lobby:
                        UpdateLobby(gameTime, keyboardState);
                        break;
                    case GameState.Playing:
                    case GameState.Replaying:
                        UpdateGame(gameTime, keyboardState);
                        break;
                    case GameState.ServerBrowser:
                        UpdateServerBrowser(gameTime, keyboardState);
                        break;
                }

                _previousKeyboardState = keyboardState;
                base.Update(gameTime);
            }
            catch(Exception e)
            {
                 Console.WriteLine("Update Crash: " + e.ToString());
                 throw;
            }
        }

        private void UpdateMenu(KeyboardState keyboardState)
        {
             if ((keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) && 
                !(_previousKeyboardState.IsKeyDown(Keys.W) || _previousKeyboardState.IsKeyDown(Keys.Up)))
            {
                _menuSelection--;
                if (_menuSelection < 0) _menuSelection = 3;
            }
            if ((keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) && 
                !(_previousKeyboardState.IsKeyDown(Keys.S) || _previousKeyboardState.IsKeyDown(Keys.Down)))
            {
                _menuSelection++;
                if (_menuSelection > 3) _menuSelection = 0;
            }

            if (keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space) ||
                keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                if (_menuSelection == 0) // Local Play
                {
                        _state = GameState.Playing;
                        _isRecording = true;
                        _isReplaying = false;
                        _isNetworked = false;
                        _localPlayerId = 0;
                        _recorder.Reset();
                        _simulation = new Simulation(randomSeed, 1);
                        string logFile = $"debug_log_player_{_localPlayerId}.txt";
                        File.WriteAllText(logFile, "--- Local Play Start ---\n");
                        _simulation.Log = (msg) => {
                            string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Frame {_currentFrame}] {msg}\n";
                            File.AppendAllText(logFile, line);
                            Console.Write(line);
                        };
                }
                else if (_menuSelection == 1) // Host
                {
                    _state = GameState.Lobby;
                    _isRecording = true; // REQUIRED FOR ROLLBACK HISTORY
                    _isReplaying = false;
                    _isNetworked = true;
                    _localPlayerId = 0;
                    _currentFrame = 0;
                    _remoteInputBuffer.Clear();
                    _networkSeed = new Random().Next();
                    _connectedPlayerCount = 1;
                    _totalPlayersForGame = 2; // Default 2P 
                    
                    _recorder.Reset(); // Clear old history

                    _networkManager = null;
                    for(int port = 5000; port < 5010; port++)
                    {
                        try 
                        {
                            _networkManager = new NetworkManager(port);
                            _networkManager.OnPacketReceived += OnNetworkPacket;
                            Console.WriteLine($"Hosting on Port {port}...");
                            break; 
                        }
                        catch(System.Net.Sockets.SocketException)
                        {
                            Console.WriteLine($"Port {port} busy, trying next...");
                            _networkManager = null;
                        }
                    }

                    if (_networkManager == null)
                    {
                         Console.WriteLine("Failed to bind any port (5000-5009)!");
                         _state = GameState.Menu; // Abort
                    }
                }
                else if (_menuSelection == 2) // Join -> Server Browser
                {
                    _state = GameState.ServerBrowser;
                    _isRecording = false;
                    _isReplaying = false;
                    _isNetworked = true;
                    _foundServers.Clear();
                    _discoveryTimer = 0f;
                    _browserSelection = 0;
                    
                    // Start network manager immediately for broadcast
                    if (_networkManager == null)
                    {
                        _networkManager = new NetworkManager(0); // Client on Ephemeral
                        _networkManager.OnPacketReceived += OnNetworkPacket;
                    }

                    Console.WriteLine("Entered Server Browser...");
                }
                else if (_menuSelection == 3) // Replay
                {
                    _state = GameState.Replaying;
                    _isRecording = false;
                    _isReplaying = true;
                    _isNetworked = false;
                    _replayFrame = 0;
                    _recorder.Load(Path.Combine("Replays", "replay.json"));
                    _simulation = new Simulation(randomSeed, 2); // Assume 2P replay for now
                }
            }
        }

        private void UpdateLobby(GameTime gameTime, KeyboardState keyboardState)
        {
             if (keyboardState.IsKeyDown(Keys.Escape))
            {
                _state = GameState.Menu;
                _networkManager?.Close();
                _networkManager = null;
            }

            // Client: Retry Join Request
            if (_localPlayerId == -1 && _networkManager != null)
            {
                _joinRetryTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_joinRetryTimer <= 0)
                {
                    _networkManager.Send(NetworkProtocol.CreateJoinRequest());
                        Console.WriteLine("Resending Join Request...");
                        _joinRetryTimer = 1.0f;
                }
            }

            if (_localPlayerId == 0) // HOST
            {
                    // Configure Players
                    int prevPlayers = _totalPlayersForGame;
                    if (keyboardState.IsKeyDown(Keys.D2)) _totalPlayersForGame = 2;
                    if (keyboardState.IsKeyDown(Keys.D3)) _totalPlayersForGame = 3;
                    if (keyboardState.IsKeyDown(Keys.D4)) _totalPlayersForGame = 4;
                    
                    if (prevPlayers != _totalPlayersForGame)
                    {
                        // Broadcast Update
                        byte[] update = NetworkProtocol.CreateLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);
                        _networkManager?.Broadcast(update);
                    }

                    // Start Game
                    if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                    {
                        // Check if we have enough connected players match the required count
                        if (_connectedPlayerCount >= _totalPlayersForGame) 
                        {
                            _networkManager?.Broadcast(NetworkProtocol.CreateStartGame(_networkSeed, _totalPlayersForGame));
                            _state = GameState.Playing;
                            _simulation = new Simulation(_networkSeed, _totalPlayersForGame);
                            string logFile = $"debug_log_player_{_localPlayerId}.txt";
                             File.WriteAllText(logFile, "--- Host Start ---\n");
                            _simulation.Log = (msg) => {
                                string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Frame {_currentFrame}] {msg}\n";
                                File.AppendAllText(logFile, line);
                                Console.Write(line);
                            };
                        }
                        else
                        {
                            Console.WriteLine($"Cannot start: {_connectedPlayerCount}/{_totalPlayersForGame} players ready.");
                        }
                    }
            }
        }

        private void UpdateServerBrowser(GameTime gameTime, KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                 _state = GameState.Menu;
                 _networkManager?.Close();
                 _networkManager = null;
                 return;
            }

            // Periodic Broadcast
            _discoveryTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_discoveryTimer <= 0)
            {
                // Broadcast to ALL possible host ports
                for(int p=5000; p<5010; p++)
                {
                    _networkManager?.BroadcastToPort(NetworkProtocol.CreateDiscoveryRequest(), p);
                }
                _discoveryTimer = 2.0f; // Retry every 2s
            }

            // Navigation
            if ((keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) && 
               !(_previousKeyboardState.IsKeyDown(Keys.W) || _previousKeyboardState.IsKeyDown(Keys.Up)))
            {
               _browserSelection--;
               if (_browserSelection < 0) _browserSelection = _foundServers.Count - 1; 
               if (_browserSelection < 0) _browserSelection = 0;
            }
            if ((keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) && 
               !(_previousKeyboardState.IsKeyDown(Keys.S) || _previousKeyboardState.IsKeyDown(Keys.Down)))
            {
               _browserSelection++;
               if (_browserSelection >= _foundServers.Count) _browserSelection = 0;
            }

            // Selection
            if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                if (_foundServers.Count > 0 && _browserSelection < _foundServers.Count)
                {
                    // Connect!
                    var endpoint = new List<System.Net.IPEndPoint>(_foundServers.Keys)[_browserSelection];
                    _networkManager?.Connect(endpoint.Address.ToString(), endpoint.Port);
                    
                    _state = GameState.Lobby;
                    _localPlayerId = -1;
                    _currentFrame = 0;
                    _remoteInputBuffer.Clear();
                    
                    // Send Join
                    _networkManager?.Send(NetworkProtocol.CreateJoinRequest());
                    Console.WriteLine($"Joining {endpoint}...");
                    _joinRetryTimer = 1.0f;
                }
            }
        }
        private void UpdateGame(GameTime gameTime, KeyboardState keyboardState)
        {
             if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                _state = GameState.Menu;
                if (_networkManager != null) { _networkManager.Close(); _networkManager = null; }
                if (_isRecording) _recorder.Save(Path.Combine("Replays", "replay.json"));
            }

            // Fixed Update Loop
            _accumulator += gameTime.ElapsedGameTime.TotalSeconds;

            // Input Latching (Capture "Just Pressed" events that happen between steps)
            if (keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space))
            {
                _pendingBombInput = true;
            }

            while (_accumulator >= FixedTimeStep)
            {
                StepSimulation(keyboardState); // Extracted method logic
                _accumulator -= FixedTimeStep;
            }
        }

        private void OnNetworkPacket(byte[] data, System.Net.IPEndPoint sender)
        {
            PacketType type = NetworkProtocol.ReadType(data);

            switch (type)
            {
                case PacketType.JoinRequest:
                    if (_localPlayerId == 0 && _state == GameState.Lobby) // Only Host handles this
                    {
                         // Check if this client is already connected (dedup JoinRequests)
                         // Note: We need to be careful. If a client crashes and rejoins from same port, we might want to allow it?
                         // For now, simple strict check.
                         bool alreadyConnected = false;
                         foreach(var c in _networkManager.ConnectedClients)
                         {
                             if (c.Equals(sender)) 
                             {
                                 alreadyConnected = true; 
                                 break;
                             }
                         }

                         if (!alreadyConnected)
                         {
                            if (_connectedPlayerCount < _totalPlayersForGame)
                            {
                                _networkManager?.AddClient(sender);
                                int newId = _connectedPlayerCount;
                                _connectedPlayerCount++;
                                
                                // Send Welcome
                                byte[] welcome = NetworkProtocol.CreateWelcome(newId, _networkSeed, _totalPlayersForGame);
                                _networkManager?.SendTo(welcome, sender);

                                // Broadcast Lobby Update to everyone
                                byte[] update = NetworkProtocol.CreateLobbyUpdate(_connectedPlayerCount, _totalPlayersForGame);
                                _networkManager.Broadcast(update); // Broadcast is now correct for updates since all valid clients are in list

                                Console.WriteLine($"Client {newId} Joined from {sender}");
                            }
                         }
                         else
                         {
                             // Already connected? Maybe resend welcome or ignore.
                             // For robustness against packet loss, we probably SHOULD resend welcome, but NOT increment ID or Count.
                             // But since we don't track which ID belongs to which IP in a map easily here (without searching), 
                             // lets just ignore for now to stop the bug. The client retries join anyway.
                         }
                    }
                    break;
                
                case PacketType.Welcome:
                    if (_localPlayerId == -1) // Client waiting for welcome
                    {
                        var (assignedId, seed, totalPlayers) = NetworkProtocol.ReadWelcome(data);
                        _localPlayerId = assignedId;
                        _networkSeed = seed;
                        _totalPlayersForGame = totalPlayers;
                        Console.WriteLine($"Joined as Player {_localPlayerId}. seed={_networkSeed}");
                    }
                    break;

                case PacketType.LobbyUpdate:
                    if (_state == GameState.Lobby)
                    {
                        var (connectedCount, totalPlayers) = NetworkProtocol.ReadLobbyUpdate(data);
                        _connectedPlayerCount = connectedCount;
                        _totalPlayersForGame = totalPlayers;
                    }
                    break;

                case PacketType.StartGame:
                    if (_state == GameState.Lobby)
                    {
                        var (seed, totalPlayers) = NetworkProtocol.ReadStartGame(data);
                        _networkSeed = seed;
                        _totalPlayersForGame = totalPlayers;
                        
                        _state = GameState.Playing;
                        _isRecording = true; // REQUIRED FOR ROLLBACK HISTORY
                        _recorder.Reset(); // Clear old history
                        _simulation = new Simulation(_networkSeed, _totalPlayersForGame);
                        string logFile = $"debug_log_player_{_localPlayerId}.txt";
                        File.WriteAllText(logFile, "--- Client Start ---\n");
                        _simulation.Log = (msg) => {
                             string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Frame {_currentFrame}] {msg}\n";
                             File.AppendAllText(logFile, line);
                             Console.Write(line);
                        };
                         Console.WriteLine($"Game Started! Seed={_networkSeed}, Players={_totalPlayersForGame}");
                    }
                    break;

                case PacketType.Input:
                     if (_state == GameState.Playing)
                     {
                        var (pid, startFrame, inputs, remotePos, remoteHash) = NetworkProtocol.ReadInputPacket(data);
                        
                        int earliestMisprediction = -1;

                        // Process all inputs in the packet (Oldest first)
                        for (int i = inputs.Length - 1; i >= 0; i--)
                        {
                            int frame = startFrame - i;
                            InputState input = inputs[i];

                            if (frame < 0) continue;

                            // Always store the confirmed input in our buffer
                            if (!_remoteInputBuffer.ContainsKey(frame))
                            {
                                _remoteInputBuffer[frame] = new Dictionary<int, InputState>();
                            }
                            _remoteInputBuffer[frame][pid] = input;

                            // CHECK FOR MISPREDICTION (INPUTS)
                            // If this input is for a past frame that we already simulated...
                            if (frame < _currentFrame)
                            {
                                 // Retrive what we *actually* used for that frame from the recorder
                                 InputState[] usedInputs = _recorder.GetFrame(frame);
                                 
                                 // If we have record of that frame, and the used input differs from the confirmed input...
                                 if (usedInputs != null && usedInputs.Length > pid && !input.Equals(usedInputs[pid]))
                                 {
                                     // MISPREDICTION! Mark for Rollback!
                                     if (earliestMisprediction == -1 || frame < earliestMisprediction)
                                     {
                                         earliestMisprediction = frame;
                                     }
                                     
                                     // FIX THE RECORDER!
                                     usedInputs[pid] = input; 
                                     _recorder.UpdateFrame(frame, usedInputs);
                                 }
                            }
                        }

                        // CHECK FOR DESYNC (STATE HASH & POSITION)
                        if (startFrame < _currentFrame && _snapshotBuffer.ContainsKey(startFrame))
                        {
                             var snap = _snapshotBuffer[startFrame];
                             
                             // 1. POSITION RECONCILIATION
                             // Find player entity index in snapshot lists
                             int pIndex = -1;
                             for(int k=0; k<snap.Players.Count; k++)
                             {
                                 if (snap.Players[k].PlayerId == pid)
                                 {
                                     pIndex = k;
                                     break;
                                 }
                             }
                             
                             if (pIndex != -1)
                             {
                                 Entity pEntity = snap.PlayerEntities[pIndex];
                                 // Find transform index
                                 int tIndex = -1;
                                 for(int k=0; k<snap.TransformEntities.Count; k++)
                                 {
                                     if (snap.TransformEntities[k].Index == pEntity.Index)
                                     {
                                         tIndex = k;
                                         break;
                                     }
                                 }
                                 
                                 if (tIndex != -1)
                                 {
                                     Vector2 localPos = snap.Transforms[tIndex].Position;
                                     if (Vector2.Distance(localPos, remotePos) > 4.0f) 
                                     {
                                         Console.WriteLine($"[Sync] Correction! Frame {startFrame} Player {pid}. Local:{localPos} Remote:{remotePos}");
                                         
                                         // FIX THE SNAPSHOT
                                         var tf = snap.Transforms[tIndex];
                                         tf.Position = remotePos;
                                         snap.Transforms[tIndex] = tf; 
                                         
                                         if (earliestMisprediction == -1 || startFrame < earliestMisprediction)
                                             earliestMisprediction = startFrame;
                                     }
                                 }
                             }

                             // 2. FULL STATE HASH CHECK
                             // We compute the hash of our SNAPSHOT for that frame
                             int localHash = StateHasher.Hash(snap);
                             if (localHash != remoteHash)
                             {
                                 Console.WriteLine($"[Sync] CRITICAL DESYNC! Frame {startFrame} Player {pid}. LocalHash:{localHash} RemoteHash:{remoteHash} -> ROLLBACK");
                                 if (earliestMisprediction == -1 || startFrame < earliestMisprediction)
                                     earliestMisprediction = startFrame;
                             }
                        }

                        // Update latest known input for prediction of future frames
                        // We assume inputs[0] is the latest (startFrame)
                        if (inputs.Length > 0)
                        {
                            _lastConfirmedRemoteInputs[pid] = inputs[0];
                            _lastConfirmedRemoteFrame[pid] = startFrame;
                        }

                        // Trigger Rollback if needed
                        if (earliestMisprediction != -1)
                        {
                            HandleRollback(earliestMisprediction);
                        }

                        // Host Relay Logic (Relay the RAW packet to others)
                        if (_localPlayerId == 0)
                        {
                            if (pid != 0) 
                            {
                                // Loop through all other clients and send Unicast
                                foreach(var client in _networkManager.ConnectedClients)
                                {
                                    if (!client.Equals(sender)) // Don't echo back to sender
                                    {
                                        _networkManager.SendTo(data, client);
                                    }
                                }
                            }
                        }
                     }
                    break;

                case PacketType.DiscoveryRequest:
                    if (_localPlayerId == 0 && (_state == GameState.Lobby || _state == GameState.Playing))
                    {
                        // I am host, reply
                        var resp = NetworkProtocol.CreateDiscoveryResponse("Local Game", _connectedPlayerCount, _totalPlayersForGame);
                        _networkManager?.SendTo(resp, sender);
                    }
                    break;
                
                case PacketType.DiscoveryResponse:
                    if (_state == GameState.ServerBrowser)
                    {
                        var info = NetworkProtocol.ReadDiscoveryResponse(data);
                        _foundServers[sender] = info;
                    }
                    break;
            }
        }

        private void StepSimulation(KeyboardState keyboardState)
        {
            InputState[] inputs;

            if (_isReplaying)
            {
                    inputs = _recorder.GetFrame(_replayFrame);
                    if (inputs == null || inputs.Length == 0) inputs = new InputState[1];
                    if (_simulation != null) _simulation.Update(inputs, (float)FixedTimeStep);
                    _replayFrame++;
                    return;
            }

            // Capture Local Input
            Vector2 movement = Vector2.Zero;
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) movement.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) movement.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) movement.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) movement.X += 1;

            if (movement != Vector2.Zero) movement.Normalize();

            // Check if we already decided input for this frame?
            InputState localInput;
            if (_localInputBuffer.ContainsKey(_currentFrame))
            {
                localInput = _localInputBuffer[_currentFrame];
            }
            else
            {
                // New Frame: Consume Latch
                bool placeBomb = _pendingBombInput;
                _pendingBombInput = false; // Reset Latch after consumption
                
                // Calculate explicit bomb target based on current local position
                // This ensures that what the client SEES (and intends) is what gets simulated everywhere
                Point bombTarget = new Point(0, 0);
                if (placeBomb)
                {
                     // Find local player position (it might be needed before the loop below, but let's grab it now)
                     // Actually, we need position BEFORE creating InputState.
                     // The loop below (lines ~670) finds 'currentPos'. We need to move that up?
                     // Or just iterate quickly here.
                     
                     Vector2 myPos = Vector2.Zero;
                     var pPool = _simulation.World.Players;
                     for(int i=0; i<pPool.Count; i++)
                     {
                         if (pPool.Get(i).PlayerId == _localPlayerId)
                         {
                             var e = pPool.GetEntity(i);
                             if (_simulation.World.Transforms.Has(e))
                                myPos = _simulation.World.Transforms.Get(e).Position;
                             break;
                         }
                     }
                     
                     int centerX = (int)(myPos.X + 12);
                     int centerY = (int)(myPos.Y + 12);
                     bombTarget = new Point(centerX / 32, centerY / 32);
                }

                localInput = new InputState { Movement = movement, PlaceBomb = placeBomb, BombTarget = bombTarget };
                _localInputBuffer[_currentFrame] = localInput;
            }


            if (_isNetworked)
            {
                // ROLLBACK / PREDICTION LOGIC
                
                // FRAME PACING / THROTTLING
                // Prevent running too far ahead of the slowest client
                // This ensures we always have a recent enough state to rollback to if a late packet arrives
                int minConfirmedFrame = _currentFrame;
                
                for (int i = 0; i < _totalPlayersForGame; i++)
                {
                    if (i == _localPlayerId) continue;
                    
                    if (_lastConfirmedRemoteFrame.TryGetValue(i, out int lastFrame))
                    {
                        if (lastFrame < minConfirmedFrame) minConfirmedFrame = lastFrame;
                    }
                    else
                    {
                        // If we haven't heard from a player yet, assume they are at 0
                        // This might stall start until first packet, which is fine
                        minConfirmedFrame = 0; 
                    }
                }

                int predictionLimit = MaxPredictionFrames; 
                // Dynamically adjust limit? No, static 15 frames (250ms) is fine.
                
                if (_currentFrame > minConfirmedFrame + predictionLimit)
                {
                    // Throttle! Wait for inputs to catch up.
                    // Console.WriteLine($"Throttling: Current={_currentFrame}, MinRemote={minConfirmedFrame}");
                    return; 
                }


                // 1. Send Local Input for THIS frame (and history)


                // 1. Send Local Input for THIS frame (and history)
                int redundancy = 8;
                List<InputState> history = new List<InputState>();
                history.Add(localInput); // Current Frame (0)

                for (int i = 1; i < redundancy; i++)
                {
                    int histFrame = _currentFrame - i;
                    if (histFrame >= 0 && _localInputBuffer.ContainsKey(histFrame))
                    {
                        history.Add(_localInputBuffer[histFrame]);
                    }
                    else
                    {
                        break; // End of valid history
                    }
                }

                // Get ID for Local Player Position Lookup
                Vector2 currentPos = Vector2.Zero;
                var playerPool = _simulation.World.Players;
                for (int i = 0; i < playerPool.Count; i++)
                {
                    if (playerPool.Get(i).PlayerId == _localPlayerId)
                    {
                        var entity = playerPool.GetEntity(i);
                        if (_simulation.World.Transforms.Has(entity))
                        {
                            currentPos = _simulation.World.Transforms.Get(entity).Position;
                        }
                        break;
                    }
                }

                int localHash = StateHasher.Hash(_simulation.World);

                byte[] packet = NetworkProtocol.CreateInputPacket(_localPlayerId, _currentFrame, history.ToArray(), currentPos, localHash);
                
                if (_networkManager != null) 
                {
                    if (_localPlayerId == 0) _networkManager.Broadcast(packet);
                    else _networkManager.Send(packet);
                }

                // 2. Construct Input Array for THIS frame using PREDICTION
                inputs = new InputState[_totalPlayersForGame];
                inputs[_localPlayerId] = localInput;

                for (int i = 0; i < _totalPlayersForGame; i++)
                {
                    if (i == _localPlayerId) continue;

                    if (_remoteInputBuffer.ContainsKey(_currentFrame) && _remoteInputBuffer[_currentFrame].ContainsKey(i))
                    {
                        // We actually have it (rare, but possible if high pings/low update rate cause buffering)
                        inputs[i] = _remoteInputBuffer[_currentFrame][i];
                    }
                    else
                    {
                        // PREDICT IT
                        inputs[i] = PredictInputForPlayer(i);
                    }
                }
                
                // 3. Record what we used (for misprediction check later)
                if (_isRecording) _recorder.RecordFrame(inputs);

                // 4. Run Simulation
                if (_simulation != null) 
                {
                    _simulation.Update(inputs, (float)FixedTimeStep);

                    // 5. Save Snapshot
                    _snapshotBuffer[_currentFrame] = new GameStateSnapshot(_currentFrame, _simulation.World);
                    if (_snapshotBuffer.ContainsKey(_currentFrame - MaxSnapshotFrames)) 
                    {
                        _snapshotBuffer.Remove(_currentFrame - MaxSnapshotFrames);
                    }
                }

                _currentFrame++;
            }
            else
            {
                // Local Single Player
                inputs = new InputState[] { localInput };
                if (_isRecording) _recorder.RecordFrame(inputs);
                if (_simulation != null) _simulation.Update(inputs, (float)FixedTimeStep);
            }

        }

        private InputState PredictInputForPlayer(int playerId)
        {
            // Simple prediction: repeat the last confirmed input.
            if (_lastConfirmedRemoteInputs.TryGetValue(playerId, out var lastInput))
            {
                return lastInput;
            }
            return new InputState(); // Default to zero input if we have nothing.
        }

        private void HandleRollback(int mispredictedFrame)
        {
            Console.WriteLine($"ROLLBACK from frame {_currentFrame} to {mispredictedFrame}");

            // 1. Load the state from the frame *before* the misprediction
            if (!_snapshotBuffer.TryGetValue(mispredictedFrame - 1, out GameStateSnapshot? snapshot))
            {
                Console.WriteLine($"!!! CRITICAL: Cannot rollback, no snapshot for frame {mispredictedFrame - 1} (Current: {_currentFrame})");
                // This is a fatal error. In a real game, you might request a full state sync from the host.
                return;
            }
            
            if (_simulation == null) return;

            snapshot.Restore(_simulation.World);

            // 2. Resimulate from the mispredicted frame up to the current frame
            for (int frame = mispredictedFrame; frame < _currentFrame; frame++)
            {
                // 3. Construct the full, correct input array for this frame
                InputState[] inputs = new InputState[_totalPlayersForGame];
                
                // Local input
                if (_localInputBuffer.ContainsKey(frame)) inputs[_localPlayerId] = _localInputBuffer[frame];

                // Remote inputs (use confirmed if available, otherwise predict again)
                for (int i = 0; i < _totalPlayersForGame; i++)
                {
                    if (i == _localPlayerId) continue;

                    if (_remoteInputBuffer.ContainsKey(frame) && _remoteInputBuffer[frame].ContainsKey(i))
                    {
                        inputs[i] = _remoteInputBuffer[frame][i]; // Use actual confirmed input
                    }
                    else
                    {
                        inputs[i] = PredictInputForPlayer(i); // Predict if we still don't have it (cascading rollback/prediction)
                    }
                }

                // 4. Resimulate this single frame (no rendering)
                _simulation.Update(inputs, (float)FixedTimeStep);

                // 5. Save the new, corrected snapshot for this frame
                _snapshotBuffer[frame] = new GameStateSnapshot(frame, _simulation.World);

                // 6. Update the recorder with the corrected inputs
                if (_isRecording) _recorder.UpdateFrame(frame, inputs);
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            try
            {
                GraphicsDevice.Clear(Color.CornflowerBlue); // Background
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        
                switch (_state)
                {
                    case GameState.Menu:
                        DrawMenu();
                        break;
                    case GameState.Lobby:
                        DrawLobby();
                        break;

                    case GameState.ServerBrowser:
                        DrawServerBrowser();
                        break;
                    default:
                        DrawGame();
                        break;
                }

                _spriteBatch.End();
                base.Draw(gameTime);
            }
            catch(Exception e)
            {
                 Console.WriteLine("Draw Crash: " + e.ToString());
                 throw;
            }
        }

        private void DrawMenu()
        {
            int btnWidth = 200;
            int btnHeight = 40;
            int centerX = _graphics.PreferredBackBufferWidth / 2 - btnWidth / 2;
            int startY = 80;
            int spacing = 50;

            // 0: Play
            DrawMenuButton(0, "PLAY", centerX, startY, btnWidth, btnHeight, Color.Green, Color.Lime);
            
            // 1: Host
            DrawMenuButton(1, "HOST", centerX, startY + spacing, btnWidth, btnHeight, Color.Purple, Color.Magenta);

            // 2: Join
            DrawMenuButton(2, "JOIN", centerX, startY + spacing * 2, btnWidth, btnHeight, Color.Goldenrod, Color.Yellow);

            // 3: Replay
            DrawMenuButton(3, "REPLAY", centerX, startY + spacing * 3, btnWidth, btnHeight, Color.Blue, Color.Cyan);
        }

        private void DrawLobby()
        {
             int scale = 3;
             DrawText("LOBBY", new Vector2(50, 50), scale, Color.White);
             
             if (_localPlayerId == 0)
             {
                 int port = _networkManager != null ? _networkManager.LocalPort : 0;
                 DrawText($"HOSTING (Port {port}): {_connectedPlayerCount}/{_totalPlayersForGame} Players", new Vector2(50, 100), 2, Color.Yellow);
                 DrawText("Press 2,3,4 to set Count", new Vector2(50, 140), 1, Color.White);
                 DrawText("Press ENTER to Start", new Vector2(50, 180), 2, Color.Green);
             }
             else
             {
                 if (_localPlayerId == -1) DrawText("Connecting...", new Vector2(50, 100), 2, Color.Yellow);
                 else DrawText($"WAITING FOR HOST... (P{_localPlayerId})", new Vector2(50, 100), 2, Color.Yellow);
             }
        }

        private void DrawServerBrowser()
        {
             DrawText("SERVER BROWSER", new Vector2(50, 50), 3, Color.White);
             DrawText("Scanning...", new Vector2(50, 90), 1, Color.Gray);

             int startY = 130;
             int index = 0;
             if (_foundServers.Count == 0)
             {
                 DrawText("No servers found.", new Vector2(50, startY), 2, Color.Red);
             }
             foreach(var kvp in _foundServers) // Dictionary enumeration order is undefined but stable enough for simple UI usually
             {
                 var ep = kvp.Key;
                 var info = kvp.Value;
                 bool selected = index == _browserSelection;
                 
                 string line = $"{info.name} - {info.current}/{info.max} ({ep.Address})";
                 DrawText(line, new Vector2(50, startY + index * 40), 2, selected ? Color.Yellow : Color.White);
                 
                 if (selected) DrawText(">", new Vector2(20, startY + index * 40), 2, Color.Yellow);
                 
                 index++;
             }
             
             DrawText("Press ENTER to Join", new Vector2(50, 380), 2, Color.Green);
        }

        private void DrawGame()
        {
            if (_simulation == null) return;
            var world = _simulation.World;
            var transformEntities = world.Transforms.GetEntities();
            var transforms = world.Transforms.GetAll();

            // 1. Draw Grid/Floor
            var tiles = world.Tiles.GetAll();
            var tileEntities = world.Tiles.GetEntities();
            for(int i=0; i<tiles.Count; i++)
            {
                var entity = tileEntities[i]; 
                TransformComponent transform = FindTransform(entity, transformEntities, transforms);

                // Draw Floor for EVERYTHING (or just Empty/Destructible)
                DrawRectangle(transform.Position + new Vector2(1,1), transform.Size - new Vector2(2,2), Color.Gray);

                if (tiles[i].Type == TileComponent.TileType.Solid) 
                {
                    DrawRectangle(transform.Position + new Vector2(1,1), transform.Size - new Vector2(2,2), Color.DarkGray);
                }
                else if (tiles[i].Type == TileComponent.TileType.Destructible && !tiles[i].Destroyed) 
                {
                    DrawRectangle(transform.Position + new Vector2(1,1), transform.Size - new Vector2(2,2), Color.SaddleBrown);
                }
            }

            // 2. Draw Bombs
            var bombs = world.Bombs.GetAll();
            var bombEntities = world.Bombs.GetEntities();
            for(int i=0; i<bombs.Count; i++)
            {
                 if (i >= bombEntities.Count) break;
                 var entity = bombEntities[i];
                 TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                 
                 // Pulse red based on timer
                 float pulse = (bombs[i].Timer % 20) / 20f;
                 Color bColor = Color.Lerp(Color.Red, Color.DarkRed, pulse);

                 DrawRectangle(transform.Position + new Vector2(4, 4), transform.Size - new Vector2(8,8), bColor);
            }

            // 3. Draw Powerups
            var powerups = world.Powerups.GetAll();
            var powerupEntities = world.Powerups.GetEntities();
            for(int i=0; i<powerups.Count; i++)
            {
                 if (i >= powerups.Count) break;
                 var entity = powerupEntities[i];
                 TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                 
                 Color pColor = Color.White;
                 if (powerups[i].Type == PowerupComponent.PowerupType.Range) pColor = Color.Yellow;
                 if (powerups[i].Type == PowerupComponent.PowerupType.Capacity) pColor = Color.Black;
                 
                 DrawRectangle(transform.Position, transform.Size, pColor);
            }

            // 4. Draw Explosions
            var expList = world.Explosions.GetAll();
            var expEntities = world.Explosions.GetEntities();
            for(int i=0; i<expList.Count; i++)
            {
                if (i >= expEntities.Count) break;
                var entity = expEntities[i];
                TransformComponent transform = FindTransform(entity, transformEntities, transforms);
                DrawRectangle(transform.Position, transform.Size, Color.OrangeRed);
            }

            // 5. Draw Players
            var players = world.Players.GetAll();
            var playerEntities = world.Players.GetEntities();
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Alive) continue; 
                var entity = playerEntities[i];
                TransformComponent transform = FindTransform(entity, transformEntities, transforms); 

                Color[] playerColors = new Color[] { Color.White, Color.Blue, Color.Red, Color.Green };
                Color pColor = playerColors[i % playerColors.Length];
                DrawRectangle(transform.Position, transform.Size, pColor);
                
                // Eyes
                Vector2 eyeOffset = new Vector2(4, 6);
                DrawRectangle(transform.Position + eyeOffset, new Vector2(4, 6), Color.Black);
                DrawRectangle(transform.Position + new Vector2(transform.Size.X - eyeOffset.X - 4, eyeOffset.Y), new Vector2(4, 6), Color.Black);
            }
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

        private void DrawMenuButton(int index, string text, int x, int y, int width, int height, Color normalColor, Color selectedColor)
        {
            Color color = _menuSelection == index ? selectedColor : normalColor;
            DrawRectangle(new Vector2(x, y), new Vector2(width, height), color);
            
            // Text
            int scale = 3;
            int textWidth = text.Length * (5 * scale + scale);
            DrawText(text, new Vector2(x + width/2 - textWidth/2, y + 10), scale, Color.White);

            // Selection Border
            if (_menuSelection == index) DrawHollowRect(new Rectangle(x-2, y-2, width+4, height+4), Color.White);
        }

        private void DrawText(string text, Vector2 position, int scale, Color color)
        {
            int spacing = 1 * scale;
            int charWidth = 5 * scale;
            
            for(int i=0; i<text.Length; i++)
            {
                bool[,] bitmap = PixelFont.GetBitmap(text[i]);
                Vector2 charPos = position + new Vector2(i * (charWidth + spacing), 0);
                
                for(int x=0; x<5; x++)
                {
                    for(int y=0; y<5; y++)
                    {
                        if (bitmap[x,y])
                        {
                            DrawRectangle(charPos + new Vector2(x * scale, y * scale), new Vector2(scale, scale), color);
                        }
                    }
                }
            }
        }

        private void DrawHollowRect(Rectangle rect, Color color)
        {
            int t = 2; 
            DrawRectangle(new Vector2(rect.X, rect.Y), new Vector2(rect.Width, t), color); 
            DrawRectangle(new Vector2(rect.X, rect.Bottom - t), new Vector2(rect.Width, t), color); 
            DrawRectangle(new Vector2(rect.X, rect.Y), new Vector2(t, rect.Height), color); 
            DrawRectangle(new Vector2(rect.Right - t, rect.Y), new Vector2(t, rect.Height), color); 
        }

        private void DrawRectangle(Vector2 position, Vector2 size, Color color)
        {
             _spriteBatch.Draw(_pixelTexture, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);
        }
    }

    public static class Program
    {
        [STAThread]
        static void Main()
        {
            try 
            {
                using (var game = new Game1())
                    game.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("CRASH: " + e.ToString());
                throw;
            }
        }
    }
}
