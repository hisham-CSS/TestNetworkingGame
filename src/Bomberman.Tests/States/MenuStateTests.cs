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
        private Mock<GameStateManager> _mockStateManager;
        private MenuState _menuState;

        [SetUp]
        public void Setup()
        {
            _context = new MockGameContext();
            _mockStateManager = new Mock<GameStateManager>();
            _menuState = new MenuState(_context.Object, _mockStateManager.Object);
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
            _context.MockInput.Setup(x => x.IsMenuSelect()).Returns(true); // Select
            
            _menuState.Update(new GameTime());

            _mockStateManager.Verify(x => x.ChangeState(It.IsAny<LobbyState>()), Times.Once);
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

            _mockStateManager.Verify(x => x.ChangeState(It.IsAny<ServerBrowserState>()), Times.Once);
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

            _context.MockGame.Verify(x => x.Exit(), Times.Once);
        }
    }
}
