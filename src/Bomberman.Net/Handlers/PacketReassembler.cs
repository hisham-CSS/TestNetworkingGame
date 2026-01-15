using System;
using System.Collections.Generic;
using System.Net;

namespace Bomberman.Net.Handlers
{
    public class PacketReassembler
    {
        private Dictionary<IPEndPoint, (Dictionary<int, byte[]> Chunks, int TotalChunks)> _reassemblyBuffers = new Dictionary<IPEndPoint, (Dictionary<int, byte[]>, int)>();

        public void HandleChunk(IPEndPoint sender, int chunkIndex, int totalChunks, byte[] chunkData, Action<byte[]> onComplete)
        {
            if (!_reassemblyBuffers.ContainsKey(sender))
            {
                _reassemblyBuffers[sender] = (new Dictionary<int, byte[]>(), totalChunks);
            }

            var buffer = _reassemblyBuffers[sender];
            
            // Validation: If TotalChunks changed, maybe reset?
            if (buffer.TotalChunks != totalChunks)
            {
                // Stale or new sync started? Reset.
                _reassemblyBuffers[sender] = (new Dictionary<int, byte[]>(), totalChunks);
                buffer = _reassemblyBuffers[sender];
            }

            if (!buffer.Chunks.ContainsKey(chunkIndex))
            {
                buffer.Chunks[chunkIndex] = chunkData;
            }

            // Check Completion
            if (buffer.Chunks.Count == totalChunks)
            {
                Console.WriteLine($"[Network] StateSync Reassembled ({totalChunks} chunks) from {sender}");
                
                // Merge
                int totalBytes = 0;
                for(int i=0; i<totalChunks; i++) totalBytes += buffer.Chunks[i].Length;
                
                byte[] fullSnapshot = new byte[totalBytes];
                int offset = 0;
                for(int i=0; i<totalChunks; i++)
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
