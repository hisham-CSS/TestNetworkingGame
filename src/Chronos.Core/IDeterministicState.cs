namespace Chronos.Core;

public interface IDeterministicState : IGameState
{
    int CalculateHash();
}
