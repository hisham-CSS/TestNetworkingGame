using Microsoft.Xna.Framework.Input;
using Bomberman.Core.Input;
using Bomberman.Core;

namespace Bomberman.App.Input
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class MonogameInputService : IInputService
    {
        private KeyboardState _current;
        private KeyboardState _previous;

        /// <summary>
        /// Updates the input service state. Should be called once per frame.
        /// </summary>
        
        public void Update()
        {
            _previous = _current;
            _current = Keyboard.GetState();
        }

        public KeyboardState GetKeyboard() => _current;

        // Menu (Navigation - Pulse)
        public bool IsMenuUp() => IsNewPress(Keys.Up) || IsNewPress(Keys.W);
        public bool IsMenuDown() => IsNewPress(Keys.Down) || IsNewPress(Keys.S);
        public bool IsMenuLeft() => IsNewPress(Keys.Left) || IsNewPress(Keys.A);
        public bool IsMenuRight() => IsNewPress(Keys.Right) || IsNewPress(Keys.D);
        public bool IsMenuSelect() => IsNewPress(Keys.Enter);
        public bool IsMenuCancel() => IsNewPress(Keys.Escape);
        public bool IsMenuToggle() => IsNewPress(Keys.Space);
        public bool IsDebugToggle() => IsNewPress(Keys.F1);

        public bool IsGameHost() => IsNewPress(Keys.H);
        public bool IsGameJoin() => IsNewPress(Keys.J);
        public bool IsGameReplay() => IsNewPress(Keys.R);

        // Gameplay (Continuous state)
        public InputState GetGameInput(int playerIndex)
        {
            // Only Local Player (Index 0) maps to Keyboard.
            
            var state = new InputState();
            state.Movement = IntVector2.Zero;

            // WASD or Arrows
            if (_current.IsKeyDown(Keys.W) || _current.IsKeyDown(Keys.Up)) state.Movement.Y -= 1;
            if (_current.IsKeyDown(Keys.S) || _current.IsKeyDown(Keys.Down)) state.Movement.Y += 1;
            if (_current.IsKeyDown(Keys.A) || _current.IsKeyDown(Keys.Left)) state.Movement.X -= 1;
            if (_current.IsKeyDown(Keys.D) || _current.IsKeyDown(Keys.Right)) state.Movement.X += 1;

            // Bomb input should be a pulse (IsNewPress) to prevent spamming
            
            state.PlaceBomb = IsNewPress(Keys.Space);
            
            return state;
        }

        private bool IsNewPress(Keys key)
        {
            return _current.IsKeyDown(key) && !_previous.IsKeyDown(key);
        }
    }
}
