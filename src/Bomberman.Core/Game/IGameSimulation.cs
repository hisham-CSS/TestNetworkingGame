namespace Bomberman.Core
{
    /// <summary>
    /// The deterministic simulation boundary that the engine — and, from Week 3, the Chronos
    /// networking layer — drives. Week 2 introduces the Update boundary only. Weeks 4-5 extend
    /// this interface with CaptureState / RestoreState for snapshots and rollback:
    ///     TState CaptureState();
    ///     void   RestoreState(TState state);
    /// </summary>
    public interface IGameSimulation<TInput> where TInput : struct, IInputState<TInput>
    {
        /// <summary>Advance the simulation by one fixed step using one input per player.</summary>
        void Update(TInput[] inputs, float dt);
    }
}
