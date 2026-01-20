namespace Chronos.Core;

/// <summary>
/// Represents a game state that supports deterministic hashing.
/// This is used for verifying synchronization between clients.
/// </summary>
public interface IDeterministicState : IGameState
{
    /// <summary>
    /// Calculates a deterministic hash of the entire game state.
    /// This hash must be identical for identical states across different machines.
    /// </summary>
    /// <returns>A hash value representing the state.</returns>
    int CalculateHash();
}
