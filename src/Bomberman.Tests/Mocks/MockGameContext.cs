using Bomberman.App.States;
using Bomberman.App.GameHost;
using Bomberman.App.Input;
using Bomberman.Core.Input;
using Bomberman.Core.Logging;
using Bomberman.App.Rendering;
using Moq;

namespace Bomberman.Tests.Mocks
{
    public class MockGameContext
    {
        public GameContext Object { get; private set; }
        
        // Mocks
        public Mock<IGameHost> MockGame { get; private set; }
        public Mock<IInputService> MockInput { get; private set; }
        public Mock<IRenderer> MockRenderer { get; private set; }
        public Mock<ILogger> MockLogger { get; private set; }

        public MockGameContext()
        {
            MockGame = new Mock<IGameHost>();
            // Setup default behavior for MockGame if needed
            MockGame.Setup(g => g.WindowWidth).Returns(800);
            MockGame.Setup(g => g.WindowHeight).Returns(600);

            MockInput = new Mock<IInputService>();
            MockRenderer = new Mock<IRenderer>();
            MockLogger = new Mock<ILogger>();
            
            Object = new GameContext(
                MockGame.Object, 
                null!, // SpriteBatch
                null!, // Texture2D
                null!, // Font
                MockInput.Object, 
                MockRenderer.Object, 
                MockLogger.Object
            );
            
            // Factory
            Object.StateFactory = new StateFactory(Object, new GameStateManager());
        }
    }
}
