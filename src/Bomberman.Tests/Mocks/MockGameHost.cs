using Bomberman.App.GameHost;

namespace Bomberman.Tests.Mocks
{
    public class MockGameHost : IGameHost
    {
        public bool ExitCalled { get; private set; }
        public int WindowWidth { get; set; } = 800;
        public int WindowHeight { get; set; } = 600;

        public void Exit()
        {
            ExitCalled = true;
        }
    }
}
