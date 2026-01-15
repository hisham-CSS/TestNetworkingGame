using System;
using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>Provides the client with the current state of the lobby.</summary>
    public struct LobbyUpdatePacket : IPacket
    {
        public PacketType Type => PacketType.LobbyUpdate;
        public int ConnectedCount;
        public int TotalPlayers;
        public int SlotMask;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(ConnectedCount);
            writer.Write(TotalPlayers);
            writer.Write(SlotMask);
        }

        public static LobbyUpdatePacket Deserialize(BinaryReader reader)
        {
            var p = new LobbyUpdatePacket
            {
                ConnectedCount = reader.ReadInt32(),
                TotalPlayers = reader.ReadInt32()
            };
            // Forward compat check
            if (reader.BaseStream.Position < reader.BaseStream.Length)
                p.SlotMask = reader.ReadInt32();
            return p;
        }
    }

    /// <summary>Packet indicating a player's ready status in the lobby.</summary>
    public struct LobbyReadyPacket : IPacket
    {
        public PacketType Type => PacketType.LobbyReady;
        public int PlayerId;
        public bool IsReady;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(PlayerId);
            writer.Write(IsReady);
        }

        public static LobbyReadyPacket Deserialize(BinaryReader reader)
        {
            return new LobbyReadyPacket
            {
                PlayerId = reader.ReadInt32(),
                IsReady = reader.ReadBoolean()
            };
        }
    }

    /// <summary>Command to start the game session.</summary>
    public struct StartGamePacket : IPacket
    {
        public PacketType Type => PacketType.StartGame;
        public int Seed;
        public int TotalPlayers;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Seed);
            writer.Write(TotalPlayers);
        }

        public static StartGamePacket Deserialize(BinaryReader reader)
        {
            return new StartGamePacket
            {
                Seed = reader.ReadInt32(),
                TotalPlayers = reader.ReadInt32()
            };
        }
    }
}
