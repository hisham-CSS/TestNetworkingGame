using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>A full serialized state snapshot in one packet. Used for late joiners / resync.
    /// The snapshot system itself is fleshed out in Weeks 4-5; the wire format is reserved now.</summary>
    public struct StateSyncPacket : IPacket
    {
        public PacketType Type => PacketType.StateSync;
        public byte[] Data { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Data.Length);
            writer.Write(Data);
        }

        public static StateSyncPacket Deserialize(BinaryReader reader)
        {
            int len = reader.ReadInt32();
            return new StateSyncPacket { Data = reader.ReadBytes(len) };
        }
    }

    /// <summary>One fragment of a snapshot too big for a single datagram. The receiver reassembles
    /// all chunks (see PacketReassembler) before applying.</summary>
    public struct StateChunkPacket : IPacket
    {
        public PacketType Type => PacketType.StateChunk;
        public int Index { get; set; }
        public int TotalChunks { get; set; }
        public byte[] Data { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Index);
            writer.Write(TotalChunks);
            writer.Write(Data.Length);
            writer.Write(Data);
        }

        public static StateChunkPacket Deserialize(BinaryReader reader)
        {
            var p = new StateChunkPacket
            {
                Index = reader.ReadInt32(),
                TotalChunks = reader.ReadInt32()
            };
            int len = reader.ReadInt32();
            p.Data = reader.ReadBytes(len);
            return p;
        }
    }
}
