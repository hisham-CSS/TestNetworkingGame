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
    /// Advances the simulation by one fixed time step using the provided inputs.
    /// This method must be deterministic: given the same state and inputs, it must produce the exact same result.
    /// </summary>
    /// <param name="inputs">Array of inputs, indexed by player ID.</param>
    /// <param name="dt">The fixed time delta for this step.</param>
    void Update(TInput[] inputs, float dt);

    /// <summary>
    /// Captures the current state of the world into a serializable snapshot.
    /// </summary>
    /// <returns>A snapshot object representing the current frame.</returns>
    TState CaptureState();

    /// <summary>
    /// Restores the world to a previous state using the provided snapshot.
    /// </summary>
    /// <param name="state">The state snapshot to restore.</param>
    void RestoreState(TState state);
}
