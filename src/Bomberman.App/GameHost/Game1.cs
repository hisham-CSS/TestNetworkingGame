using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.Core;
using Bomberman.Net;
using Bomberman.Net.Lockstep;
using Bomberman.Net.Protocol;

namespace Bomberman.App
{
    /// <summary>
    /// The View layer. It now renders real screens (menu, lobby, LAN server browser) using PixelFont,
    /// not just colored rectangles. Single-player runs the Week 2 threaded loop; networked play drives
    /// a LockstepSession on the main thread (a lockstep peer must gate each frame on the other player).
    /// </summary>
    public class Game1 : Game
    {
        private enum Mode { Menu, SinglePlayer, HostLobby, Browser, ClientLobby, NetPlaying }

        private const int HostPort = 5000;
        private const int DiscoveryStart = 5000, DiscoveryEnd = 5010;
        private const int TotalPlayers = 2;
        private const int W = 480, H = 416;

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private Texture2D _pixel = null!;
        private KeyboardState _prev;

        private Mode _mode = Mode.Menu;

        // Single-player (Week 2 path)
        private GameSession? _session;
        private SimulationLoop? _loop;

        // Menu
        private readonly string[] _menu = { "HOST GAME", "FIND LAN GAME", "SINGLE PLAYER", "QUIT" };
        private int _menuIndex;

        // Networking / lobby
        private NetworkController<InputState>? _net;
        private bool _isHost;
        private int _seed = 12345;
        private int _localPlayerId;
        private bool _peerConnected;
        private string _peerLabel = "WAITING...";
        private IPEndPoint? _peerEp;
        private string _serverName = "HOST GAME";
        private readonly bool[] _ready = new bool[TotalPlayers];

        // Server browser
        private class Server { public IPEndPoint Ep = null!; public string Name = ""; public int Players, Max; public DateTime Seen; }
        private readonly List<Server> _servers = new();
        private int _serverIndex;
        private DateTime _lastDiscovery = DateTime.MinValue;

        // Net play
        private GameSession? _netSession;
        private LockstepSession? _lockstep;
        private string _hud = "";
        private string _desyncMsg = "";
        private double _desyncMsgUntil;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            // Keep updating (and sending heartbeats) when the window loses focus. Otherwise tabbing to
            // the other window to ready up would stall this peer and the lobby would time out.
            InactiveSleepTime = TimeSpan.Zero;
            _graphics.PreferredBackBufferWidth = W;
            _graphics.PreferredBackBufferHeight = H;
        }

        protected override void Initialize()
        {
            DeterminismHarness.Verify(_seed, out string report);
            Console.WriteLine(report);
            _mode = Mode.Menu;
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        // ---------------- transitions ----------------

        private void StartSinglePlayer()
        {
            _session = new GameSession(_seed);
            _loop = new SimulationLoop(_session);
            _loop.Start();
            _mode = Mode.SinglePlayer;
        }

        private void StartHost()
        {
            _isHost = true;
            _localPlayerId = 0;
            _seed = new Random().Next();
            _peerConnected = false; _peerLabel = "WAITING..."; _peerEp = null;
            _ready[0] = _ready[1] = false;
            _serverName = MakeServerName();
            _net = new NetworkController<InputState>(new UdpTransport(HostPort));

            _net.OnDiscoveryRequestReceived += (sender, _, __, ___) =>
                _net!.SendDiscoveryResponse(sender, _serverName, _peerConnected ? 2 : 1, TotalPlayers);

            _net.OnJoinRequestRaw += sender =>
            {
                _net!.AddClient(sender);
                _peerConnected = true;
                _peerEp = sender;
                _peerLabel = sender.Address.ToString();
                _net.SendWelcome(sender, assignedId: 1, seed: _seed, totalPlayers: TotalPlayers);
                _net.BroadcastLobbyUpdate(2, TotalPlayers, 0b11);
            };
            _net.OnLobbyReadyReceived += (pid, ready) => { if (pid >= 0 && pid < TotalPlayers) _ready[pid] = ready; };
            // Only the actual peer disconnecting tears down the lobby (ignore stale/unknown endpoints).
            _net.OnDisconnected += (ep, __) =>
            {
                if (_peerEp != null && ep.Equals(_peerEp))
                { _peerConnected = false; _peerLabel = "WAITING..."; _ready[1] = false; _peerEp = null; }
            };

            _mode = Mode.HostLobby;
        }

        private static string MakeServerName()
        {
            var sb = new System.Text.StringBuilder("HOST ");
            foreach (char c in Environment.MachineName.ToUpperInvariant())
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.Length > 5 ? sb.ToString() : "HOST GAME";
        }

        private void StartBrowser()
        {
            _isHost = false;
            _servers.Clear(); _serverIndex = 0;
            _net = new NetworkController<InputState>(new UdpTransport(0));
            _net.OnDiscoveryResponseReceived += (sender, name, cur, max) =>
            {
                // Dedupe by host identity (name): one host can answer from BOTH its loopback and LAN
                // address on a single machine. Prefer a loopback endpoint so local joins are reliable.
                var s = _servers.Find(v => v.Name == name);
                if (s == null) { s = new Server { Name = name, Ep = new IPEndPoint(sender.Address, HostPort) }; _servers.Add(s); }
                if (IPAddress.IsLoopback(sender.Address) || !IPAddress.IsLoopback(s.Ep.Address))
                    s.Ep = new IPEndPoint(sender.Address, HostPort);
                s.Players = cur; s.Max = max; s.Seen = DateTime.Now;
            };
            _mode = Mode.Browser;
            BroadcastDiscovery();
        }

        private void BroadcastDiscovery()
        {
            // LAN broadcast for real networks...
            _net?.BroadcastDiscoveryRequest(DiscoveryStart, DiscoveryEnd);
            // ...plus a direct loopback probe so two instances on ONE machine find each other
            // (UDP broadcast does not reliably loop back to 127.0.0.1).
            if (_net != null)
                for (int p = DiscoveryStart; p < DiscoveryEnd; p++)
                    _net.RelayPacket(new IPEndPoint(IPAddress.Loopback, p), NetworkProtocol<InputState>.CreateDiscoveryRequest());
            _lastDiscovery = DateTime.Now;
        }

        private void JoinServer(IPEndPoint host)
        {
            _isHost = false;
            _localPlayerId = 1;
            _peerConnected = false; _peerLabel = host.Address.ToString();
            _ready[0] = _ready[1] = false;

            _net!.OnWelcomeReceived += (assignedId, seed, total) =>
            {
                _localPlayerId = assignedId;
                _seed = seed;
                _peerConnected = true;
            };
            _net.OnLobbyReadyReceived += (pid, ready) => { if (pid >= 0 && pid < TotalPlayers) _ready[pid] = ready; };
            _net.OnStartGameReceived += (seed, total) => { _seed = seed; BeginMatch(); };
            _net.OnDisconnected += (_, __) => { _peerConnected = false; };

            _net.Connect(host.Address.ToString(), host.Port);
            _net.SendJoinRequest();
            _mode = Mode.ClientLobby;
        }

        private void BeginMatch()
        {
            _netSession = new GameSession(_seed, TotalPlayers);
            // Lockstep needs a cushion: the peer's input for a frame must be in hand BEFORE we reach it,
            // or we stall. A 1-frame delay leaves no slack for the per-frame send/recv cadence, so on a
            // fast link we stall constantly and it feels far laggier than the ping. Floor the delay at a
            // few frames (still tiny, ~50ms) so play stays smooth; the ping-based value scales up from there.
            const int MinNetDelayFrames = 3;
            int delay = LockstepSession.CalculateInputDelay(_net!.LastPingMs, minDelay: MinNetDelayFrames);
            _lockstep = new LockstepSession(_netSession, _net, _localPlayerId, delay);

            // Week 4: host's authoritative snapshot arrives here; restore it.
            _net.OnStateSyncReceived += bytes => _lockstep!.ApplyResync(bytes);
            _lockstep.OnDesyncDetected += r => Flash($"DESYNC F{r.Frame}");
            _lockstep.OnResynced += fr => Flash($"RESYNCED -> F{fr}");

            _mode = Mode.NetPlaying;
        }

        private void Flash(string msg) { _desyncMsg = msg; _desyncMsgUntil = _clock + 2.5; }
        private double _clock;

        private void LeaveToMenu()
        {
            try { _net?.Close(); } catch { }
            _net = null; _netSession = null; _lockstep = null;
            _loop?.Stop(); _loop = null; _session = null;
            _peerConnected = false; _servers.Clear();
            _mode = Mode.Menu;
        }

        // ---------------- update ----------------

        protected override void Update(GameTime gameTime)
        {
            var k = Keyboard.GetState();
            _clock += gameTime.ElapsedGameTime.TotalSeconds;
            _net?.Update();

            switch (_mode)
            {
                case Mode.Menu: UpdateMenu(k); break;
                case Mode.SinglePlayer:
                    if (Pressed(k, Keys.Escape)) LeaveToMenu();
                    else _loop!.SubmitInput(ReadInput(k));
                    break;
                case Mode.HostLobby: UpdateHostLobby(k); break;
                case Mode.Browser: UpdateBrowser(k); break;
                case Mode.ClientLobby: UpdateClientLobby(k); break;
                case Mode.NetPlaying: UpdateNetPlaying(k); break;
            }

            _prev = k;
            base.Update(gameTime);
        }

        private void UpdateMenu(KeyboardState k)
        {
            if (Pressed(k, Keys.Down)) _menuIndex = (_menuIndex + 1) % _menu.Length;
            if (Pressed(k, Keys.Up)) _menuIndex = (_menuIndex + _menu.Length - 1) % _menu.Length;
            if (Pressed(k, Keys.H)) { _menuIndex = 0; }
            if (Pressed(k, Keys.F)) { _menuIndex = 1; }
            if (Pressed(k, Keys.Enter) || Pressed(k, Keys.Space))
            {
                switch (_menuIndex)
                {
                    case 0: StartHost(); break;
                    case 1: StartBrowser(); break;
                    case 2: StartSinglePlayer(); break;
                    case 3: Exit(); break;
                }
            }
        }

        private void UpdateHostLobby(KeyboardState k)
        {
            if (Pressed(k, Keys.Escape)) { LeaveToMenu(); return; }
            if (Pressed(k, Keys.R))
            {
                _ready[0] = !_ready[0];
                _net!.SendLobbyReady(0, _ready[0]); // host broadcasts its ready
            }
            if (_peerConnected && _ready[0] && _ready[1])
            {
                _net!.BroadcastStartGame(_seed, TotalPlayers);
                BeginMatch();
            }
        }

        private void UpdateBrowser(KeyboardState k)
        {
            if (Pressed(k, Keys.Escape)) { LeaveToMenu(); return; }
            if (DateTime.Now - _lastDiscovery > TimeSpan.FromSeconds(2)) BroadcastDiscovery();
            _servers.RemoveAll(s => DateTime.Now - s.Seen > TimeSpan.FromSeconds(5));
            if (_servers.Count > 0)
            {
                if (Pressed(k, Keys.Down)) _serverIndex = (_serverIndex + 1) % _servers.Count;
                if (Pressed(k, Keys.Up)) _serverIndex = (_serverIndex + _servers.Count - 1) % _servers.Count;
                if (_serverIndex >= _servers.Count) _serverIndex = 0;
                if (Pressed(k, Keys.Enter)) JoinServer(_servers[_serverIndex].Ep);
            }
        }

        private void UpdateClientLobby(KeyboardState k)
        {
            if (Pressed(k, Keys.Escape)) { LeaveToMenu(); return; }
            if (!_peerConnected) _net!.SendJoinRequest(); // keep retrying until welcomed
            if (Pressed(k, Keys.R))
            {
                _ready[_localPlayerId] = !_ready[_localPlayerId];
                _net!.SendLobbyReady(_localPlayerId, _ready[_localPlayerId]); // client -> host
            }
            // Host decides start and sends StartGame (handled by OnStartGameReceived).
        }

        private void UpdateNetPlaying(KeyboardState k)
        {
            if (Pressed(k, Keys.Escape)) { LeaveToMenu(); return; }
            if (Pressed(k, Keys.K)) _lockstep!.ForceDesync();   // demo: corrupt our state -> desync
            _lockstep!.SubmitLocalInput(ReadInput(k));
            // Advance at most ONE simulation frame per fixed 60Hz Update. MonoGame already calls
            // Update at a fixed cadence, so one step per tick pins the sim to wall-clock. Draining
            // every buffered frame here (the old while-loop) fast-forwards through the input-delay
            // cushion and runs the game at 2x+; if the peer's input is not in yet we just stall this
            // tick and retry next tick.
            _lockstep.TryAdvance();
            _hud = _lockstep.IsStalledWaitingForRemote
                ? $"FRAME {_lockstep.CurrentFrame}  STALLING"
                : $"FRAME {_lockstep.CurrentFrame}  PING {_net!.LastPingMs}MS";
        }

        private InputState ReadInput(KeyboardState k)
        {
            Vector2 m = Vector2.Zero;
            if (k.IsKeyDown(Keys.W) || k.IsKeyDown(Keys.Up)) m.Y -= 1;
            if (k.IsKeyDown(Keys.S) || k.IsKeyDown(Keys.Down)) m.Y += 1;
            if (k.IsKeyDown(Keys.A) || k.IsKeyDown(Keys.Left)) m.X -= 1;
            if (k.IsKeyDown(Keys.D) || k.IsKeyDown(Keys.Right)) m.X += 1;
            if (m != Vector2.Zero) m.Normalize();
            bool bomb = k.IsKeyDown(Keys.Space) && !_prev.IsKeyDown(Keys.Space);
            return new InputState { Movement = m, PlaceBomb = bomb };
        }

        private bool Pressed(KeyboardState k, Keys key) => k.IsKeyDown(key) && !_prev.IsKeyDown(key);

        // ---------------- draw ----------------

        private static readonly Color BG = new(14, 23, 38);
        private static readonly Color CYAN = new(34, 211, 238);
        private static readonly Color AMBER = new(251, 191, 36);
        private static readonly Color WHITE = new(230, 237, 247);
        private static readonly Color MUT = new(120, 140, 170);
        private static readonly Color GREEN = new(52, 211, 153);
        private static readonly Color REDC = new(248, 113, 113);

        private void T(string s, float x, float y, float px, Color c) => PixelFont.Draw(_spriteBatch, _pixel, s, x, y, px, c);
        private void TC(string s, float y, float px, Color c) => PixelFont.DrawCentered(_spriteBatch, _pixel, s, W / 2f, y, px, c);

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(BG);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            switch (_mode)
            {
                case Mode.Menu: DrawMenu(); break;
                case Mode.HostLobby:
                case Mode.ClientLobby: DrawLobby(_isHost); break;
                case Mode.Browser: DrawBrowser(); break;
                case Mode.SinglePlayer: DrawGame(_session); break;
                case Mode.NetPlaying: DrawGame(_netSession); DrawHud(); break;
            }
            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private void DrawMenu()
        {
            TC("BOMBERMAN", 70, 6, CYAN);
            TC("LOCKSTEP NETWORKING", 130, 2, MUT);
            for (int i = 0; i < _menu.Length; i++)
            {
                bool sel = i == _menuIndex;
                float y = 200 + i * 34;
                float left = W / 2f - PixelFont.MeasureWidth(_menu[i], 3) / 2f;
                if (sel) T(">", left - 26, y, 3, AMBER);   // sits clear to the LEFT of the item
                TC(_menu[i], y, 3, sel ? AMBER : WHITE);
            }
            TC("UP / DOWN  +  ENTER", 380, 2, MUT);
        }

        private void DrawLobby(bool host)
        {
            TC(host ? "LOBBY - HOSTING" : "LOBBY - JOINING", 40, 4, CYAN);
            TC(host ? "PORT 5000" : "HOST " + _peerLabel, 86, 2, MUT);

            string[] who = host
                ? new[] { "YOU (HOST)", _peerConnected ? _peerLabel : "WAITING..." }
                : new[] { _peerLabel, _peerConnected ? "YOU" : "CONNECTING..." };

            for (int i = 0; i < TotalPlayers; i++)
            {
                float y = 150 + i * 50;
                bool me = i == _localPlayerId;
                bool present = i == 0 ? (host || _peerConnected) : (host ? _peerConnected : true);
                T("P" + i, 60, y, 3, me ? AMBER : WHITE);
                T(who[i], 120, y, 2, present ? WHITE : MUT);
                if (present)
                    T(_ready[i] ? "READY" : "NOT READY", 120, y + 22, 2, _ready[i] ? GREEN : REDC);
            }

            TC("PRESS R TO " + (_ready[_localPlayerId] ? "UNREADY" : "READY"), 300, 2, AMBER);
            if (host && _peerConnected && _ready[0] && _ready[1]) TC("STARTING...", 330, 2, GREEN);
            else if (host && !_peerConnected) TC("WAITING FOR A PLAYER TO JOIN", 330, 2, MUT);
            TC("ESC TO CANCEL", 380, 2, MUT);
        }

        private void DrawBrowser()
        {
            TC("FIND LAN GAME", 40, 4, CYAN);
            if (_servers.Count == 0)
            {
                TC("SCANNING...", 180, 3, MUT);
            }
            else
            {
                for (int i = 0; i < _servers.Count; i++)
                {
                    var s = _servers[i];
                    bool sel = i == _serverIndex;
                    float y = 130 + i * 40;
                    if (sel) T(">", 40, y, 3, AMBER);
                    T(s.Name, 70, y, 2, sel ? AMBER : WHITE);
                    T(s.Players + "/" + s.Max, 360, y, 2, MUT);
                }
                TC("ENTER TO JOIN", 350, 2, AMBER);
            }
            TC("ESC TO CANCEL", 380, 2, MUT);
        }

        private void DrawHud()
        {
            T(_hud, 8, 8, 2, _lockstep!.IsStalledWaitingForRemote ? REDC : GREEN);
            T("P" + _localPlayerId, W - 40, 8, 2, AMBER);
            T("K: FORCE DESYNC", 8, H - 20, 2, MUT);
            if (_lockstep.ResyncCount > 0) T("RESYNCS " + _lockstep.ResyncCount, W - 120, H - 20, 2, MUT);
            if (_desyncMsg != "" && _clock < _desyncMsgUntil)
                T(_desyncMsg, 8, 28, 2, REDC);
        }

        private void DrawGame(GameSession? src)
        {
            var snap = src?.CaptureRenderSnapshot();
            if (snap == null) return;
            foreach (var t in snap.Tiles)
            {
                if (t.Variant == (int)TileComponent.TileType.Solid) Rect(t.Position, t.Size, Color.DarkGray);
                else if (t.Variant == (int)TileComponent.TileType.Destructible && !t.Flag) Rect(t.Position, t.Size, Color.Brown);
            }
            foreach (var b in snap.Bombs)
            {
                Rect(b.Position + new Vector2(4, 4), b.Size - new Vector2(8, 8), Color.Black);
                Rect(b.Position + new Vector2(12, 12), new Vector2(8, 8), Color.Yellow);
            }
            foreach (var p in snap.Powerups)
            {
                Color c = p.Variant == (int)PowerupComponent.PowerupType.Range ? Color.Yellow
                        : p.Variant == (int)PowerupComponent.PowerupType.Capacity ? Color.Black : Color.White;
                Rect(p.Position, p.Size, c);
            }
            foreach (var e in snap.Explosions) Rect(e.Position, e.Size, Color.OrangeRed);
            foreach (var pl in snap.Players)
            {
                if (!pl.Flag) continue;                         // Flag = alive
                Color c = pl.Variant == 0 ? Color.DodgerBlue    // Variant = PlayerId
                        : pl.Variant == 1 ? Color.Crimson
                        : pl.Variant == 2 ? Color.MediumSeaGreen : Color.Gold;
                Rect(pl.Position, pl.Size, c);
            }
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
