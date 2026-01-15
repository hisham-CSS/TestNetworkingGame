using Bomberman.App.States;
using Bomberman.App.GameHost;

namespace Bomberman.Tests.Mocks
{
    public class MockGameContext
    {
        public GameContext Object { get; private set; }
        
        // Mocks
        public MockGameHost MockGame { get; private set; }
        public MockInputService MockInput { get; private set; }
        public MockRenderer MockRenderer { get; private set; }
        public MockLogger MockLogger { get; private set; }

        public MockGameContext()
        {
            MockGame = new MockGameHost();
            MockInput = new MockInputService();
            MockRenderer = new MockRenderer();
            MockLogger = new MockLogger();
            
            Object = new GameContext(
                MockGame, 
                null!, // SpriteBatch - tests shouldn't use it directly if abstracted
                null!, // Texture2D
                null!, // Font
                MockInput, 
                MockRenderer, 
                MockLogger
            );
            
            // Factory
            Object.StateFactory = new StateFactory(Object, new GameStateManager());
        }
    }
}
