using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Bomberman.Core.Game;

namespace Bomberman.App.States
{
    public class ReplaySelectState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private List<string> _replayFiles = new List<string>();
        private int _selection = 0;
        private bool _prevDown;
        private bool _prevUp;
        private bool _prevEnter;
        private bool _prevEsc;

        public ReplaySelectState(GameContext context, GameStateManager manager)
        {
            _context = context;
            _manager = manager;
        }

        public void Enter()
        {
            Console.WriteLine("[ReplaySelectState] Enter");
            
            var kState = Keyboard.GetState();
            _prevDown = kState.IsKeyDown(Keys.Down);
            _prevUp = kState.IsKeyDown(Keys.Up);
            _prevEnter = kState.IsKeyDown(Keys.Enter);
            _prevEsc = kState.IsKeyDown(Keys.Escape);

            string replayDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Replays");
            if (Directory.Exists(replayDir))
            {
                _replayFiles = Directory.GetFiles(replayDir, "*.json")
                                        .OrderByDescending(f => File.GetCreationTime(f))
                                        .ToList();
            }
            _selection = 0;
        }

        public void Exit()
        {
            Console.WriteLine("[ReplaySelectState] Exit");
        }

        public void Update(GameTime gameTime)
        {
            var kState = Keyboard.GetState();

            bool down = kState.IsKeyDown(Keys.Down);
            bool up = kState.IsKeyDown(Keys.Up);
            bool enter = kState.IsKeyDown(Keys.Enter);
            bool esc = kState.IsKeyDown(Keys.Escape);

            if (_replayFiles.Count > 0)
            {
                if (down && !_prevDown) _selection++;
                if (up && !_prevUp) _selection--;

                if (_selection < 0) _selection = _replayFiles.Count - 1;
                if (_selection >= _replayFiles.Count) _selection = 0;

                if (enter && !_prevEnter)
                {
                    string selectedFile = _replayFiles[_selection];
                    Console.WriteLine($"Loading Replay: {selectedFile}");
                    
                    // Launch Replay
                    GameSession replaySession = new GameSession(selectedFile);
                    _manager.ChangeState(new PlayState(_context, _manager, replaySession));
                }
            }
            
            if (esc && !_prevEsc)
            {
                _manager.ChangeState(new MenuState(_context, _manager));
            }

            _prevDown = down;
            _prevUp = up;
            _prevEnter = enter;
            _prevEsc = esc;
        }

        public void Draw(GameTime gameTime)
        {
            _context.Game.GraphicsDevice.Clear(Color.Black);
            _context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);

            _context.Font.DrawText(_context.SpriteBatch, 10, 10, "SELECT REPLAY (ESC to Back)", Color.Yellow, 3);

            if (_replayFiles.Count == 0)
            {
                _context.Font.DrawText(_context.SpriteBatch, 20, 60, "No Replays Found", Color.Gray, 2);
            }
            else
            {
                int y = 60;
                // Show a window of replays if list is long? For now show all.
                for (int i = 0; i < _replayFiles.Count; i++)
                {
                    string filename = Path.GetFileName(_replayFiles[i]);
                    string marker = (i == _selection) ? "> " : "  ";
                    Color color = (i == _selection) ? Color.White : Color.Gray;
                    
                    _context.Font.DrawText(_context.SpriteBatch, 20, y, $"{marker}{filename}", color, 2);
                    y += 24;
                }
            }

            _context.SpriteBatch.End();
        }
    }
}
