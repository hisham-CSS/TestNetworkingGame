namespace Bomberman.Core
{
    /// <summary>
    /// The deterministic simulation boundary the engine and the networking layer drive. Week 2
    /// introduced the Update boundary; Week 4 adds the snapshot boundary (CaptureState / RestoreState)
    /// that desync detection and resync need now, and that Week 5 rollback reuses to rewind time.
    /// </summary>
    public interface IGameSimulation<TInput> where TInput : struct, IInputState<TInput>
    {
        /// <summary>Advance the simulation by one fixed step using one input per player.</summary>
        void Update(TInput[] inputs, float dt);

        /// <summary>Capture a complete, restorable copy of the world at the current frame.</summary>
        GameStateSnapshot CaptureState();

        /// <summary>Rewind the world to a previously captured snapshot.</summary>
        void RestoreState(GameStateSnapshot state);
    }
}
