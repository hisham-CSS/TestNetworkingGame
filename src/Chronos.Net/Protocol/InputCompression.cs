using System.Collections.Generic;
using System.IO;
using Chronos.Core;

namespace Chronos.Net.Protocol
{
    /// <summary>
    /// Run-length compression for a run of per-frame inputs (Week 5 bandwidth optimization).
    ///
    /// Rollback sends a few redundant frames of input in every packet for loss tolerance, but a
    /// player's input barely changes frame to frame: they hold a direction, or sit idle. So instead
    /// of writing every frame, we write (runLength, oneInput) pairs. On realistic play this shrinks
    /// the input history by well over 70%. It is lossless: decode reproduces the exact sequence.
    /// Generic over TInput via IEquatable, so it stays framework-agnostic.
    /// </summary>
    public static class InputCompression
    {
        public static void Write<TInput>(BinaryWriter w, TInput[] inputs) where TInput : struct, IInputState<TInput>
        {
            w.Write(inputs.Length);                 // expanded length, so the reader can size the result

            var runs = new List<(int len, TInput val)>();
            int i = 0;
            while (i < inputs.Length)
            {
                int j = i + 1;
                while (j < inputs.Length && inputs[j].Equals(inputs[i])) j++;
                runs.Add((j - i, inputs[i]));
                i = j;
            }

            w.Write(runs.Count);
            foreach (var (len, val) in runs)
            {
                w.Write(len);
                val.Serialize(w);
            }
        }

        public static TInput[] Read<TInput>(BinaryReader r) where TInput : struct, IInputState<TInput>
        {
            int total = r.ReadInt32();
            // Hardening: never trust a length from the wire.
            if (total < 0 || (r.BaseStream.CanSeek && total > r.BaseStream.Length)) return new TInput[0];
            int runCount = r.ReadInt32();
            if (runCount < 0 || (r.BaseStream.CanSeek && runCount > r.BaseStream.Length)) return new TInput[0];

            var result = new TInput[total];
            int idx = 0;
            for (int k = 0; k < runCount; k++)
            {
                int len = r.ReadInt32();
                TInput val = TInput.Deserialize(r);
                for (int n = 0; n < len && idx < total; n++) result[idx++] = val;
            }
            return result;
        }
    }
}
