using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Chronos.Net;

namespace Bomberman.App.States
{
    public class ConnectingState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private Action _onConnected;
        private Action _onFailure;
        private float _timeout = 5.0f; // 5 seconds timeout
        private string _message = "Connecting to Relay...";

        public ConnectingState(GameContext context, GameStateManager manager, Action onConnected, Action onFailure)
        {
            _context = context;
            _manager = manager;
            _onConnected = onConnected;
            _onFailure = onFailure;
        }

        public void Enter() { }

        public void Exit() { }

        public void Update(GameTime gameTime)
        {
            _timeout -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            _context.Network?.Update();

            if (_context.Network?.Transport is RelayTransport relay)
            {
                if (relay.IsConnected)
                {
                    _onConnected?.Invoke();
                    return;
                }
            }

            if (_timeout <= 0)
            {
                _onFailure?.Invoke();
            }
            
            // Allow cancel
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                 _onFailure?.Invoke();
            }
        }

        public void Draw(GameTime gameTime)
        {
            _context.Renderer.ClearScreen(Color.Black);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.WindowWidth / 2;
            int centerY = _context.Game.WindowHeight / 2;

            _context.Renderer.DrawTextCentered(_message, centerX, centerY, Color.Yellow, 2);
            _context.Renderer.DrawTextCentered($"Timeout in {_timeout:0.0}s", centerX, centerY + 30, Color.White, 1);
            
            _context.Renderer.EndDraw();
        }
    }
}
