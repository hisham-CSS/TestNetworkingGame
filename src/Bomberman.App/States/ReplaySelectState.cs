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
    /// <summary>
    /// State for browsing and selecting saved replay files (.json) to watch.
    /// </summary>
    public class ReplaySelectState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private List<string> _replayFiles = new List<string>();
        
        // Navigation
        private int _selection = 0;
        private int _scrollOffset = 0;
        private const int MaxVisibleItems = 9;

        public ReplaySelectState(GameContext context, GameStateManager manager)
        {
            _context = context;
            _manager = manager;
        }

        public void Enter()
        {
            _context.Logger.Info("[ReplaySelectState] Enter");

            string replayDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Replays");
            if (Directory.Exists(replayDir))
            {
                _replayFiles = Directory.GetFiles(replayDir, "*.json")
                                        .OrderByDescending(f => File.GetCreationTime(f))
                                        .ToList();
            }
            _selection = 0;
            _scrollOffset = 0;
        }

        public void Exit()
        {
            _context.Logger.Info("[ReplaySelectState] Exit");
        }

        public void Update(GameTime gameTime)
        {
            bool down = _context.Input.IsMenuDown();
            bool up = _context.Input.IsMenuUp();
            bool enter = _context.Input.IsMenuSelect();
            bool esc = _context.Input.IsMenuCancel();

            if (_replayFiles.Count > 0)
            {
                if (down) 
                {
                    _selection++;
                    if (_selection >= _replayFiles.Count) _selection = 0;
                }
                if (up) 
                {
                    _selection--;
                    if (_selection < 0) _selection = _replayFiles.Count - 1;
                }

                // Update Scroll Offset
                if (_selection < _scrollOffset)
                {
                    _scrollOffset = _selection;
                }
                else if (_selection >= _scrollOffset + MaxVisibleItems)
                {
                    _scrollOffset = _selection - MaxVisibleItems + 1;
                }
                
                // Handle looping wrap-around for scroll logic
                if (_selection == 0) _scrollOffset = 0;
                // If we wrapped to end, adjust scroll to show end
                if (_selection == _replayFiles.Count - 1) 
                    _scrollOffset = Math.Max(0, _replayFiles.Count - MaxVisibleItems);


                if (enter)
                {
                    string selectedFile = _replayFiles[_selection];
                    _context.Logger.Info($"Loading Replay: {selectedFile}");
                    
                    try 
                    {
                        GameSession replaySession = new GameSession(selectedFile);
                        _manager.ChangeState(_context.StateFactory.CreateReplay(replaySession));
                    }
                    catch (Exception ex)
                    {
                        _context.Logger.Error($"Failed to load replay: {ex.Message}", ex);
                    }
                }
            }
            
            if (esc)
            {
                _manager.ChangeState(_context.StateFactory.CreateMenu());
            }
        }

        public void Draw(GameTime gameTime)
        {
            _context.Renderer.ClearScreen(Color.Black);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.WindowWidth / 2;
            int startY = 80;
            int gap = 30;

            _context.Renderer.DrawTextCentered("REPLAYS", centerX, 30, Color.White, 4);

            if (_replayFiles.Count == 0)
            {
                _context.Renderer.DrawTextCentered("No Replays Found", centerX, startY, Color.Gray, 2);
            }
            else
            {
                int itemsToShow = Math.Min(MaxVisibleItems, _replayFiles.Count);
                // Ensure we don't go out of bounds
                int endIndex = Math.Min(_scrollOffset + itemsToShow, _replayFiles.Count);

                for (int i = _scrollOffset; i < endIndex; i++)
                {
                    string filename = Path.GetFileNameWithoutExtension(_replayFiles[i]);
                    bool selected = (i == _selection);
                    string text = filename; 
                    if (selected) text = $"> {text} <";
                    Color color = selected ? Color.Yellow : Color.White;
                    
                    // index for drawing position (0 to MaxVisibleItems-1)
                    int drawIndex = i - _scrollOffset;
                    
                    _context.Renderer.DrawTextCentered(text, centerX, startY + (drawIndex * gap), color, 2);
                }
                
                // Scroll Indicators
                if (_scrollOffset > 0)
                    _context.Renderer.DrawTextCentered("^", centerX, startY - 20, Color.Gray, 1);
                
                if (_scrollOffset + itemsToShow < _replayFiles.Count)
                    _context.Renderer.DrawTextCentered("v", centerX, startY + (itemsToShow * gap), Color.Gray, 1);
            }

            _context.Renderer.EndDraw();
        }
    }
}
