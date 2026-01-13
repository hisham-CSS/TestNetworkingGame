using Bomberman.App.Input;
using Microsoft.Xna.Framework.Input;
using Bomberman.Core.Input;
using Bomberman.Core;

namespace Bomberman.Tests.Mocks
{
    public class MockInputService : IInputService
    {
        private KeyboardState _state;

        // Flags for mocking specific actions if needed, or we can map them to keys like Monogame Service
        public bool MockMenuUp { get; set; }
        public bool MockMenuDown { get; set; }
        public bool MockMenuConfirm { get; set; }
        public bool MockMenuCancel { get; set; }

        public MockInputService()
        {
            _state = new KeyboardState();
        }

        public void SetKeys(params Keys[] keys)
        {
            _state = new KeyboardState(keys);
        }

        public KeyboardState GetKeyboard()
        {
            return _state;
        }

        public void Update() 
        { 
            // No-op for mock, unless we want to simulate frame progression
        }

        public bool IsMenuUp() => MockMenuUp || _state.IsKeyDown(Keys.Up);
        public bool IsMenuDown() => MockMenuDown || _state.IsKeyDown(Keys.Down);
        public bool IsMenuLeft() => _state.IsKeyDown(Keys.Left);
        public bool IsMenuRight() => _state.IsKeyDown(Keys.Right);
        public bool IsMenuSelect() => MockMenuConfirm || _state.IsKeyDown(Keys.Enter);
        public bool IsMenuCancel() => MockMenuCancel || _state.IsKeyDown(Keys.Escape);
        public bool IsMenuToggle() => _state.IsKeyDown(Keys.Space);
        public bool IsDebugToggle() => _state.IsKeyDown(Keys.F1);

        public bool IsGameHost() => _state.IsKeyDown(Keys.H);
        public bool IsGameJoin() => _state.IsKeyDown(Keys.J);
        public bool IsGameReplay() => _state.IsKeyDown(Keys.R);

        public InputState GetGameInput(int playerIndex) 
        {
            var s = new InputState();
            s.Movement = IntVector2.Zero;
            if (_state.IsKeyDown(Keys.W)) s.Movement.Y = -1;
            if (_state.IsKeyDown(Keys.S)) s.Movement.Y = 1;
            if (_state.IsKeyDown(Keys.A)) s.Movement.X = -1;
            if (_state.IsKeyDown(Keys.D)) s.Movement.X = 1;
            s.PlaceBomb = _state.IsKeyDown(Keys.Space);
            return s;
        }
    }
}
