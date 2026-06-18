using NUnit.Framework;
using Bomberman.App.States;
using Bomberman.Tests.Mocks;
using Bomberman.App.Input;
using Bomberman.Core.Input;
using Bomberman.Core.Logging;
using Moq;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System.Reflection;

using Bomberman.App.GameHost;
using Bomberman.App.Rendering;

namespace Bomberman.Tests.States
{
    [TestFixture]
    public class GameOverStateTests
    {
        private Mock<IInputService> _input;
        private GameContext _context;
        private GameOverState _state = null!;

        [SetUp]
        public void Setup()
        {
            _input = new Mock<IInputService>();
            var game = new Mock<IGameHost>();
            game.Setup(g => g.WindowWidth).Returns(800);
            game.Setup(g => g.WindowHeight).Returns(600);
            
            // We need a context with Input
            _context = new GameContext(game.Object, null!, null!, null!, _input.Object, new Mock<IRenderer>().Object, new Mock<ILogger>().Object);
        }

        [Test]
        public void GameOverState_Initialization_ReplayView()
        {
            _state = new GameOverState(_context, null!, null!, -1, true, false);
            
            var isReplay = (bool)GetPrivateField(_state, "_isReplayView");
            var isComplete = (bool)GetPrivateField(_state, "_isGameCompleted");

            Assert.That(isReplay, Is.True);
            Assert.That(isComplete, Is.False);
        }

        [Test]
        public void GameOverState_Initialization_GameComplete()
        {
            _state = new GameOverState(_context, null!, null!, 1, false, true);

            var isReplay = (bool)GetPrivateField(_state, "_isReplayView");
            var isComplete = (bool)GetPrivateField(_state, "_isGameCompleted");
            var winner = (int)GetPrivateField(_state, "_winnerId");

            Assert.That(isReplay, Is.False);
            Assert.That(isComplete, Is.True);
            Assert.That(winner, Is.EqualTo(1));
        }

        [Test]
        public void MenuOptions_ReplayView_OffersRewatch()
        {
            _state = new GameOverState(_context, null!, null!, -1, isReplayView: true, isGameCompleted: true);
            var options = (string[])GetPrivateField(_state, "_options");
            Assert.That(options, Does.Contain("REWATCH"));
            Assert.That(options, Does.Contain("RETURN TO MENU"));
        }

        [Test]
        public void MenuOptions_LiveGame_OffersSaveReplay()
        {
            _state = new GameOverState(_context, null!, null!, 1, isReplayView: false, isGameCompleted: true);
            var options = (string[])GetPrivateField(_state, "_options");
            Assert.That(options, Does.Contain("SAVE REPLAY"));
            Assert.That(options, Does.Contain("RETURN TO MENU"));
        }

        [Test]
        public void Disconnection_EndReason_IsStored()
        {
            _state = new GameOverState(_context, null!, null!, 0, false, false, "PLAYER 2 DISCONNECTED");
            var reason = (string)GetPrivateField(_state, "_endReason");
            Assert.That(reason, Is.EqualTo("PLAYER 2 DISCONNECTED"));
        }

        private object GetPrivateField(object obj, string name)
        {
            return obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        }
    }
}
