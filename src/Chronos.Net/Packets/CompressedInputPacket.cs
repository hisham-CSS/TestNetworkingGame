using System.IO;
using Chronos.Core;
using Chronos.Net.Protocol;

namespace Chronos.Net.Packets
{
    /// <summary>
    /// Week 5: the same payload as <see cref="InputPacket{TInput}"/>, but the input history is
    /// run-length compressed (see <see cref="InputCompression"/>). This is the packet rollback sends on
    /// the wire; the uncompressed InputPacket is kept as the teaching "before".
    /// </summary>
    public struct CompressedInputPacket<TInput> : IPacket where TInput : struct, IInputState<TInput>
    {
        public PacketType Type => PacketType.InputCompressed;

        public int PlayerId { get; set; }
        public int StartFrame { get; set; }
        public TInput[] Inputs { get; set; }
        public int PosX { get; set; }
        public int PosY { get; set; }
        public int StateHash { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(PlayerId);
            writer.Write(StartFrame);
            InputCompression.Write(writer, Inputs);   // <-- the compression
            writer.Write(PosX);
            writer.Write(PosY);
            writer.Write(StateHash);
        }

        public static CompressedInputPacket<TInput> Deserialize(BinaryReader reader)
        {
            var p = new CompressedInputPacket<TInput>();
            p.PlayerId = reader.ReadInt32();
            p.StartFrame = reader.ReadInt32();
            p.Inputs = InputCompression.Read<TInput>(reader);
            p.PosX = reader.ReadInt32();
            p.PosY = reader.ReadInt32();
            p.StateHash = reader.ReadInt32();
            return p;
        }
    }
}
