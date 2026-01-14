using Microsoft.Xna.Framework.Input;
using Bomberman.Core.Input;

namespace Bomberman.App.Input
{
    /// <summary>
    /// Service for handling user input, abstracting hardware details.
    /// Provides methods for menu navigation, game actions, and debug toggles.
    /// </summary>
    public interface IInputService
    {
        // Legacy/Direct Access (optional, can be deprecated)
        /// <summary>Returns the raw MonoGame KeyboardState.</summary>
        KeyboardState GetKeyboard();
        
        /// <summary>Updates the internal state of the input service (e.g. previous key states).</summary>
        void Update();
        
        // Menu Abstractions
        /// <summary>Checks if the menu 'Up' action was triggered.</summary>
        bool IsMenuUp();
        /// <summary>Checks if the menu 'Down' action was triggered.</summary>
        bool IsMenuDown();
        /// <summary>Checks if the menu 'Left' action was triggered.</summary>
        bool IsMenuLeft();
        /// <summary>Checks if the menu 'Right' action was triggered.</summary>
        bool IsMenuRight();
        /// <summary>Checks if the menu 'Select' action was triggered (Enter/Space).</summary>
        bool IsMenuSelect(); // Enter/Space
        /// <summary>Checks if the menu 'Cancel' action was triggered (Esc).</summary>
        bool IsMenuCancel(); // Esc
        /// <summary>Checks if the menu 'Toggle' action was triggered (Space/Ready).</summary>
        bool IsMenuToggle(); // Space (Ready)
        /// <summary>Checks if the 'Debug' toggle action was triggered (F1).</summary>
        bool IsDebugToggle(); // F1
        
        // Menu Specific Hotkeys
        /// <summary>Checks if the 'Host Game' hotkey was pressed.</summary>
        bool IsGameHost(); // H
        /// <summary>Checks if the 'Join Game' hotkey was pressed.</summary>
        bool IsGameJoin(); // J
        /// <summary>Checks if the 'Replay' hotkey was pressed.</summary>
        bool IsGameReplay(); // R

        // Gameplay Abstractions
        /// <summary>
        /// Retrieves the gameplay input state for a specific player (Movement, Bomb Place).
        /// Does NOT calculate BombTarget (requires World context).
        /// </summary>
        InputState GetGameInput(int playerIndex); 
    }
}
