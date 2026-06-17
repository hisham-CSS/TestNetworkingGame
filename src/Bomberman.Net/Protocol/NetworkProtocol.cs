using System.IO;
using Bomberman.Core;
using Bomberman.Net.Packets;

namespace Bomberman.Net.Protocol
{
    /// <summary>
    /// Static helpers that turn packets into bytes and back. This is the single place that knows the
    /// wire format: every Create* builds a datagram, every Read* parses one. Generic over the game's
    /// input type so the protocol stays framework-agnostic (the seed of the Week 5 Chronos library).
    /// Back-ported faithfully from Chronos.Net.Protocol.NetworkProtocol.
    /// </summary>
    public static class NetworkProtocol<TInput> where TInput : struct, IInputState<TInput>
    {
        public const int ProtocolVersion = 1;

        /// <summary>Serializes any packet into a byte array (leading type byte + payload).</summary>
        public static byte[] Serialize(IPacket packet)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                packet.Serialize(writer);
                return ms.ToArray();
            }
        }

        // --- Session ---
        public static byte[] CreateJoinRequest() => Serialize(new JoinRequestPacket { Version = ProtocolVersion });
        public static byte[] CreateWelcome(int assignedId, int seed, int totalPlayers)
            => Serialize(new WelcomePacket { AssignedId = assignedId, Seed = seed, TotalPlayers = totalPlayers });
        public static byte[] CreateHeartbeat() => Serialize(new HeartbeatPacket());
        public static byte[] CreateDisconnect(string reason) => Serialize(new DisconnectPacket { Reason = reason });
        public static byte[] CreateStartGame(int seed, int totalPlayers)
            => Serialize(new StartGamePacket { Seed = seed, TotalPlayers = totalPlayers });

        // --- Lobby ---
        public static byte[] CreateLobbyUpdate(int connectedCount, int totalPlayers, int slotMask)
            => Serialize(new LobbyUpdatePacket { ConnectedCount = connectedCount, TotalPlayers = totalPlayers, SlotMask = slotMask });
        public static byte[] CreateLobbyReady(int playerId, bool isReady)
            => Serialize(new LobbyReadyPacket { PlayerId = playerId, IsReady = isReady });
        public static byte[] CreateDiscoveryRequest() => Serialize(new DiscoveryRequestPacket());
        public static byte[] CreateDiscoveryResponse(string serverName, int currentPlayers, int maxPlayers)
            => Serialize(new DiscoveryResponsePacket { ServerName = serverName, CurrentPlayers = currentPlayers, MaxPlayers = maxPlayers });

        // --- State ---
        public static byte[] CreateStateSync(byte[] snapshotData)
            => Serialize(new StateSyncPacket { Data = snapshotData });
        public static byte[] CreateStateChunk(int index, int totalChunks, byte[] chunkData)
            => Serialize(new StateChunkPacket { Index = index, TotalChunks = totalChunks, Data = chunkData });

        // --- Ping/Pong ---
        public static byte[] CreatePing(long timestamp) => Serialize(new PingPacket { Timestamp = timestamp });
        public static byte[] CreatePong(long timestamp) => Serialize(new PongPacket { Timestamp = timestamp });

        // --- Input ---
        public static byte[] CreateInputPacket(int playerId, int startFrame, TInput[] inputs, int posX, int posY, int stateHash)
            => Serialize(new InputPacket<TInput>
            {
                PlayerId = playerId,
                StartFrame = startFrame,
                Inputs = inputs,
                PosX = posX,
                PosY = posY,
                StateHash = stateHash
            });

        // --- Readers ---

        /// <summary>Peeks the leading byte to learn a datagram's type before fully parsing it.</summary>
        public static PacketType ReadType(byte[] data)
        {
            if (data == null || data.Length == 0) return PacketType.Input;
            return (PacketType)data[0];
        }

        public static int ReadJoinRequest(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            return JoinRequestPacket.Deserialize(reader).Version;
        }

        public static (int assignedId, int seed, int totalPlayers) ReadWelcome(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            var p = WelcomePacket.Deserialize(reader);
            return (p.AssignedId, p.Seed, p.TotalPlayers);
        }

        public static (int connected, int total, int mask) ReadLobbyUpdate(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            var p = LobbyUpdatePacket.Deserialize(reader);
            return (p.ConnectedCount, p.TotalPlayers, p.SlotMask);
        }

        public static (int seed, int total) ReadStartGame(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            var p = StartGamePacket.Deserialize(reader);
            return (p.Seed, p.TotalPlayers);
        }

        public static (int pid, bool isReady) ReadLobbyReady(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            var p = LobbyReadyPacket.Deserialize(reader);
            return (p.PlayerId, p.IsReady);
        }

        public static string ReadDisconnect(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            return DisconnectPacket.Deserialize(reader).Reason;
        }

        public static (string name, int cur, int max) ReadDiscoveryResponse(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            var p = DiscoveryResponsePacket.Deserialize(reader);
            return (p.ServerName, p.CurrentPlayers, p.MaxPlayers);
        }

        public static long ReadPing(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            return PingPacket.Deserialize(reader).Timestamp;
        }

        public static long ReadPong(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            return PongPacket.Deserialize(reader).Timestamp;
        }

        public static byte[] ReadStateSync(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            return StateSyncPacket.Deserialize(reader).Data;
        }

        public static (int index, int total, byte[] data) ReadStateChunk(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            var p = StateChunkPacket.Deserialize(reader);
            return (p.Index, p.TotalChunks, p.Data);
        }

        public static (int pid, int startFrame, TInput[] inputs, int posX, int posY, int hash) ReadInputPacket(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            reader.ReadByte();
            var p = InputPacket<TInput>.Deserialize(reader);
            return (p.PlayerId, p.StartFrame, p.Inputs, p.PosX, p.PosY, p.StateHash);
        }
    }
}
