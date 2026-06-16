using System;

namespace Bomberman
{
    /// <summary>
    /// Fixed-capacity circular (ring) buffer that stores the inputs for one simulation frame
    /// per slot. Capacity is 256 frames; the slot for a frame is (frame &amp; 255), so frames
    /// older than 256 are overwritten automatically and the buffer never grows.
    ///
    /// Why this exists (Week 1): treating a match as "a seed + a stream of per-frame inputs"
    /// is what makes REPLAY (this week) and ROLLBACK (Week 5) possible. If you can record and
    /// re-feed the exact input stream, you can re-create any past frame deterministically.
    ///
    /// Why 256: a power of two lets us index with a cheap bit-mask (frame &amp; 255) instead of a
    /// modulo, and 256 frames is ~4.27 seconds at 60Hz - comfortably larger than any network
    /// delay or rollback window we will need later. For replays longer than 256 frames you would
    /// persist the stream to disk; the ring is the in-memory working set.
    /// </summary>
    public class InputBuffer
    {
        public const int Capacity = 256;      // power of two => index with a mask, not a modulo
        private const int Mask = Capacity - 1; // 255 == 0xFF

        // One InputState[] per stored frame (the array holds one entry per player).
        private readonly InputState[][] _frames = new InputState[Capacity][];
        // Which absolute frame number currently lives in each slot (-1 == empty).
        private readonly int[] _frameNumbers = new int[Capacity];

        /// <summary>Highest frame number recorded so far (-1 before anything is recorded).</summary>
        public int LatestFrame { get; private set; } = -1;

        public InputBuffer()
        {
            for (int i = 0; i < Capacity; i++)
                _frameNumbers[i] = -1;
        }

        /// <summary>
        /// Record the inputs for <paramref name="frame"/>, overwriting whatever frame previously
        /// occupied this slot (the frame from 256 ticks ago). The array is cloned so later mutation
        /// of the caller's array cannot corrupt the stored snapshot - structs are value types, but
        /// the array itself is a reference.
        /// </summary>
        public void Record(int frame, InputState[] inputs)
        {
            int slot = frame & Mask;
            _frames[slot] = (InputState[])inputs.Clone();
            _frameNumbers[slot] = frame;
            if (frame > LatestFrame) LatestFrame = frame;
        }

        /// <summary>
        /// True if the buffer still holds inputs for <paramref name="frame"/> (i.e. it has not yet
        /// been overwritten by a frame 256 ticks newer). This is the guard that keeps replay correct.
        /// </summary>
        public bool TryGet(int frame, out InputState[] inputs)
        {
            int slot = frame & Mask;
            if (frame >= 0 && _frameNumbers[slot] == frame && _frames[slot] != null)
            {
                inputs = _frames[slot];
                return true;
            }
            inputs = Array.Empty<InputState>();
            return false;
        }

        /// <summary>Convenience accessor; returns an empty array if the frame is not available.</summary>
        public InputState[] Get(int frame)
        {
            return TryGet(frame, out var inputs) ? inputs : Array.Empty<InputState>();
        }

        /// <summary>Forget all recorded inputs (e.g. when starting a new match or replay).</summary>
        public void Reset()
        {
            for (int i = 0; i < Capacity; i++)
            {
                _frames[i] = null;
                _frameNumbers[i] = -1;
            }
            LatestFrame = -1;
        }
    }
}
