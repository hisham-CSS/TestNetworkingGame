using System.IO;

namespace Bomberman.Net.Packets
{
    /// <summary>Host -> all: how many slots are filled and a bitmask of which seats are taken.</summary>
    public struct LobbyUpdatePacket : IPacket
    {
        public PacketType Type => PacketType.LobbyUpdate;
        public int ConnectedCount { get; set; }
        public int TotalPlayers { get; set; }
        public int SlotMask { get; set; }

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

            // SlotMask is read defensively so older senders that omit it still parse.
            if (reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
            {
                p.SlotMask = reader.ReadInt32();
            }
            return p;
        }
    }

    /// <summary>A player's ready toggle. Client sends to host; host re-broadcasts to everyone.</summary>
    public struct LobbyReadyPacket : IPacket
    {
        public PacketType Type => PacketType.LobbyReady;
        public int PlayerId { get; set; }
        public bool IsReady { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(PlayerId);
            writer.Write(IsReady);
        }

        public static LobbyReadyPacket Deserialize(BinaryReader reader)
            => new LobbyReadyPacket
            {
                PlayerId = reader.ReadInt32(),
                IsReady = reader.ReadBoolean()
            };
    }

    /// <summary>Client -> broadcast: "any servers out there?" Sent to a range of LAN ports.</summary>
    public struct DiscoveryRequestPacket : IPacket
    {
        public PacketType Type => PacketType.DiscoveryRequest;
        public void Serialize(BinaryWriter writer) => writer.Write((byte)Type);
        public static DiscoveryRequestPacket Deserialize(BinaryReader reader) => new DiscoveryRequestPacket();
    }

    /// <summary>Server -> client: "here I am," with a name and current/max player counts.</summary>
    public struct DiscoveryResponsePacket : IPacket
    {
        public PacketType Type => PacketType.DiscoveryResponse;
        public string ServerName { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Type);
            writer.Write(ServerName ?? "");
            writer.Write(CurrentPlayers);
            writer.Write(MaxPlayers);
        }

        public static DiscoveryResponsePacket Deserialize(BinaryReader reader)
            => new DiscoveryResponsePacket
            {
                ServerName = reader.ReadString(),
                CurrentPlayers = reader.ReadInt32(),
                MaxPlayers = reader.ReadInt32()
            };
    }
}
