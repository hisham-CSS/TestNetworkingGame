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

        public void Save(int frame, World world)
        {
            _buffer[frame] = new GameStateSnapshot(frame, world);
            Prune(frame);
        }

        public bool TryGet(int frame, out GameStateSnapshot snapshot)
        {
            return _buffer.TryGetValue(frame, out snapshot);
        }

        public bool Has(int frame)
        {
            return _buffer.ContainsKey(frame);
        }

        public void Clear()
        {
            _buffer.Clear();
        }

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
            // Prune one specifically (as done in original logic) or all older?
            // Original logic: Remove(oldestFrameToKeep - 1)
            if (_buffer.ContainsKey(oldestFrameToKeep - 1))
            {
                _buffer.Remove(oldestFrameToKeep - 1);
            }
            
            // To be safe, we could prune everything older, but keeping it consistent with original for now.
        }
    }
}
