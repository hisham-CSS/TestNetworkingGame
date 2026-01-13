using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.Core.Game;

namespace Bomberman.App.States
{
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
            _prevKeyboard = Keyboard.GetState();
        }

        public void Exit()
        {
            // Nothing to clean up
        }

        public void Update(GameTime gameTime)
        {
            if (_context.Network != null) _context.Network.Update(); 
            
            var kState = Keyboard.GetState();

            if (_isReplayView)
            {
                // Simple Exit Logic for Replay View
                if (kState.IsKeyDown(Keys.Escape) && !_prevKeyboard.IsKeyDown(Keys.Escape))
                {
                    _manager.ChangeState(_context.StateFactory.CreateMenu());
                    return;
                }
                // Maybe 'R' to restart?
                _prevKeyboard = kState;
                return;
            }

            // Normal Game Over Logic (Save Replay)
            if (kState.IsKeyDown(Keys.Enter) && !_prevKeyboard.IsKeyDown(Keys.Enter))
            {
                SaveReplayAndExit();
                return;
            }
            if (kState.IsKeyDown(Keys.Escape) && !_prevKeyboard.IsKeyDown(Keys.Escape))
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

            // Simple Alpha-Numeric (Very basic)
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
            _context.Game.GraphicsDevice.Clear(Color.Black);
            _context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);

            int centerX = _context.Game.GraphicsDevice.Viewport.Width / 2;
            int h = _context.Game.GraphicsDevice.Viewport.Height;

            string title = GetTitle(_isReplayView, _isGameCompleted, _winnerId);
            Color titleColor;
            int scale = 4;

            if (_isReplayView && !_isGameCompleted)
            {
                titleColor = Color.Orange;
                scale = 3; // Reduce scale to fit
            }
            else
            {
                titleColor = _winnerId == -1 ? Color.Gray : Color.Gold;
            }
            
            DrawCenteredText(centerX, 100, title, titleColor, scale);

            if (_isReplayView)
            {
                 if (!_isGameCompleted)
                 {
                     DrawCenteredText(centerX, 200, "Recording stopped before game over.", Color.White, 2);
                 }
                 else
                 {
                     DrawCenteredText(centerX, 250, "REPLAY FINISHED", Color.Cyan, 2);
                 }
                 DrawCenteredText(centerX, 400, "Press ESC to Return to Menu", Color.White, 2);
            }
            else
            {
                // Replay Input
                DrawCenteredText(centerX, 250, "Name your Replay:", Color.White, 2);
                DrawCenteredText(centerX, 280, _replayName + "_", Color.Yellow, 2);

                DrawCenteredText(centerX, 400, "Press ENTER to Save & Exit", Color.White, 2);
                DrawCenteredText(centerX, 430, "Press ESC to Discard", Color.Gray, 2);
            }

            _context.SpriteBatch.End();
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

        private void DrawCenteredText(int centerX, int y, string text, Color color, int scale)
        {
            var size = _context.Font.MeasureString(text, scale);
            _context.Font.DrawText(_context.SpriteBatch, centerX - size.X / 2, y, text, color, scale);
        }
    }
}
