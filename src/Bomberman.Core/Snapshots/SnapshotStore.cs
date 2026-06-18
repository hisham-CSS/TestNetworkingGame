using System.Collections.Generic;

namespace Bomberman.Core
{
    /// <summary>
    /// A bounded ring of recent <see cref="GameStateSnapshot"/>s keyed by frame. Lockstep stores one
    /// snapshot per simulated frame so that, when a peer's hash for frame F arrives, we can answer
    /// "what was MY state (and hash) at F?" - the lookup desync detection needs. The same buffer is
    /// what Week 5 rewinds to for rollback.
    ///
    /// Capacity is fixed (default 128 frames, ~2 s at 60 Hz): old frames fall off so memory stays flat.
    /// </summary>
    public sealed class SnapshotStore
    {
        private readonly Dictionary<int, GameStateSnapshot> _byFrame = new();
        private readonly Queue<int> _order = new();
        private readonly int _capacity;

        public SnapshotStore(int capacity = 128) { _capacity = capacity; }

        public int Count => _byFrame.Count;
        public int OldestFrame { get; private set; } = -1;
        public int NewestFrame { get; private set; } = -1;

        /// <summary>Store (or overwrite) the snapshot for its frame, evicting the oldest if full.</summary>
        public void Store(GameStateSnapshot snap)
        {
            int f = snap.Frame;
            if (!_byFrame.ContainsKey(f)) _order.Enqueue(f);
            _byFrame[f] = snap;

            while (_order.Count > _capacity)
            {
                int old = _order.Dequeue();
                _byFrame.Remove(old);
            }

            OldestFrame = _order.Count > 0 ? _order.Peek() : -1;
            if (f > NewestFrame) NewestFrame = f;
        }

        public bool TryGet(int frame, out GameStateSnapshot snap)
        {
            if (_byFrame.TryGetValue(frame, out var s)) { snap = s; return true; }
            snap = null!;
            return false;
        }

        /// <summary>The stored hash for a frame, or null if that frame is no longer buffered.</summary>
        public int? HashAt(int frame) => _byFrame.TryGetValue(frame, out var s) ? s.Hash : (int?)null;

        public void Clear() { _byFrame.Clear(); _order.Clear(); OldestFrame = NewestFrame = -1; }
    }
}
