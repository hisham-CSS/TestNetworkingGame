using Microsoft.Xna.Framework.Input;

namespace Bomberman.App.Input
{
    public class MonogameInputService : IInputService
    {
        public KeyboardState GetKeyboard()
        {
            return Keyboard.GetState();
        }
    }
}
