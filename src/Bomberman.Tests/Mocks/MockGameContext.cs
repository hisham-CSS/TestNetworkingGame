using Bomberman.App.States;
using Bomberman.App.GameHost;
using Bomberman.App.Input;
using Bomberman.Core.Input;
using Bomberman.Core.Logging;
using Moq;

namespace Bomberman.Tests.Mocks
{
    public class MockGameContext
    {
        public GameContext Object { get; private set; }
        
        // Mocks
        public MockGameHost MockGame { get; private set; }
        public Mock<IInputService> MockInput { get; private set; }
        public MockRenderer MockRenderer { get; private set; } // Keep as manual mock for now if not IDisposable/Interface
        public Mock<ILogger> MockLogger { get; private set; }

        public MockGameContext()
        {
            MockGame = new MockGameHost();
            MockInput = new Mock<IInputService>();
            MockRenderer = new MockRenderer();
            MockLogger = new Mock<ILogger>();
            
            Object = new GameContext(
                MockGame, 
                null!, // SpriteBatch
                null!, // Texture2D
                null!, // Font
                MockInput.Object, 
                MockRenderer, 
                MockLogger.Object
            );
            
            // Factory
            Object.StateFactory = new StateFactory(Object, new GameStateManager());
        }
    }
}
