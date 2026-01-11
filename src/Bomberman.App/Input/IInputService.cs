using Microsoft.Xna.Framework.Input;

namespace Bomberman.App.Input
{
    public interface IInputService
    {
        KeyboardState GetKeyboard();
    }
}
