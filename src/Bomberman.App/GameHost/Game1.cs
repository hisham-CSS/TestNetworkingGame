using System;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.Core;
using Bomberman.Net;
using Bomberman.Net.Lockstep;

namespace Bomberman.App
{
    /// <summary>
    /// The View layer. In single-player it owns a threaded SimulationLoop (Week 2). In networked
    /// play (Week 3) it instead drives a LockstepSession on the main thread, because a lockstep peer
    /// must gate each frame on the other player's input and cannot free-run.
    ///
    /// Controls: [H] host on 5000, [J] join 127.0.0.1:5000, [F] find LAN host, [R] toggle ready.
    /// </summary>
    public class Game1 : Game
    {
        private enum AppMode { SinglePlayer, Lobby, NetPlaying }

        private const int HostPort = 5000;
        private const int DiscoveryStart = 5000;
        private const int DiscoveryEnd = 5010;
        private const int TotalPlayers = 2;

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private Texture2D _pixel = null!;
        private KeyboardState _previousKeyboardState;

        // Single-player (Week 2 path)
        private GameSession _session = null!;
        private SimulationLoop _loop = null!;

        // Networked (Week 3 path)
        private AppMode _mode = AppMode.SinglePlayer;
        private bool _isHost;
        private int _seed = 12345;
        private NetworkController<InputState>? _net;
        private GameSession? _netSession;
        private LockstepSession? _lockstep;
        private bool _localReady, _remoteReady, _peerConnected;
        private string _status = "Single-player. [H]ost  [J]oin  [F]ind";

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            _graphics.PreferredBackBufferWidth = 480;
            _graphics.PreferredBackBufferHeight = 416;
        }

        protected override void Initialize()
        {
            _session = new GameSession(_seed);
            DeterminismHarness.Verify(_seed, out string report);
            Console.WriteLine(report);
            _loop = new SimulationLoop(_session);
            _loop.Start();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        // ---------------- Networking setup ----------------

        private void StartHost()
        {
            _loop.Stop();
            _isHost = true;
            _mode = AppMode.Lobby;
            _net = new NetworkController<InputState>(new UdpTransport(HostPort));
            _status = "Hosting. Waiting for a player to join...";

            // A client found us via LAN discovery: answer with our details.
            _net.OnDiscoveryRequestReceived += (sender, _, __, ___) =>
                _net!.SendDiscoveryResponse(sender, "Bomberman Host", _peerConnected ? 2 : 1, TotalPlayers);

            // A client wants in: register it, assign player id 1, and welcome it with the shared seed.
            _net.OnJoinRequestRaw += sender =>
            {
                _net!.AddClient(sender);
                _peerConnected = true;
                _net.SendWelcome(sender, assignedId: 1, seed: _seed, totalPlayers: TotalPlayers);
                _status = "Player joined. [R] to ready up.";
            };

            _net.OnLobbyReadyReceived += (pid, ready) => { if (pid == 1) _remoteReady = ready; };
            _net.OnInputReceived += (_, __, ___, ____, _____, ______) => { };
        }

        private void StartJoin(string hostIp)
        {
            _loop.Stop();
            _isHost = false;
            _mode = AppMode.Lobby;
            _net = new NetworkController<InputState>(new UdpTransport(0)); // any free port
            _status = $"Joining {hostIp}...";

            _net.OnWelcomeReceived += (assignedId, seed, total) =>
            {
                _seed = seed;            // adopt the host's seed so both sims match from frame 0
                _peerConnected = true;
                _status = "Connected. [R] to ready up.";
            };
            _net.OnLobbyReadyReceived += (pid, ready) => { if (pid == 0) _remoteReady = ready; };
            _net.OnStartGameReceived += (seed, total) => { _seed = seed; BeginMatch(); };

            _net.Connect(hostIp, HostPort);
            _net.SendJoinRequest();
        }

        private void FindLanHost()
        {
            if (_net == null) { StartJoin("127.0.0.1"); }
            _status = "Searching LAN for a host...";
            _net!.OnDiscoveryResponseReceived += (sender, name, cur, max) =>
            {
                _status = $"Found {name} at {sender.Address}";
                _net!.Connect(sender.Address.ToString(), HostPort);
                _net!.SendJoinRequest();
            };
            _net!.BroadcastDiscoveryRequest(DiscoveryStart, DiscoveryEnd);
        }

        private void BeginMatch()
        {
            _netSession = new GameSession(_seed);
            int localId = _isHost ? 0 : 1;
            int delay = LockstepSession.CalculateInputDelay(_net!.LastPingMs); // pick delay from measured RTT
            _lockstep = new LockstepSession(_netSession, _net, localId, delay);
            _mode = AppMode.NetPlaying;
            _status = $"Playing (P{localId}, delay {delay}f)";
        }

        // ---------------- Update ----------------

        protected override void Update(GameTime gameTime)
        {
            var k = Keyboard.GetState();

            if (_mode == AppMode.SinglePlayer)
            {
                if (Pressed(k, Keys.H)) StartHost();
                else if (Pressed(k, Keys.J)) StartJoin("127.0.0.1");
                else if (Pressed(k, Keys.F)) FindLanHost();
                else _loop.SubmitInput(ReadInput(k));
            }
            else
            {
                _net!.Update();

                if (_mode == AppMode.Lobby)
                {
                    if (Pressed(k, Keys.R))
                    {
                        _localReady = !_localReady;
                        _net.SendLobbyReady(_isHost ? 0 : 1, _localReady);
                    }
                    // Host starts the match once both peers are connected and ready.
                    if (_isHost && _peerConnected && _localReady && _remoteReady)
                    {
                        _net.BroadcastStartGame(_seed, TotalPlayers);
                        BeginMatch();
                    }
                }
                else if (_mode == AppMode.NetPlaying)
                {
                    // Capture local input, schedule + send it, then advance as many frames as we have
                    // both players' inputs for. Missing remote input => stall (we simply stop stepping).
                    _lockstep!.SubmitLocalInput(ReadInput(k));
                    while (_lockstep.TryAdvance() == LockstepStep.Stepped) { }
                    _status = _lockstep.IsStalledWaitingForRemote
                        ? $"Frame {_lockstep.CurrentFrame} - stalling (waiting for peer)"
                        : $"Frame {_lockstep.CurrentFrame} - ping {_net.LastPingMs}ms";
                }
            }

            _previousKeyboardState = k;
            base.Update(gameTime);
        }

        private InputState ReadInput(KeyboardState k)
        {
            Vector2 m = Vector2.Zero;
            if (k.IsKeyDown(Keys.W) || k.IsKeyDown(Keys.Up)) m.Y -= 1;
            if (k.IsKeyDown(Keys.S) || k.IsKeyDown(Keys.Down)) m.Y += 1;
            if (k.IsKeyDown(Keys.A) || k.IsKeyDown(Keys.Left)) m.X -= 1;
            if (k.IsKeyDown(Keys.D) || k.IsKeyDown(Keys.Right)) m.X += 1;
            if (m != Vector2.Zero) m.Normalize();
            bool bomb = k.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space);
            return new InputState { Movement = m, PlaceBomb = bomb };
        }

        private bool Pressed(KeyboardState k, Keys key) => k.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);

        // ---------------- Draw ----------------

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            RenderSnapshot? snap = _mode == AppMode.NetPlaying
                ? _netSession?.CaptureRenderSnapshot()
                : _loop.LatestSnapshot;
            if (snap == null) { base.Draw(gameTime); return; }

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            foreach (var t in snap.Tiles)
            {
                if (t.Variant == (int)TileComponent.TileType.Solid)
                    Rect(t.Position, t.Size, Color.DarkGray);
                else if (t.Variant == (int)TileComponent.TileType.Destructible && !t.Flag)
                    Rect(t.Position, t.Size, Color.Brown);
            }
            foreach (var b in snap.Bombs)
            {
                Rect(b.Position + new Vector2(4, 4), b.Size - new Vector2(8, 8), Color.Black);
                Rect(b.Position + new Vector2(12, 12), new Vector2(8, 8), Color.Yellow);
            }
            foreach (var p in snap.Powerups)
            {
                Color c = p.Variant == (int)PowerupComponent.PowerupType.Range ? Color.Yellow
                        : p.Variant == (int)PowerupComponent.PowerupType.Capacity ? Color.Black
                        : Color.White;
                Rect(p.Position, p.Size, c);
            }
            foreach (var e in snap.Explosions) Rect(e.Position, e.Size, Color.OrangeRed);
            foreach (var pl in snap.Players) if (pl.Flag) Rect(pl.Position, pl.Size, Color.Blue);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private void Rect(Vector2 position, Vector2 size, Color color)
            => _spriteBatch.Draw(_pixel, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);

        protected override void Dispose(bool disposing)
        {
            _loop?.Stop();
            try { _net?.Close(); } catch { }
            base.Dispose(disposing);
        }
    }
}
