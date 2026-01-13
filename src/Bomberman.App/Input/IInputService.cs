using Microsoft.Xna.Framework.Input;
using Bomberman.Core.Input;

namespace Bomberman.App.Input
{
    public interface IInputService
    {
        // Legacy/Direct Access (optional, can be deprecated)
        KeyboardState GetKeyboard();
        
        void Update();
        
        // Menu Abstractions
        bool IsMenuUp();
        bool IsMenuDown();
        bool IsMenuLeft();
        bool IsMenuRight();
        bool IsMenuSelect(); // Enter/Space
        bool IsMenuCancel(); // Esc
        bool IsMenuToggle(); // Space (Ready)
        bool IsDebugToggle(); // F1
        
        // Menu Specific Hotkeys
        bool IsGameHost(); // H
        bool IsGameJoin(); // J
        bool IsGameReplay(); // R

        // Gameplay Abstractions
        // Returns partial state (Movement, PlaceBomb)
        // Does NOT calculate BombTarget (requires World context)
        InputState GetGameInput(int playerIndex); 
    }
}
