using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.Core.Game;

namespace Bomberman.App.States
{
    /// <summary>
    /// Displays the game over screen (Win/Draw) or Replay completion screen.
    /// Handles replay saving and naming.
    /// </summary>
    public class GameOverState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private GameSession _session;
        private bool _isReplayView;
        private bool _isGameCompleted;
        private int _winnerId;
        private string _replayName = "replay";
        private KeyboardState _prevKeyboard;

        public GameOverState(GameContext context, GameStateManager manager, GameSession session, int winnerId, bool isReplayView = false, bool isGameCompleted = true)
        {
            _context = context;
            _manager = manager;
            _session = session;
            _winnerId = winnerId;
            _isReplayView = isReplayView;
            _isGameCompleted = isGameCompleted;
            _replayName = $"Replay_{DateTime.Now:yyyyMMdd_HHmm}";
        }

        public void Enter()
        {
            _context.Logger.Info($"[GameOver] Winner: {_winnerId} (ReplayView={_isReplayView})");
            _prevKeyboard = _context.Input.GetKeyboard();
        }

        public void Exit()
        {
            // Nothing to clean up
        }

        public void Update(GameTime gameTime)
        {
            if (_context.Network != null) _context.Network.Update(); 
            
            // Still need raw keyboard for text entry detection
            var kState = _context.Input.GetKeyboard();

            if (_isReplayView)
            {
                // Replay View Exit Logic
                if (_context.Input.IsMenuCancel())
                {
                    _manager.ChangeState(_context.StateFactory.CreateMenu());
                    return;
                }
                _prevKeyboard = kState;
                return;
            }

            // Normal Game Over Logic (Save Replay)
            if (_context.Input.IsMenuSelect())
            {
                SaveReplayAndExit();
                return;
            }
            if (_context.Input.IsMenuCancel())
            {
                // Discard
                _manager.ChangeState(_context.StateFactory.CreateMenu());
                return;
            }

            HandleTextInput(kState);
            _prevKeyboard = kState;
        }

        private void HandleTextInput(KeyboardState kState)
        {
            // Simple Backspace
            if (kState.IsKeyDown(Keys.Back) && !_prevKeyboard.IsKeyDown(Keys.Back) && _replayName.Length > 0)
            {
                _replayName = _replayName.Substring(0, _replayName.Length - 1);
            }

            // Alphanumeric Input Logic
            Keys[] keys = kState.GetPressedKeys();
            foreach (Keys key in keys)
            {
                if (!_prevKeyboard.IsKeyDown(key))
                {
                    string charToAdd = "";
                    if (key >= Keys.A && key <= Keys.Z) charToAdd = key.ToString();
                    else if (key >= Keys.D0 && key <= Keys.D9) charToAdd = (key - Keys.D0).ToString();
                    else if (key == Keys.OemMinus) charToAdd = "_";

                    if (!string.IsNullOrEmpty(charToAdd) && _replayName.Length < 20)
                    {
                        _replayName += charToAdd;
                    }
                }
            }
        }

        private void SaveReplayAndExit()
        {
            try
            {
                string replayDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Replays");
                if (!Directory.Exists(replayDir)) Directory.CreateDirectory(replayDir);
                
                string filename = $"{_replayName}.json";
                foreach(char c in Path.GetInvalidFileNameChars()) filename = filename.Replace(c, '_');

                string fullPath = Path.Combine(replayDir, filename);
                _session.SaveReplay(fullPath);
                _context.Logger.Info($"Replay Saved: {fullPath}");
            }
            catch (Exception ex)
            {
                _context.Logger.Error($"Failed to save replay: {ex.Message}", ex);
            }

            if (_context.Network != null)
            {
                _context.Network.Close();
                _context.Network = null;
            }

            _manager.ChangeState(_context.StateFactory.CreateMenu());
        }

        public void Draw(GameTime gameTime)
        {
            _context.Renderer.ClearScreen(Rendering.Theme.Bg);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.WindowWidth / 2;
            int h = _context.Game.WindowHeight;

            string title = GetTitle(_isReplayView, _isGameCompleted, _winnerId);
            Color titleColor;
            int scale = 4;

            if (_isReplayView && !_isGameCompleted)
            {
                titleColor = Rendering.Theme.Accent;
                scale = 3; // Reduce scale to fit
            }
            else
            {
                titleColor = _winnerId == -1 ? Rendering.Theme.Muted : Rendering.Theme.Accent;
            }
            
            _context.Renderer.DrawTextCentered(title, centerX, 100, titleColor, scale);

            if (_isReplayView)
            {
                 if (!_isGameCompleted)
                 {
                     _context.Renderer.DrawTextCentered("RECORDING STOPPED BEFORE GAME OVER.", centerX, 200, Rendering.Theme.Text, 2);
                 }
                 else
                 {
                     _context.Renderer.DrawTextCentered("REPLAY FINISHED", centerX, 250, Rendering.Theme.Title, 2);
                 }
                 _context.Renderer.DrawTextCentered("PRESS ESC TO RETURN TO MENU", centerX, 400, Rendering.Theme.Text, 2);
            }
            else
            {
                // Replay Input
                _context.Renderer.DrawTextCentered("NAME YOUR REPLAY:", centerX, 250, Rendering.Theme.Text, 2);
                _context.Renderer.DrawTextCentered(_replayName + "_", centerX, 280, Rendering.Theme.Accent, 2);

                _context.Renderer.DrawTextCentered("PRESS ENTER TO SAVE AND EXIT", centerX, 400, Rendering.Theme.Text, 2);
                _context.Renderer.DrawTextCentered("PRESS ESC TO DISCARD", centerX, 430, Rendering.Theme.Muted, 2);
            }

            _context.Renderer.EndDraw();
        }

        public static string GetTitle(bool isReplay, bool isComplete, int winnerId)
        {
            if (isReplay && !isComplete)
            {
                return "REPLAY ENDED (INCOMPLETE)";
            }
            else
            {
                return winnerId == -1 ? "DRAW GAME!" : $"PLAYER {winnerId + 1} WINS!";
            }
        }
    }
}
