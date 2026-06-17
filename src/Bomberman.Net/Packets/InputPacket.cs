using System.IO;
using Bomberman.Core;

namespace Bomberman.Net.Packets
{
    /// <summary>
    /// The packet that carries gameplay. In lockstep we send INPUTS, not state: a run of one or more
    /// per-frame <typeparamref name="TInput"/> values starting at <see cref="StartFrame"/>. Sending a
    /// short history (not just the newest frame) is our packet-loss insurance — one received packet
    /// can fill several missing frames. PosX/PosY/StateHash ride along as a sync-check proxy that
    /// Week 4 will use for desync detection.
    /// </summary>
    public struct InputPacket<TInput> : IPacket where TInput : struct, IInputState<TInput>
    {
        public PacketType Type => PacketType.Input;

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

            // Hardening: never trust a length from the wire. If the claimed count exceeds the bytes
            // actually available, bail out with an empty history instead of allocating wildly.
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
