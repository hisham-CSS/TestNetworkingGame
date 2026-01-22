using System;
using System.IO;

namespace Chronos.Net.Protocol
{
    public enum RelayPacketType : byte
    {
        JoinSession = 1,
        LeaveSession = 2,
        RelayPacket = 3,
        JoinSessionAck = 4
    }

    public struct RelayHeader
    {
        public RelayPacketType PacketType;
        public ushort SessionId;
        public byte SourcePlayerId; // 0-3 for assigned players, or random 10-250 for handshake

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)PacketType);
            writer.Write(SessionId);
            writer.Write(SourcePlayerId);
        }

        public static RelayHeader Deserialize(BinaryReader reader)
        {
            return new RelayHeader
            {
                PacketType = (RelayPacketType)reader.ReadByte(),
                SessionId = reader.ReadUInt16(),
                SourcePlayerId = reader.ReadByte()
            };
        }
    }
}
