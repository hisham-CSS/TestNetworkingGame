using System;
using System.IO;
using Microsoft.Xna.Framework;
using Bomberman.Core.Game;
using Bomberman.App.Rendering;

namespace Bomberman.App.States
{
    /// <summary>
    /// End-of-match / end-of-replay overlay. Draws the final frame of the game dimmed, with a panel on
    /// top showing the result (winner, draw, disconnection, or replay finished) and a small menu:
    /// REWATCH (replays) or SAVE REPLAY (live games), and RETURN TO MENU.
    /// </summary>
    public class GameOverState : IGameState
    {
        private readonly GameContext _context;
        private readonly GameStateManager _manager;
        private readonly GameSession _session;
        private readonly WorldRenderer _worldRenderer;

        private readonly bool _isReplayView;
        private readonly bool _isGameCompleted;
        private readonly int _winnerId;
        private readonly string? _endReason;

        private readonly string[] _options;
        private int _selected;

        public GameOverState(GameContext context, GameStateManager manager, GameSession session, int winnerId,
                             bool isReplayView = false, bool isGameCompleted = true, string? endReason = null)
        {
            _context = context;
            _manager = manager;
            _session = session;
            _winnerId = winnerId;
            _isReplayView = isReplayView;
            _isGameCompleted = isGameCompleted;
            _endReason = endReason;
            _worldRenderer = new WorldRenderer(_context.Renderer);

            // Replays can be rewatched; live games can be saved.
            _options = _isReplayView
                ? new[] { "REWATCH", "RETURN TO MENU" }
                : new[] { "SAVE REPLAY", "RETURN TO MENU" };
        }

        public void Enter()
        {
            _context.Logger.Info($"[GameOver] winner={_winnerId} replay={_isReplayView} reason={_endReason ?? "-"}");
            _selected = 0;
        }

        public void Exit() { }

        public void Update(GameTime gameTime)
        {
            if (_context.Network != null) _context.Network.Update();

            if (_context.Input.IsMenuDown()) _selected = (_selected + 1) % _options.Length;
            if (_context.Input.IsMenuUp())   _selected = (_selected + _options.Length - 1) % _options.Length;
            if (_context.Input.IsMenuCancel()) { ReturnToMenu(); return; }
            if (_context.Input.IsMenuSelect()) Execute();
        }

        private void Execute()
        {
            switch (_options[_selected])
            {
                case "REWATCH":
                    if (!string.IsNullOrEmpty(_session.ReplayPath))
                        _manager.ChangeState(_context.StateFactory.CreateReplay(new GameSession(_session.ReplayPath!)));
                    else
                        ReturnToMenu();
                    break;

                case "SAVE REPLAY":
                    SaveReplay();
                    ReturnToMenu();
                    break;

                default: // RETURN TO MENU
                    ReturnToMenu();
                    break;
            }
        }

        private void SaveReplay()
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Replays");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string name = $"Replay_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                _session.SaveReplay(Path.Combine(dir, name));
                _context.Logger.Info($"Replay saved: {name}");
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Failed to save replay: {ex.Message}", ex);
            }
        }

        private void ReturnToMenu()
        {
            if (_context.Network != null)
            {
                _context.Network.Close();   // disconnect cleanly
                _context.Network = null;
            }
            _manager.ChangeState(_context.StateFactory.CreateMenu());
        }

        public void Draw(GameTime gameTime)
        {
            int w = _context.Game.WindowWidth;
            int h = _context.Game.WindowHeight;

            _context.Renderer.ClearScreen(Theme.Bg);
            _context.Renderer.BeginDraw();

            // 1) the final frame of the match, frozen underneath
            if (_session.Simulation != null)
                _worldRenderer.DrawWorld(_session.Simulation.World);

            // 2) dim it so the panel reads clearly
            _context.Renderer.DrawTexture(new Rectangle(0, 0, w, h), new Color(8, 12, 22, 210));

            int cx = w / 2;
            int maxTextWidth = w - 24;   // keep a small margin on both edges

            // 3) headline + status (auto-fit so long strings like "PLAYER 2 DISCONNECTED" stay on screen)
            Color headColor = _endReason != null ? Theme.Bad
                            : (_winnerId == -1 ? Theme.Muted : Theme.Accent);
            string head = Headline();
            _context.Renderer.DrawTextCentered(head, cx, 70, headColor, FitScale(head, 4, maxTextWidth));

            string info = SubInfo();
            if (info != "") _context.Renderer.DrawTextCentered(info, cx, 120, Theme.Text, FitScale(info, 2, maxTextWidth));

            // 4) menu
            int startY = 240, gap = 34;
            for (int i = 0; i < _options.Length; i++)
            {
                bool sel = i == _selected;
                string label = sel ? $"> {_options[i]} <" : _options[i];
                _context.Renderer.DrawTextCentered(label, cx, startY + i * gap, sel ? Theme.Accent : Theme.Text, 2);
            }

            _context.Renderer.DrawTextCentered("[UP/DOWN] SELECT    [ENTER] CONFIRM    [ESC] MENU", cx, h - 30, Theme.Muted, 1);

            _context.Renderer.EndDraw();
        }

        /// <summary>Largest scale (down to 1) at which <paramref name="text"/> fits in maxWidth.
        /// PixelFont advances 6 px per character per scale (5 px glyph + 1 px spacing).</summary>
        private static int FitScale(string text, int maxScale, int maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return maxScale;
            for (int sc = maxScale; sc > 1; sc--)
                if (text.Length * 6 * sc <= maxWidth) return sc;
            return 1;
        }

        private string Headline()
        {
            if (_endReason != null) return _endReason;                       // e.g. PLAYER 2 DISCONNECTED
            if (_isReplayView) return _isGameCompleted ? "REPLAY FINISHED" : "REPLAY ENDED";
            return _winnerId == -1 ? "DRAW GAME" : $"PLAYER {_winnerId + 1} WINS";
        }

        private string SubInfo()
        {
            if (_endReason != null) return "MATCH ENDED EARLY";
            if (_isReplayView) return _isGameCompleted ? "GAME COMPLETED" : "RECORDING STOPPED BEFORE GAME OVER";
            return "GAME COMPLETED";
        }
    }
}
