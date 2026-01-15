using System;
using System.Collections.Generic;
using Bomberman.Core.Game;
using Bomberman.Rollback;
using Bomberman.Core;

namespace Bomberman.Rollback
{
    /// <summary>
    /// Manages the history of game states (snapshots).
    /// Handles storage, retrieval, and pruning of old snapshots.
    /// </summary>
    public class SnapshotStore
    {
        private readonly Dictionary<int, GameStateSnapshot> _buffer = new Dictionary<int, GameStateSnapshot>();
        private readonly int _maxHistoryFrames;

        public SnapshotStore(int maxHistoryFrames)
        {
            _maxHistoryFrames = maxHistoryFrames;
        }

        /// <summary>
        /// Saves a snapshot of the current world state for the given frame.
        /// Also prunes old snapshots to maintain the maximum history size.
        /// </summary>
        public void Save(int frame, World world)
        {
            _buffer[frame] = new GameStateSnapshot(frame, world);
            Prune(frame);
        }

        /// <summary>
        /// Attempts to retrieve a stored snapshot for a specific frame.
        /// </summary>
        public bool TryGet(int frame, out GameStateSnapshot snapshot)
        {
            return _buffer.TryGetValue(frame, out snapshot);
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
