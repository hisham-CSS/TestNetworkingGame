using System;

namespace Bomberman.Core.Game
{
    /// <summary>
    /// A lightweight, deterministic pseudo-random number generator (Xorshift32).
    /// </summary>
    public class DeterministicRandom
    {
        private uint _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeterministicRandom"/> class.
        /// </summary>
        /// <param name="seed">The initial seed (must be non-zero, automatically corrected if 0).</param>
        public DeterministicRandom(int seed)
        {
            // Ensure non-zero seed
            if (seed == 0) seed = 123456789;
            _state = (uint)seed;
        }

        /// <summary>
        /// Gets or sets the internal state of the RNG.
        /// Required for state rollback/serialization.
        /// </summary>
        public uint State
        {
            get => _state;
            set => _state = value;
        }

        /// <summary>
        /// Generates the next random integer.
        /// </summary>
        /// <returns>A positive integer.</returns>
        public int Next()
        {
            // Xorshift32 algorithm
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (int)(x & 0x7FFFFFFF); // Return positive int
        }

        /// <summary>
        /// Generates a random integer strictly less than the specified maximum.
        /// </summary>
        /// <param name="max"> The exclusive upper bound.</param>
        public int Next(int max)
        {
            if (max <= 0) return 0;
            return Next() % max;
        }
        
        /// <summary>
        /// Generates a random integer within a specified range.
        /// </summary>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The exclusive upper bound.</param>
        public int Next(int min, int max)
        {
            if (max <= min) return min;
            return min + (Next() % (max - min));
        }

        /// <summary>
        /// Generates a random floating-point number between 0.0 and 1.0.
        /// </summary>
        public double NextDouble()
        {
            return (double)Next() / int.MaxValue;
        }
    }
}
