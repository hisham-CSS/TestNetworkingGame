using System;

namespace Bomberman.Core.Game
{
    /// <summary>
    /// A lightweight, deterministic pseudo-random number generator (Xorshift32).
    /// </summary>
    public class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            // Ensure non-zero seed
            if (seed == 0) seed = 123456789;
            _state = (uint)seed;
        }

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

        public int Next(int max)
        {
            if (max <= 0) return 0;
            return Next() % max;
        }
        
        public int Next(int min, int max)
        {
            if (max <= min) return min;
            return min + (Next() % (max - min));
        }

        public double NextDouble()
        {
            return (double)Next() / int.MaxValue;
        }
    }
}
