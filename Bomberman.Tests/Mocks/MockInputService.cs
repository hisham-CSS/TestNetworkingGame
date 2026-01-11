using Bomberman.App.Input;
using Microsoft.Xna.Framework.Input;

namespace Bomberman.Tests.Mocks
{
    public class MockInputService : IInputService
    {
        private KeyboardState _state;

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
    }
}
