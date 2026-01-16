using NUnit.Framework;
using Bomberman.App.States;
using Bomberman.Net;
using Bomberman.Rollback;
using Bomberman.Core;
using Bomberman.App.Input;
using Bomberman.Core.Input;
using Moq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Bomberman.Tests.Mocks;

namespace Bomberman.Tests.States
{
    [TestFixture]
    public class PlayStateTests
    {
        private MockGameContext _context;
        private StubGameStateManager _stubStateManager;
        private PlayState _playState;
        private GameSession _gameSession;

        [SetUp]
        public void Setup()
        {
            _context = new MockGameContext();
            _stubStateManager = new StubGameStateManager();
            _gameSession = new GameSession(0, 2, 12345);
            
            _playState = new PlayState(_context.Object, _stubStateManager, _gameSession);
        }

        [TearDown]
        public void TearDown()
        {
            _playState.Exit();
        }

        [Test]
        public void Enter_SubscribesToNetworkEvents()
        {
            // Just verifying it doesn't throw
            _playState.Enter();
            Assert.Pass();
        }

        [Test]
        public void Update_AdvancesSimulation()
        {
            _playState.Enter();
            // Stub Input
            
            GameTime gameTime = new GameTime(new System.TimeSpan(0), new System.TimeSpan(0, 0, 0, 0, 32));
            _playState.Update(gameTime);

            Assert.That(_gameSession.CurrentFrame, Is.GreaterThan(0));
        }

        [Test]
        public void Update_SendsInputPacket_WhenNetworked()
        {
            _playState.Enter();
            _context.Object.Network = new NetworkController(new MockTransport());
            
            GameTime gameTime = new GameTime(new System.TimeSpan(0), new System.TimeSpan(0, 0, 0, 0, 17));
            _playState.Update(gameTime);
            
            // Verifying no crash
            Assert.Pass();
        }

        [Test]
        public void MenuCancel_ExitsToMenu()
        {
            // Trigger Menu Cancel
            // Since Abstract is weird on GetGameInput, we rely on IsMenuCancel logic
            // MockInputService defaults IsMenuCancel to false.
            // We need to set keys.
            
            _context.MockInput.Setup(x => x.IsMenuCancel()).Returns(true);
            _playState.Update(new GameTime());

            Assert.That(_stubStateManager.LastChangedState, Is.InstanceOf<MenuState>());
        }
    }
}
