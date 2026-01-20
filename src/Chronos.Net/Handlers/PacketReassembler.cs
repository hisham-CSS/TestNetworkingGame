using System;
using System.Collections.Generic;
using System.Net;

namespace Chronos.Net
{
    public class PacketReassembler
    {
        private Dictionary<IPEndPoint, (Dictionary<int, byte[]> Chunks, int TotalChunks)> _reassemblyBuffers = new Dictionary<IPEndPoint, (Dictionary<int, byte[]>, int)>();

        /// <summary>
        /// Processes a StateChunk packet. Buffer fragments until all chunks for a frame are received.
        /// </summary>
        /// <param name="sender">The endpoint triggering the reassembly.</param>
        /// <param name="chunkIndex">Index of the current chunk.</param>
        /// <param name="totalChunks">Total expected chunks.</param>
        /// <param name="chunkData">Raw byte data of the chunk.</param>
        /// <param name="onComplete">Callback invoked with the full reassembled byte array when complete.</param>
        public void HandleChunk(IPEndPoint sender, int chunkIndex, int totalChunks, byte[] chunkData, Action<byte[]> onComplete)
        {
            if (!_reassemblyBuffers.ContainsKey(sender))
            {
                _reassemblyBuffers[sender] = (new Dictionary<int, byte[]>(), totalChunks);
            }

            var buffer = _reassemblyBuffers[sender];
            
            // If TotalChunks changed, it implies a new sync sequence or stale data; reset buffer.
            if (buffer.TotalChunks != totalChunks)
            {
                _reassemblyBuffers[sender] = (new Dictionary<int, byte[]>(), totalChunks);
                buffer = _reassemblyBuffers[sender];
            }

            if (!buffer.Chunks.ContainsKey(chunkIndex))
            {
                // Clone array to avoid reference issues if underlying buffer is reused (rare in C# UDP but good practice)
                byte[] safeData = new byte[chunkData.Length];
                Array.Copy(chunkData, safeData, chunkData.Length);
                buffer.Chunks[chunkIndex] = safeData;
            }

            // Check Completion
            if (buffer.Chunks.Count == totalChunks)
            {
                Console.WriteLine($"[CHRONOS.NET] StateSync Reassembled ({totalChunks} chunks) from {sender}");
                
                // Merge
                int totalBytes = 0;
                for(int i=0; i<totalChunks; i++) 
                {
                     if (buffer.Chunks.ContainsKey(i)) totalBytes += buffer.Chunks[i].Length;
                     else return; // Missing chunk?
                }
                
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
