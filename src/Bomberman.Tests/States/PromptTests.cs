using System;
using Bomberman.App.States;
using Bomberman.App.GameHost;
using Bomberman.App.Input;
using Bomberman.App.GameHost;
using Bomberman.App.Rendering;
using Bomberman.Tests.Mocks;
using Bomberman.Core.Input;
using Bomberman.Core.Logging;
using Moq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NUnit.Framework;

namespace Bomberman.Tests.States
{
    [TestFixture]
    public class PromptTests
    {
        private Mock<IInputService> _input;
        private GameContext _context;
        private GameStateManager _manager;

        [SetUp]
        public void Setup()
        {
            _input = new Mock<IInputService>();
            
            // We need to mock GameContext dependencies.
            // Game1, SpriteBatch, Texture2D, PixelFont are hard to mock fully without GL, 
            // but we only strictly need Input for logic testing if we don't call Draw.
            
            // NOTE: We pass null/mocks for graphical components since we won't call PromptState.Draw() in unit tests.
            // PromptState only uses them in Draw().
            // Enter() and Update() only use Input.
            
            var mockGame = new Mock<IGameHost>();
            mockGame.Setup(g => g.WindowWidth).Returns(800);
            mockGame.Setup(g => g.WindowHeight).Returns(600);
            
            _context = new GameContext(mockGame.Object, null, null, null, _input.Object, new Mock<IRenderer>().Object, new Mock<ILogger>().Object);
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
            
            // Press Enter (Maps to IsMenuSelect in implementation, but we mock the abstract method)
            _input.Setup(x => x.IsMenuSelect()).Returns(true);
            state.Update(new GameTime());

            Assert.That(triggered, Is.True);
        }

        [Test]
        public void Confirm_TriggersAction_OnSpaceKeyPress()
        {
            bool triggered = false;
            var state = new PromptState(_context, _manager, "Test", () => triggered = true);

            state.Enter();
            
            // Press Space (Maps to IsMenuToggle usually)
            _input.Setup(x => x.IsMenuToggle()).Returns(true);
            state.Update(new GameTime());

            Assert.That(triggered, Is.True);
        }

        [Test]
        public void HoldingKey_FromPreviousState_DoesNotTrigger()
        {
            bool triggered = false;
            
            // Scenario: Input Service determined key is held, so 'IsMenuSelect' returns false.
            _input.Setup(x => x.IsMenuSelect()).Returns(false);
            
            var state = new PromptState(_context, _manager, "Test", () => triggered = true);

            state.Enter();
            
            // Update
            state.Update(new GameTime());
            Assert.That(triggered, Is.False, "Should not trigger if IsMenuSelect is false");

            // User releases and presses again -> IsMenuSelect returns true
            _input.Setup(x => x.IsMenuSelect()).Returns(true);
             
            state.Update(new GameTime());
            Assert.That(triggered, Is.True);
        }
    }
}
