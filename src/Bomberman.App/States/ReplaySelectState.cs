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


        public ReplaySelectState(GameContext context, GameStateManager manager)
        {
            _context = context;
            _manager = manager;
        }

        public void Enter()
        {
            Console.WriteLine("[ReplaySelectState] Enter");

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
            bool down = _context.Input.IsMenuDown();
            bool up = _context.Input.IsMenuUp();
            bool enter = _context.Input.IsMenuSelect();
            bool esc = _context.Input.IsMenuCancel();

            if (_replayFiles.Count > 0)
            {
                if (down) _selection++;
                if (up) _selection--;

                if (_selection < 0) _selection = _replayFiles.Count - 1;
                if (_selection >= _replayFiles.Count) _selection = 0;

                if (enter)
                {
                    string selectedFile = _replayFiles[_selection];
                    Console.WriteLine($"Loading Replay: {selectedFile}");
                    
                    // Launch Replay
                    GameSession replaySession = new GameSession(selectedFile);
                    _manager.ChangeState(_context.StateFactory.CreateReplay(replaySession));
                }
            }
            
            if (esc)
            {
                _manager.ChangeState(_context.StateFactory.CreateMenu());
            }
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
