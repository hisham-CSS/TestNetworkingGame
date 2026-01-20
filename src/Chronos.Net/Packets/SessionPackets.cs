using System.IO;

namespace Chronos.Net.Packets
{
    public struct JoinRequestPacket : IPacket
    {
        public PacketType Type => PacketType.JoinRequest;
        public int Version { get; set; }

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
        public int AssignedId { get; set; }
        public int Seed { get; set; }
        public int TotalPlayers { get; set; }

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
        public void Serialize(BinaryWriter writer) => writer.Write((byte)Type);
        public static HeartbeatPacket Deserialize(BinaryReader reader) => new HeartbeatPacket();
    }

    public struct DisconnectPacket : IPacket
    {
        public PacketType Type => PacketType.Disconnect;
        public string Reason { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(Reason ?? "Unknown");
        }

        public static DisconnectPacket Deserialize(BinaryReader reader)
        {
            return new DisconnectPacket { Reason = reader.ReadString() };
        }
    }

    public struct StartGamePacket : IPacket
    {
        public PacketType Type => PacketType.StartGame;
        public int Seed { get; set; }
        public int TotalPlayers { get; set; }

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
