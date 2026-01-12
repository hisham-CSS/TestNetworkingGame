using System;
using System.IO;
using Bomberman.Core;
using Bomberman.Core.Input;

namespace Bomberman.Net.Packets
{
    public struct InputPacket : IPacket
    {
        public PacketType Type => PacketType.Input;
        public int PlayerId;
        public int StartFrame;
        public InputState[] Inputs;
        public IntVector2 CurrentPos;
        public int StateHash;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(PlayerId);
            writer.Write(StartFrame);
            writer.Write(Inputs != null ? Inputs.Length : 0);
            writer.Write(CurrentPos.X);
            writer.Write(CurrentPos.Y);
            writer.Write(StateHash);
            
            if (Inputs != null)
            {
                for (int i = 0; i < Inputs.Length; i++)
                {
                    writer.Write(Inputs[i].Movement.X);
                    writer.Write(Inputs[i].Movement.Y);
                    writer.Write(Inputs[i].PlaceBomb);
                    writer.Write(Inputs[i].BombTarget.X);
                    writer.Write(Inputs[i].BombTarget.Y);
                }
            }
        }

        public static InputPacket Deserialize(BinaryReader reader)
        {
            var p = new InputPacket();
            p.PlayerId = reader.ReadInt32();
            p.StartFrame = reader.ReadInt32();
            int count = reader.ReadInt32();
            
            // Hardening check inline
            long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (count < 0 || count > 60 || remaining < count * 17)
            {
                p.Inputs = new InputState[0];
                return p; 
            }

            p.CurrentPos = new IntVector2(reader.ReadInt32(), reader.ReadInt32());
            p.StateHash = reader.ReadInt32();

            p.Inputs = new InputState[count];
            for(int i=0; i<count; i++)
            {
                p.Inputs[i].Movement.X = reader.ReadInt32();
                p.Inputs[i].Movement.Y = reader.ReadInt32();
                p.Inputs[i].PlaceBomb = reader.ReadBoolean();
                p.Inputs[i].BombTarget = new IntVector2(reader.ReadInt32(), reader.ReadInt32());
            }

            return p;
        }
    }

    public struct StateSyncPacket : IPacket
    {
        public PacketType Type => PacketType.StateSync;
        public byte[] Data;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Data != null ? Data.Length : 0);
            if (Data != null) writer.Write(Data);
        }

        public static StateSyncPacket Deserialize(BinaryReader reader)
        {
            int len = reader.ReadInt32();
            return new StateSyncPacket { Data = reader.ReadBytes(len) };
        }
    }

    public struct StateChunkPacket : IPacket
    {
        public PacketType Type => PacketType.StateChunk;
        public int Index;
        public int TotalChunks;
        public byte[] Data;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Index);
            writer.Write(TotalChunks);
            writer.Write(Data != null ? Data.Length : 0);
            if (Data != null) writer.Write(Data);
        }

        public static StateChunkPacket Deserialize(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            int total = reader.ReadInt32();
            int len = reader.ReadInt32();
            return new StateChunkPacket 
            {
                Index = index,
                TotalChunks = total,
                Data = reader.ReadBytes(len)
            };
        }
    }
}
