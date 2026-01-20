using System.IO;
using Chronos.Core;

namespace Chronos.Net.Packets
{
    public struct InputPacket<TInput> : IPacket where TInput : struct, IInputState<TInput>
    {
        public PacketType Type => PacketType.Input;

        public int PlayerId { get; set; }
        public int StartFrame { get; set; }
        public TInput[] Inputs { get; set; }
        // TODO: We need a generic way to represent position/hash if we want to be truly agnostic?
        // Or keep them as primitives.
        // For now, let's keep Hash as int.
        // Position is tricky. IntVector2 is Bomberman specific.
        // We will pass 2 ints for X/Y position as a "Sync Check" proxy.
        public int PosX { get; set; }
        public int PosY { get; set; }
        public int StateHash { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(PlayerId);
            writer.Write(StartFrame);
            
            writer.Write(Inputs.Length);
            for (int i = 0; i < Inputs.Length; i++)
            {
                Inputs[i].Serialize(writer);
            }

            writer.Write(PosX);
            writer.Write(PosY);
            writer.Write(StateHash);
        }

        public static InputPacket<TInput> Deserialize(BinaryReader reader)
        {
            var p = new InputPacket<TInput>();
            p.PlayerId = reader.ReadInt32();
            p.StartFrame = reader.ReadInt32();

            int count = reader.ReadInt32();
            
            // Hardening check: if count claims more bytes than available (assuming min 1 byte per input), abort.
            if (count < 0 || (reader.BaseStream.CanSeek && count > (reader.BaseStream.Length - reader.BaseStream.Position)))
            {
                p.Inputs = new TInput[0];
                return p;
            }

            p.Inputs = new TInput[count];
            for (int i = 0; i < count; i++)
            {
                p.Inputs[i] = TInput.Deserialize(reader);
            }

            p.PosX = reader.ReadInt32();
            p.PosY = reader.ReadInt32();
            p.StateHash = reader.ReadInt32();
            return p;
        }
    }
}
