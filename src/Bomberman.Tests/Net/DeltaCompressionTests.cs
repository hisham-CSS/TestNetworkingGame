using System.IO;
using Bomberman.Core;
using Bomberman.Core.Input;
using Chronos.Net.Protocol;
using NUnit.Framework;

namespace Bomberman.Tests.Net
{
    /// <summary>Run-length input compression (Week 5 bandwidth optimization).</summary>
    public class DeltaCompressionTests
    {
        private static int RawSize(InputState[] history)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(history.Length);
            foreach (var i in history) i.Serialize(w);   // every frame written out (the Week 3 format)
            return (int)ms.Length;
        }

        private static int CompressedSize(InputState[] history)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            InputCompression.Write(w, history);
            return (int)ms.Length;
        }

        private static InputState[] RoundTrip(InputState[] history)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            InputCompression.Write(w, history);
            ms.Position = 0;
            using var r = new BinaryReader(ms);
            return InputCompression.Read<InputState>(r);
        }

        [Test]
        public void Compression_IsLossless_RoundTrip()
        {
            var history = new InputState[40];
            for (int i = 0; i < 40; i++) history[i] = new InputState { Movement = new IntVector2(1, 0) };
            history[10].PlaceBomb = true;
            history[25] = new InputState { Movement = new IntVector2(0, -1) };

            var back = RoundTrip(history);
            Assert.That(back.Length, Is.EqualTo(history.Length));
            for (int i = 0; i < history.Length; i++)
                Assert.That(back[i], Is.EqualTo(history[i]), $"frame {i} differs");
        }

        [Test]
        public void RealisticHistory_ShrinksByAtLeast70Percent()
        {
            // 64 frames of a player holding Right, with one bomb tap. (Typical rollback redundancy.)
            var history = new InputState[64];
            for (int i = 0; i < 64; i++) history[i] = new InputState { Movement = new IntVector2(1, 0) };
            history[30].PlaceBomb = true;

            int raw = RawSize(history);
            int comp = CompressedSize(history);
            double ratio = (double)comp / raw;

            Assert.That(ratio, Is.LessThan(0.30), $"expected >70% reduction, got raw={raw} comp={comp} ratio={ratio:P0}");
        }

        [Test]
        public void IdleHistory_CompressesToASingleRun()
        {
            var history = new InputState[120];   // 2 seconds of standing still
            int comp = CompressedSize(history);
            int raw = RawSize(history);
            Assert.That(comp, Is.LessThan(raw / 5)); // one run + headers, far under 20%
        }
    }
}
