using System;
using System.IO;
using Bomberman.Core;
using Bomberman.Core.Input;
using Bomberman.Net.Packets;

namespace Bomberman.Net
{
    /// <summary>
    /// Static helper class for serializing and deserializing network packets.
    /// Handles packet creation and parsing for the unified protocol.
    /// </summary>
    public static class NetworkProtocol
    {
        public const int ProtocolVersion = 1;

        /// <summary>
        /// Serializes a generic packet into a byte array.
        /// </summary>
        public static byte[] Serialize(IPacket packet)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                packet.Serialize(writer);
                return ms.ToArray();
            }
        }

        public static byte[] CreateJoinRequest() => Serialize(new JoinRequestPacket { Version = ProtocolVersion });

        public static int ReadJoinRequest(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // type
                return JoinRequestPacket.Deserialize(reader).Version;
            }
        }

        public static byte[] CreateWelcome(int assignedId, int seed, int totalPlayers) 
            => Serialize(new WelcomePacket { AssignedId = assignedId, Seed = seed, TotalPlayers = totalPlayers });

        public static (int assignedId, int seed, int totalPlayers) ReadWelcome(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = WelcomePacket.Deserialize(reader);
                return (p.AssignedId, p.Seed, p.TotalPlayers);
            }
        }

        public static byte[] CreateLobbyUpdate(int connectedCount, int totalPlayers, int slotMask)
            => Serialize(new LobbyUpdatePacket { ConnectedCount = connectedCount, TotalPlayers = totalPlayers, SlotMask = slotMask });

        public static (int connectedCount, int totalPlayers, int slotMask) ReadLobbyUpdate(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = LobbyUpdatePacket.Deserialize(reader);
                return (p.ConnectedCount, p.TotalPlayers, p.SlotMask);
            }
        }

        public static byte[] CreateStartGame(int seed, int totalPlayers)
            => Serialize(new StartGamePacket { Seed = seed, TotalPlayers = totalPlayers });

        public static (int seed, int totalPlayers) ReadStartGame(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = StartGamePacket.Deserialize(reader);
                return (p.Seed, p.TotalPlayers);
            }
        }

        /// <summary>
        /// Creates a packet containing player input for a specific frame.
        /// </summary>
        public static byte[] CreateInputPacket(int playerId, int startFrame, InputState[] inputs, IntVector2 currentPos, int stateHash)
        {
            return Serialize(new InputPacket
            {
                PlayerId = playerId,
                StartFrame = startFrame,
                Inputs = inputs,
                CurrentPos = currentPos,
                StateHash = stateHash
            });
        }

        public static (int playerId, int startFrame, InputState[] inputs, IntVector2 currentPos, int stateHash) ReadInputPacket(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = InputPacket.Deserialize(reader);
                return (p.PlayerId, p.StartFrame, p.Inputs, p.CurrentPos, p.StateHash);
            }
        }

        public static byte[] CreateDiscoveryRequest() => Serialize(new DiscoveryRequestPacket());

        public static byte[] CreateDiscoveryResponse(string serverName, int currentPlayers, int maxPlayers)
            => Serialize(new DiscoveryResponsePacket { ServerName = serverName, CurrentPlayers = currentPlayers, MaxPlayers = maxPlayers });

        public static (string serverName, int currentPlayers, int maxPlayers) ReadDiscoveryResponse(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = DiscoveryResponsePacket.Deserialize(reader);
                return (p.ServerName, p.CurrentPlayers, p.MaxPlayers);
            }
        }

        /// <summary>
        /// Reads the packet type from the first byte of the data.
        /// </summary>
        public static PacketType ReadType(byte[] data)
        {
            if (data == null || data.Length == 0) return PacketType.Input;
            return (PacketType)data[0];
        }

        public static byte[] CreateHeartbeat() => Serialize(new HeartbeatPacket());

        public static byte[] CreateDisconnect(string reason) => Serialize(new DisconnectPacket { Reason = reason });

        public static string ReadDisconnect(byte[] data)
        {
             using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = DisconnectPacket.Deserialize(reader);
                return p.Reason;
            }
        }

        public static byte[] CreateLobbyReady(int playerId, bool isReady)
            => Serialize(new LobbyReadyPacket { PlayerId = playerId, IsReady = isReady });

        public static (int PlayerId, bool IsReady) ReadLobbyReady(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = LobbyReadyPacket.Deserialize(reader);
                return (p.PlayerId, p.IsReady);
            }
        }

        public static byte[] CreateStateSync(byte[] snapshotData)
            => Serialize(new StateSyncPacket { Data = snapshotData });

        public static byte[] ReadStateSync(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = StateSyncPacket.Deserialize(reader);
                return p.Data;
            }
        }

        public static byte[] CreateStateChunk(int index, int totalChunks, byte[] chunkData)
            => Serialize(new StateChunkPacket { Index = index, TotalChunks = totalChunks, Data = chunkData });

        public static (int index, int totalChunks, byte[] data) ReadStateChunk(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = StateChunkPacket.Deserialize(reader);
                return (p.Index, p.TotalChunks, p.Data);
            }
        }

        public static byte[] CreatePing(long timestamp) => Serialize(new PingPacket { Timestamp = timestamp });

        public static long ReadPing(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                return PingPacket.Deserialize(reader).Timestamp;
            }
        }

        public static byte[] CreatePong(long timestamp) => Serialize(new PongPacket { Timestamp = timestamp });

        public static long ReadPong(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                return PongPacket.Deserialize(reader).Timestamp;
            }
        }
    }
}
