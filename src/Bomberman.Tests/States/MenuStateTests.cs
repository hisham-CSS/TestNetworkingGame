using NUnit.Framework;
using Bomberman.App.States;
using Microsoft.Xna.Framework;
using Bomberman.Tests.Mocks;

namespace Bomberman.Tests.States
{
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
            Assert.That(_context.MockLogger.Logs.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ExecuteSelection_Host_CreatesLobby()
        {
            // Default index is 0 (HOST)
            _context.MockInput.SetKeys(Microsoft.Xna.Framework.Input.Keys.Enter); // Select
            
            _menuState.Update(new GameTime());

            Assert.That(_stubStateManager.LastChangedState, Is.InstanceOf<LobbyState>());
        }
        
        [Test]
        public void ExecuteSelection_Join_CreatesServerBrowser()
        {
            // Move Down to Join (Index 1)
            _context.MockInput.SetKeys(Microsoft.Xna.Framework.Input.Keys.Down);
            _menuState.Update(new GameTime());
            _context.MockInput.Update(); // Latch state
            
            _context.MockInput.SetKeys(Microsoft.Xna.Framework.Input.Keys.Enter); // Select
            _menuState.Update(new GameTime());

            Assert.That(_stubStateManager.LastChangedState, Is.InstanceOf<ServerBrowserState>());
        }

        [Test]
        public void ExecuteSelection_Exit_CallsGameExit()
        {
            // Move Up to Exit (Wrap around to 3)
            _context.MockInput.SetKeys(Microsoft.Xna.Framework.Input.Keys.Up);
            _menuState.Update(new GameTime());
            _context.MockInput.Update();
            
            _context.MockInput.SetKeys(Microsoft.Xna.Framework.Input.Keys.Enter);
            _menuState.Update(new GameTime());

            Assert.That(_context.MockGame.ExitCalled, Is.True);
        }
    }
}
