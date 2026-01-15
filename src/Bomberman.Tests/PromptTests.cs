using System;
using Bomberman.App.States;
using Bomberman.App.GameHost;
using Bomberman.App.Input;
using Bomberman.App.Rendering;
using Bomberman.Tests.Mocks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NUnit.Framework;

namespace Bomberman.Tests
{
    [TestFixture]
    public class PromptTests
    {
        private MockInputService _input;
        private GameContext _context;
        private GameStateManager _manager;

        [SetUp]
        public void Setup()
        {
            _input = new MockInputService();
            
            // We need to mock GameContext dependencies.
            // Game1, SpriteBatch, Texture2D, PixelFont are hard to mock fully without GL, 
            // but we only strictly need Input for logic testing if we don't call Draw.
            
            // NOTE: We pass null/mocks for graphical components since we won't call PromptState.Draw() in unit tests.
            // PromptState only uses them in Draw().
            // Enter() and Update() only use Input.
            
            _context = new GameContext(new Mocks.MockGameHost(), null, null, null, _input, new Mocks.MockRenderer(), new Mocks.MockLogger());
            _manager = new GameStateManager();
        }

        [Test]
        public void Enter_DoesNotTriggerAction_Initially()
        {
            bool triggered = false;
            var state = new PromptState(_context, _manager, "Test", () => triggered = true);

            state.Enter();
            state.Update(new GameTime());

            Assert.That(triggered, Is.False);
        }

        [Test]
        public void Confirm_TriggersAction_OnEnterKeyPress()
        {
            bool triggered = false;
            var state = new PromptState(_context, _manager, "Test", () => triggered = true);

            state.Enter();
            
            // Press Enter
            _input.SetKeys(Keys.Enter);
            state.Update(new GameTime());

            Assert.That(triggered, Is.True);
        }

        [Test]
        public void Confirm_TriggersAction_OnSpaceKeyPress()
        {
            bool triggered = false;
            var state = new PromptState(_context, _manager, "Test", () => triggered = true);

            state.Enter();
            
            // Press Space
            _input.SetKeys(Keys.Space);
            state.Update(new GameTime());

            Assert.That(triggered, Is.True);
        }

        [Test]
        public void HoldingKey_FromPreviousState_DoesNotTrigger()
        {
            bool triggered = false;
            
            // User holding Enter when entering state
            _input.SetKeys(Keys.Enter);
            _input.Update(); // Move to 'Previous' state to simulate holding
            _input.SetKeys(Keys.Enter); // Ensure it's still down in 'Current'
            
            var state = new PromptState(_context, _manager, "Test", () => triggered = true);

            state.Enter();
            
            // Still holding
            state.Update(new GameTime());
            Assert.That(triggered, Is.False, "Should not trigger if key was already held");

            // Release
            _input.Update(); // Prepare for next frame
            _input.SetKeys(); // Empty
            state.Update(new GameTime());
            Assert.That(triggered, Is.False);

            // Press Again
            _input.Update(); // Previous = Empty
            _input.SetKeys(Keys.Enter); // Current = Enter
            state.Update(new GameTime());
            Assert.That(triggered, Is.True);
        }
    }
}
