namespace Chronos.Core;

/// <summary>
/// Represents the deterministic game simulation that Chronos will drive.
/// </summary>
/// <typeparam name="TInput">The type of input struct.</typeparam>
/// <typeparam name="TState">The type of state snapshot.</typeparam>
public interface IGameSimulation<TInput, TState> 
    where TInput : struct, IInputState<TInput>
    where TState : IGameState
{
    /// <summary>
    /// Advances the simulation by one step using the provided inputs.
    /// </summary>
    /// <param name="inputs">Array of inputs, one for each player ID.</param>
    /// <param name="dt">Time delta (usually fixed).</param>
    void Update(TInput[] inputs, float dt);

    /// <summary>
    /// Captures the current state of the world.
    /// </summary>
    /// <returns>A snapshot of the world.</returns>
    TState CaptureState();

    /// <summary>
    /// Restores the world to a previous state.
    /// </summary>
    /// <param name="state">The state to restore.</param>
    void RestoreState(TState state);
}
