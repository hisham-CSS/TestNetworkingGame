using System;
using System.Text;
using Microsoft.Xna.Framework;

namespace Bomberman.Core
{
    /// <summary>
    /// Self-contained, headless proof that the simulation is deterministic and that the
    /// record-then-replay loop reproduces state exactly. No graphics or keyboard required, so it
    /// can run at startup (see Game1.Initialize) or be lifted verbatim into a unit test.
    ///
    /// Two independent checks:
    ///   (A) Replay determinism - run the sim while RECORDING inputs into a 256-frame InputBuffer,
    ///       then spin up a FRESH sim from the same seed, re-feed the recorded stream, and confirm
    ///       the per-frame StateHasher values match frame-for-frame.
    ///   (B) Cross-run determinism - run two fresh sims with the same scripted inputs and confirm
    ///       identical hashes, proving the simulation has no hidden nondeterminism (clocks, RNG, etc).
    /// </summary>
    public static class DeterminismHarness
    {
        // Stay strictly below InputBuffer.Capacity (256) so the ring never overwrites a frame
        // we still need for the replay comparison.
        public const int DefaultFrames = 240;
        private const float Dt = 1f / 60f;

        /// <summary>
        /// A fixed, frame-driven input script. Pure function of the frame number, so it is itself
        /// perfectly reproducible - the inputs can never be the source of a divergence.
        /// </summary>
        public static InputState[] ScriptedInput(int frame)
        {
            Vector2 move = Vector2.Zero;
            switch ((frame / 20) % 4)
            {
                case 0: move = new Vector2(1, 0); break;
                case 1: move = new Vector2(0, 1); break;
                case 2: move = new Vector2(-1, 0); break;
                case 3: move = new Vector2(0, -1); break;
            }
            bool placeBomb = (frame % 50) == 0; // drop a bomb every 50 frames
            return new[] { new InputState { Movement = move, PlaceBomb = placeBomb } };
        }

        public static bool Verify(int seed, int frames, out string report)
        {
            var sb = new StringBuilder();
            bool ok = true;

            // ----- (A) Record a live run, then replay it from a fresh world -----
            var recordSim = new Simulation(seed);
            var buffer = new InputBuffer();
            var recordedHashes = new int[frames];

            for (int f = 0; f < frames; f++)
            {
                var inputs = ScriptedInput(f);
                buffer.Record(f, inputs);              // <-- the input buffer in action
                recordSim.Update(inputs, Dt);
                recordedHashes[f] = StateHasher.Hash(recordSim.World);
            }

            var replaySim = new Simulation(seed);      // same seed => same starting map
            int replayDivergedAt = -1;
            for (int f = 0; f < frames; f++)
            {
                if (!buffer.TryGet(f, out var inputs))
                {
                    replayDivergedAt = f;
                    sb.AppendLine($"[A] FAIL: frame {f} was already overwritten in the ring buffer.");
                    ok = false;
                    break;
                }
                replaySim.Update(inputs, Dt);
                int h = StateHasher.Hash(replaySim.World);
                if (h != recordedHashes[f])
                {
                    replayDivergedAt = f;
                    sb.AppendLine($"[A] FAIL: replay diverged at frame {f} " +
                                  $"(record=0x{recordedHashes[f]:X8} replay=0x{h:X8}).");
                    ok = false;
                    break;
                }
            }
            if (replayDivergedAt == -1)
                sb.AppendLine($"[A] PASS: {frames} frames replayed identically " +
                              $"(final hash 0x{recordedHashes[frames - 1]:X8}).");

            // ----- (B) Two independent fresh runs must agree -----
            var simX = new Simulation(seed);
            var simY = new Simulation(seed);
            int crossDivergedAt = -1;
            for (int f = 0; f < frames; f++)
            {
                var inputs = ScriptedInput(f);
                simX.Update(inputs, Dt);
                simY.Update(inputs, Dt);
                if (StateHasher.Hash(simX.World) != StateHasher.Hash(simY.World))
                {
                    crossDivergedAt = f;
                    sb.AppendLine($"[B] FAIL: two runs diverged at frame {f}.");
                    ok = false;
                    break;
                }
            }
            if (crossDivergedAt == -1)
                sb.AppendLine($"[B] PASS: two independent runs stayed bit-identical for {frames} frames.");

            sb.Insert(0, ok ? "DETERMINISM CHECK: PASS\n" : "DETERMINISM CHECK: FAIL\n");
            report = sb.ToString();
            return ok;
        }

        public static bool Verify(int seed, out string report) => Verify(seed, DefaultFrames, out report);
    }
}
