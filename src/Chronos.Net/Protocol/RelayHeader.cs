using System;
using System.IO;

namespace Chronos.Net.Protocol
{
    /// <summary>
    /// Types of packets exchanged between Relay Client and Server.
    /// </summary>
    public enum RelayPacketType : byte
    {
        /// <summary>Client requesting to join a session.</summary>
        JoinSession = 1,
        /// <summary>Client leaving a session.</summary>
        LeaveSession = 2,
        /// <summary>Wraps a game packet to be forwarded to other clients.</summary>
        RelayPacket = 3,
        /// <summary>Server acknowledging a successful JoinSession request.</summary>
        JoinSessionAck = 4
    }

    /// <summary>
    /// Header prefixed to all packets sent via the Relay Transport.
    /// Handles routing and session management logic.
    /// </summary>
    public struct RelayHeader
    {
        public RelayPacketType PacketType;
        public ushort SessionId;
        public byte SourcePlayerId; // 0-3 for assigned players, or random 10-250 for handshake

        /// <summary>
        /// Serializes the header into the provided BinaryWriter.
        /// </summary>
        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)PacketType);
            writer.Write(SessionId);
            writer.Write(SourcePlayerId);
        }

        /// <summary>
        /// Deserializes a RelayHeader from the provided BinaryReader.
        /// </summary>
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
