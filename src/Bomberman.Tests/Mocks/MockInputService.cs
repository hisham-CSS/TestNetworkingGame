using Bomberman.App.Input;
using Microsoft.Xna.Framework.Input;
using Bomberman.Core.Input;
using Bomberman.Core;

namespace Bomberman.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IInputService for testing.
    /// Allows setting input states programmatically.
    /// </summary>
    public class MockInputService : IInputService
    {
        private KeyboardState _current;
        private KeyboardState _previous;

        // Flags for mocking specific actions if needed, or we can map them to keys like Monogame Service
        public bool MockMenuUp { get; set; }
        public bool MockMenuDown { get; set; }
        public bool MockMenuConfirm { get; set; }
        public bool MockMenuCancel { get; set; }

        public MockInputService()
        {
            _current = new KeyboardState();
            _previous = new KeyboardState();
        }

        public void SetKeys(params Keys[] keys)
        {
            _current = new KeyboardState(keys);
        }

        public KeyboardState GetKeyboard()
        {
            return _current;
        }

        public void Update() 
        { 
            _previous = _current;
        }

        private bool IsNewPress(Keys key)
        {
            return _current.IsKeyDown(key) && !_previous.IsKeyDown(key);
        }

        public bool IsMenuUp() => MockMenuUp || IsNewPress(Keys.Up);
        public bool IsMenuDown() => MockMenuDown || IsNewPress(Keys.Down);
        public bool IsMenuLeft() => IsNewPress(Keys.Left);
        public bool IsMenuRight() => IsNewPress(Keys.Right);
        public bool IsMenuSelect() => MockMenuConfirm || IsNewPress(Keys.Enter);
        public bool IsMenuCancel() => MockMenuCancel || IsNewPress(Keys.Escape);
        public bool IsMenuToggle() => IsNewPress(Keys.Space);
        public bool IsDebugToggle() => IsNewPress(Keys.F1);

        public bool IsGameHost() => IsNewPress(Keys.H);
        public bool IsGameJoin() => IsNewPress(Keys.J);
        public bool IsGameReplay() => IsNewPress(Keys.R);

        public InputState GetGameInput(int playerIndex) 
        {
            var s = new InputState();
            s.Movement = IntVector2.Zero;
            if (_current.IsKeyDown(Keys.W)) s.Movement.Y = -1;
            if (_current.IsKeyDown(Keys.S)) s.Movement.Y = 1;
            if (_current.IsKeyDown(Keys.A)) s.Movement.X = -1;
            if (_current.IsKeyDown(Keys.D)) s.Movement.X = 1;
            s.PlaceBomb = IsNewPress(Keys.Space);
            return s;
        }
    }
}
