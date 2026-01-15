namespace Bomberman.App.GameHost
{
    public interface IGameHost
    {
        void Exit();
        int WindowWidth { get; }
        int WindowHeight { get; }
    }
}
