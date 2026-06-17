using System;
using Microsoft.Xna.Framework;

namespace Bomberman
{
    /// <summary>
    /// Produces a deterministic integer hash ("checksum") of the whole World. Two simulations
    /// that started from the same seed and were fed the same inputs MUST produce the same hash
    /// every frame. We use this to VERIFY determinism: record a session's inputs, replay them,
    /// and compare the hash sequences. A single mismatch pinpoints the exact frame state diverged.
    ///
    /// Algorithm: Jenkins "one-at-a-time" hash. It is cheap, order-sensitive, and avalanches well
    /// enough to catch the small state differences (a position off by one, a bomb timer off by a
    /// frame) that cause desyncs. In Week 4 the same idea scales up to snapshot-based desync
    /// detection over the network.
    ///
    /// DETERMINISM CAVEAT (taught in lecture): positions are floats (Vector2) at this milestone.
    /// We hash their raw 32-bit pattern via BitConverter, which is perfectly reproducible on a
    /// SINGLE machine - enough to verify replay determinism this week. Across different
    /// machines/compilers, float results can differ; eliminating that is a Week 3+ topic (the real
    /// Chronos codebase later switches positions to integers for exactly this reason).
    /// </summary>
    public static class StateHasher
    {
        public static int Hash(World world)
        {
            // TODO (LA1 - Determinism): produce a deterministic hash of the whole World.
            //  Mix in (via Combine, provided below): players (id, alive, range, capacity, position bits),
            //  bombs (owner, timer, position bits), destructible tiles' Destroyed flags, powerups,
            //  and the explosion count. Return Finalize(hash). Same inputs + seed must give the same hash.
            return 0;
        }

        /// <summary>Mix in the bit-pattern of an entity's Transform position, if it has one.</summary>
        private static int CombinePosition(int hash, World world, Entity entity)
        {
            var transformEntities = world.Transforms.GetEntities();
            var transforms = world.Transforms.GetAll();
            for (int t = 0; t < transformEntities.Count; t++)
            {
                if (transformEntities[t].Equals(entity))
                {
                    Vector2 pos = transforms[t].Position;
                    hash = Combine(hash, BitConverter.SingleToInt32Bits(pos.X));
                    hash = Combine(hash, BitConverter.SingleToInt32Bits(pos.Y));
                    break;
                }
            }
            return hash;
        }

        // Jenkins one-at-a-time: per-value mix.
        private static int Combine(int hash, int value)
        {
            unchecked
            {
                hash += value;
                hash += (hash << 10);
                hash ^= (hash >> 6);
                return hash;
            }
        }

        // Jenkins one-at-a-time: final avalanche.
        private static int Finalize(int hash)
        {
            unchecked
            {
                hash += (hash << 3);
                hash ^= (hash >> 11);
                hash += (hash << 15);
                return hash;
            }
        }
    }
}
