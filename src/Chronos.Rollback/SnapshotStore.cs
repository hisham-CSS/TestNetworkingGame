using System.Collections.Generic;
using Chronos.Core;

namespace Chronos.Rollback
{
    /// <summary>
    /// Manages the history of game states (snapshots).
    /// Handles storage, retrieval, and pruning of old snapshots.
    /// </summary>
    /// <typeparam name="TState">The type of game state.</typeparam>
    public class SnapshotStore<TState> where TState : IGameState
    {
        private readonly Dictionary<int, TState> _buffer = new Dictionary<int, TState>();
        private readonly int _maxHistoryFrames;

        public SnapshotStore(int maxHistoryFrames)
        {
            _maxHistoryFrames = maxHistoryFrames;
        }

        /// <summary>
        /// Saves a snapshot of the current world state for the given frame.
        /// Also prunes old snapshots to maintain the maximum history size.
        /// </summary>
        public void Save(int frame, TState state)
        {
            _buffer[frame] = state;
            Prune(frame);
        }

        /// <summary>
        /// Attempts to retrieve a stored snapshot for a specific frame.
        /// </summary>
        public bool TryGet(int frame, out TState snapshot)
        {
            if (_buffer.TryGetValue(frame, out var s)) { snapshot = s; return true; }
            snapshot = default!;
            return false;
        }

        /// <summary>
        /// Checks if a snapshot exists for the given frame.
        /// </summary>
        public bool Has(int frame)
        {
            return _buffer.ContainsKey(frame);
        }

        /// <summary>
        /// Clears all stored snapshots.
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }

        /// <summary>
        /// Returns the frame index of the oldest snapshot currently stored.
        /// Returns -1 if the store is empty.
        /// </summary>
        public int GetOldestFrame()
        {
            int oldest = -1;
            foreach (var k in _buffer.Keys)
            {
                if (oldest == -1 || k < oldest) oldest = k;
            }
            return oldest;
        }

        private void Prune(int currentFrame)
        {
            int oldestFrameToKeep = currentFrame - _maxHistoryFrames;
            // Prune specific frame to maintain buffer size
            if (_buffer.ContainsKey(oldestFrameToKeep - 1))
            {
                _buffer.Remove(oldestFrameToKeep - 1);
            }
        }
    }
}
