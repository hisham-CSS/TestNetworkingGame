using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.App.Input;

namespace Bomberman.App.States
{
    public class TextInputState : IGameState
    {
        private GameContext _context;
        private GameStateManager _manager;
        private string _prompt;
        private string _currentInput = "";
        private Action<string> _onConfirm;
        private KeyboardState _prevKeyboard;

        public TextInputState(GameContext context, GameStateManager manager, string prompt, Action<string> onConfirm, string defaultText = "")
        {
            _context = context;
            _manager = manager;
            _prompt = prompt;
            _onConfirm = onConfirm;
            _currentInput = defaultText;
        }

        public void Enter()
        {
            _prevKeyboard = Keyboard.GetState();
            // Clear input buffer if needed
        }

        public void Exit() { }

        public void Update(GameTime gameTime)
        {
            var kState = Keyboard.GetState();

            // Handle Character Input (Simplified for IP/Numbers)
            foreach (var key in kState.GetPressedKeys())
            {
                if (!_prevKeyboard.IsKeyDown(key))
                {
                    if (key == Keys.Back && _currentInput.Length > 0)
                    {
                        _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
                    }
                    else if (key == Keys.Enter)
                    {
                        _onConfirm?.Invoke(_currentInput);
                    }
                    else if (key == Keys.Space)
                    {
                        _currentInput += " ";
                    }
                    else if (key == Keys.OemPeriod || key == Keys.Decimal)
                    {
                        _currentInput += ".";
                    }
                    else
                    {
                        string charStr = GetCharFromKey(key);
                        if (!string.IsNullOrEmpty(charStr))
                        {
                            _currentInput += charStr;
                        }
                    }
                }
            }

            _prevKeyboard = kState;
        }

        private string GetCharFromKey(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z) return key.ToString();
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)key - (int)Keys.D0).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return ((int)key - (int)Keys.NumPad0).ToString();
            return "";
        }

        public void Draw(GameTime gameTime)
        {
            _context.Renderer.ClearScreen(Color.Black);
            _context.Renderer.BeginDraw();

            int centerX = _context.Game.WindowWidth / 2;
            int centerY = _context.Game.WindowHeight / 2;

            _context.Renderer.DrawTextCentered(_prompt, centerX, centerY - 50, Color.Yellow, 2);
            _context.Renderer.DrawTextCentered($"> {_currentInput}_", centerX, centerY + 20, Color.White, 2);
            
            _context.Renderer.EndDraw();
        }
    }
}
