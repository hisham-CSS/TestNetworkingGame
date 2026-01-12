using System;
using System.IO;

namespace Bomberman.Net.Packets
{
    public struct JoinRequestPacket : IPacket
    {
        public PacketType Type => PacketType.JoinRequest;
        public int Version;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Version);
        }

        public static JoinRequestPacket Deserialize(BinaryReader reader)
        {
            return new JoinRequestPacket { Version = reader.ReadInt32() };
        }
    }

    public struct WelcomePacket : IPacket
    {
        public PacketType Type => PacketType.Welcome;
        public int AssignedId;
        public int Seed;
        public int TotalPlayers;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(AssignedId);
            writer.Write(Seed);
            writer.Write(TotalPlayers);
        }

        public static WelcomePacket Deserialize(BinaryReader reader)
        {
            return new WelcomePacket
            {
                AssignedId = reader.ReadInt32(),
                Seed = reader.ReadInt32(),
                TotalPlayers = reader.ReadInt32()
            };
        }
    }

    public struct HeartbeatPacket : IPacket
    {
        public PacketType Type => PacketType.Heartbeat;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
        }

        public static HeartbeatPacket Deserialize(BinaryReader reader)
        {
            return new HeartbeatPacket();
        }
    }

    public struct DisconnectPacket : IPacket
    {
        public PacketType Type => PacketType.Disconnect;
        public string Reason;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Reason ?? "Unknown");
        }

        public static DisconnectPacket Deserialize(BinaryReader reader)
        {
            // Helper to avoid EOS
            if (reader.BaseStream.Position >= reader.BaseStream.Length) return new DisconnectPacket { Reason = "Unknown" };
            return new DisconnectPacket { Reason = reader.ReadString() };
        }
    }

    public struct DiscoveryRequestPacket : IPacket
    {
        public PacketType Type => PacketType.DiscoveryRequest;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
        }
        
        public static DiscoveryRequestPacket Deserialize(BinaryReader reader) => new DiscoveryRequestPacket();
    }

    public struct DiscoveryResponsePacket : IPacket
    {
        public PacketType Type => PacketType.DiscoveryResponse;
        public string ServerName;
        public int CurrentPlayers;
        public int MaxPlayers;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(ServerName ?? "Unknown");
            writer.Write(CurrentPlayers);
            writer.Write(MaxPlayers);
        }

        public static DiscoveryResponsePacket Deserialize(BinaryReader reader)
        {
            return new DiscoveryResponsePacket
            {
                ServerName = reader.ReadString(),
                CurrentPlayers = reader.ReadInt32(),
                MaxPlayers = reader.ReadInt32()
            };
        }
    }

    public struct PingPacket : IPacket
    {
        public PacketType Type => PacketType.Ping;
        public long Timestamp;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Timestamp);
        }

        public static PingPacket Deserialize(BinaryReader reader)
        {
            return new PingPacket { Timestamp = reader.ReadInt64() };
        }
    }

    public struct PongPacket : IPacket
    {
        public PacketType Type => PacketType.Pong;
        public long Timestamp;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Timestamp);
        }

        public static PongPacket Deserialize(BinaryReader reader)
        {
            return new PongPacket { Timestamp = reader.ReadInt64() };
        }
    }
}
