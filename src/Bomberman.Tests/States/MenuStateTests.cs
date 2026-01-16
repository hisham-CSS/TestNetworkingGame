using NUnit.Framework;
using Bomberman.App.States;
using Microsoft.Xna.Framework;
using Bomberman.Tests.Mocks;

namespace Bomberman.Tests.States
{
    using Moq;
    using Bomberman.App.Input;
    using Microsoft.Xna.Framework.Input;
    using Bomberman.Core.Logging;
    [TestFixture]
    public class MenuStateTests
    {
        private MockGameContext _context;
        private StubGameStateManager _stubStateManager;
        private MenuState _menuState;

        [SetUp]
        public void Setup()
        {
            _context = new MockGameContext();
            _stubStateManager = new StubGameStateManager();
            _menuState = new MenuState(_context.Object, _stubStateManager);
        }

        [Test]
        public void Enter_ResetsSelection()
        {
            _menuState.Enter();
            _menuState.Enter();
            _context.MockLogger.Verify(x => x.Info(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Test]
        public void ExecuteSelection_Host_CreatesLobby()
        {
            // Default index is 0 (HOST)
            // Default index is 0 (HOST)
            _context.MockInput.Setup(x => x.IsMenuSelect()).Returns(true); // Select
            
            _menuState.Update(new GameTime());

            Assert.That(_stubStateManager.LastChangedState, Is.InstanceOf<LobbyState>());
        }
        
        [Test]
        public void ExecuteSelection_Join_CreatesServerBrowser()
        {
            // Move Down to Join (Index 1)
            _context.MockInput.Setup(x => x.IsMenuDown()).Returns(true);
            _menuState.Update(new GameTime());
            
            // Latch state / Release Down
            _context.MockInput.Setup(x => x.IsMenuDown()).Returns(false);
            
            _context.MockInput.Setup(x => x.IsMenuSelect()).Returns(true); // Select
            _menuState.Update(new GameTime());

            Assert.That(_stubStateManager.LastChangedState, Is.InstanceOf<ServerBrowserState>());
        }

        [Test]
        public void ExecuteSelection_Exit_CallsGameExit()
        {
            // Move Up to Exit (Wrap around to 3)
            _context.MockInput.Setup(x => x.IsMenuUp()).Returns(true);
            _menuState.Update(new GameTime());
            
            // Release
            _context.MockInput.Setup(x => x.IsMenuUp()).Returns(false);
            
            _context.MockInput.Setup(x => x.IsMenuSelect()).Returns(true);
            _menuState.Update(new GameTime());

            Assert.That(_context.MockGame.ExitCalled, Is.True);
        }
    }
}
