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
            // TODO (LA2 - Protocol): write this packet to the wire.
            //  1. Write the leading type byte:           writer.Write((byte)Type);
            //  2. Write PlayerId, then StartFrame (ints).
            //  3. Write the input count (Inputs.Length), then each input: Inputs[i].Serialize(writer);
            //  4. Write PosX, PosY, StateHash (ints).
            //  Read them back in the SAME order in Deserialize.
            throw new System.NotImplementedException("LA2: implement InputPacket.Serialize");
        }

        public static InputPacket<TInput> Deserialize(BinaryReader reader)
        {
            // TODO (LA2 - Protocol): read the packet back in the SAME order Serialize wrote it.
            //  - Read PlayerId, StartFrame (ints), then the input count (int).
            //  - HARDENING: if count < 0, or count is larger than the bytes remaining in the stream,
            //    return a packet with an empty Inputs array instead of allocating (reject bad input).
            //  - Otherwise read 'count' inputs with TInput.Deserialize(reader).
            //  - Read PosX, PosY, StateHash (ints). Return the populated packet.
            throw new System.NotImplementedException("LA2: implement InputPacket.Deserialize");
        }
    }
}
