using System;
using System.Collections.Generic;
using System.Net;

namespace Bomberman.Net
{
    /// <summary>
    /// Reassembles a snapshot that was split across several <c>StateChunk</c> datagrams (a single UDP
    /// payload can't hold a large state). Buffers fragments per sender and fires a callback once every
    /// chunk has arrived. Back-ported from Chronos.Net.PacketReassembler.
    /// </summary>
    public class PacketReassembler
    {
        private Dictionary<IPEndPoint, (Dictionary<int, byte[]> Chunks, int TotalChunks)> _reassemblyBuffers
            = new Dictionary<IPEndPoint, (Dictionary<int, byte[]>, int)>();

        public void HandleChunk(IPEndPoint sender, int chunkIndex, int totalChunks, byte[] chunkData, Action<byte[]> onComplete)
        {
            if (!_reassemblyBuffers.ContainsKey(sender))
            {
                _reassemblyBuffers[sender] = (new Dictionary<int, byte[]>(), totalChunks);
            }

            var buffer = _reassemblyBuffers[sender];

            // A different TotalChunks means a new sync sequence began; discard the stale partial.
            if (buffer.TotalChunks != totalChunks)
            {
                _reassemblyBuffers[sender] = (new Dictionary<int, byte[]>(), totalChunks);
                buffer = _reassemblyBuffers[sender];
            }

            if (!buffer.Chunks.ContainsKey(chunkIndex))
            {
                byte[] safeData = new byte[chunkData.Length];
                Array.Copy(chunkData, safeData, chunkData.Length);
                buffer.Chunks[chunkIndex] = safeData;
            }

            if (buffer.Chunks.Count == totalChunks)
            {
                int totalBytes = 0;
                for (int i = 0; i < totalChunks; i++)
                {
                    if (buffer.Chunks.ContainsKey(i)) totalBytes += buffer.Chunks[i].Length;
                    else return; // a gap remains; wait for more
                }

                byte[] fullSnapshot = new byte[totalBytes];
                int offset = 0;
                for (int i = 0; i < totalChunks; i++)
                {
                    byte[] c = buffer.Chunks[i];
                    Array.Copy(c, 0, fullSnapshot, offset, c.Length);
                    offset += c.Length;
                }

                _reassemblyBuffers.Remove(sender);
                onComplete?.Invoke(fullSnapshot);
            }
        }
    }
}
