using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>Client -> Host: "I want to join, here is my protocol version."</summary>
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
            => new JoinRequestPacket { Version = reader.ReadInt32() };
    }

    /// <summary>Host -> Client: accepted. Carries the assigned player id and the SHARED SEED that
    /// makes both simulations deterministic from frame 0.</summary>
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
            => new WelcomePacket
            {
                AssignedId = reader.ReadInt32(),
                Seed = reader.ReadInt32(),
                TotalPlayers = reader.ReadInt32()
            };
    }

    /// <summary>Keep-alive. Presence of traffic resets the peer's timeout.</summary>
    public struct HeartbeatPacket : IPacket
    {
        public PacketType Type => PacketType.Heartbeat;
        public void Serialize(BinaryWriter writer) => writer.Write((byte)Type);
        public static HeartbeatPacket Deserialize(BinaryReader reader) => new HeartbeatPacket();
    }

    /// <summary>Explicit goodbye so the peer can drop us immediately instead of waiting to time out.</summary>
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
            => new DisconnectPacket { Reason = reader.ReadString() };
    }

    /// <summary>Host -> all: the match starts now, with this seed and player count.</summary>
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
            => new StartGamePacket
            {
                Seed = reader.ReadInt32(),
                TotalPlayers = reader.ReadInt32()
            };
    }
}
