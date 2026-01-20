namespace Chronos.Core;

/// <summary>
/// Marker interface for game state snapshots.
/// Implementations must be serializable and contain all necessary data to completely restore a frame.
/// </summary>
public interface IGameState
{
}
