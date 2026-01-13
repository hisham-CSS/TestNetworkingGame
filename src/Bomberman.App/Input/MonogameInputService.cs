using Microsoft.Xna.Framework.Input;
using Bomberman.Core.Input;
using Bomberman.Core;

namespace Bomberman.App.Input
{
    public class MonogameInputService : IInputService
    {
        private KeyboardState _current;
        private KeyboardState _previous;

        // Call this once per frame if we want stateful tracking (JustPressed)
        // usage in states currently grabs fresh state. 
        // For simplicity, we'll grab fresh state here for isDown checks.
        // But for "Pressed" (trigger), we need previous state.
        // The existing States manage _prevKeyboard themselves.
        // To abstract that, this Service needs to be updated every frame explicitly? 
        // Or specific "IsNewPress" methods?
        
        // Let's stick to "IsDown" for now or expose JustPressed logic.
        // Current interface just asks "IsMenuUp". Is that Down or Pressed?
        // Menu usually wants Pulse (Pressed).
        // Let's modify interface to support both or handle state internally if context updates it.
        
        // BETTER: allow States to pass their previous state or manage update here?
        // Managing Update here requires GameContext to call input.Update().
        // Let's add Update() to interface?
        
        // Simpler: Just implement direct checks first (IsDown). 
        // The states (MenuState) rely on IsNewKeyPress.
        // So we need IsNewKeyPress abstraction.
        
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
            // For now, only Local Player (Index 0/controlled) maps to Keyboard.
            // P2/P3/P4 would come from Gamepad if implemented.
            
            var state = new InputState();
            state.Movement = IntVector2.Zero;

            // WASD or Arrows
            if (_current.IsKeyDown(Keys.W) || _current.IsKeyDown(Keys.Up)) state.Movement.Y -= 1;
            if (_current.IsKeyDown(Keys.S) || _current.IsKeyDown(Keys.Down)) state.Movement.Y += 1;
            if (_current.IsKeyDown(Keys.A) || _current.IsKeyDown(Keys.Left)) state.Movement.X -= 1;
            if (_current.IsKeyDown(Keys.D) || _current.IsKeyDown(Keys.Right)) state.Movement.X += 1;

            // Bomb (Space) - Continuous or Pulse?
            // Game logic usually handles Pulse for planting, but InputState stores "IsDown" often?
            // Core logic: "if (input.PlaceBomb && canPlace) ..."
            // Usually we send "PlaceBomb" as true only on the frame it was pressed?
            // PlayState logic: `if (kState.IsKeyDown(Keys.Space) && !_prev.IsKeyDown(Keys.Space)) _pendingBomb = true`
            // So PlayState wants a Pulse.
            
            // If we return "IsDown", Core needs to handle debouncing.
            // But PlayState currently handles debouncing.
            // Let's return IsNewPress for Bomb to match PlayState intent.
            
            state.PlaceBomb = IsNewPress(Keys.Space);
            
            return state;
        }

        private bool IsNewPress(Keys key)
        {
            return _current.IsKeyDown(key) && !_previous.IsKeyDown(key);
        }
    }
}
