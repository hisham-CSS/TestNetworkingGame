using System;
using System.IO;
using Chronos.Core;
using Chronos.Net.Packets;

namespace Chronos.Net.Protocol
{
    /// <summary>
    /// Static helper class for serializing and deserializing network packets.
    /// Handles packet creation and parsing for the unified protocol.
    /// </summary>
    public static class NetworkProtocol<TInput> where TInput : struct, IInputState<TInput>
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
        // --- Input (Week 5: run-length compressed variant) ---
        public static byte[] CreateCompressedInputPacket(int playerId, int startFrame, TInput[] inputs, int posX, int posY, int stateHash)
        {
            return Serialize(new Packets.CompressedInputPacket<TInput>
            {
                PlayerId = playerId,
                StartFrame = startFrame,
                Inputs = inputs,
                PosX = posX,
                PosY = posY,
                StateHash = stateHash
            });
        }

        public static (int pid, int startFrame, TInput[] inputs, int posX, int posY, int hash) ReadCompressedInputPacket(byte[] data)
        {
            using (var ms = new System.IO.MemoryStream(data))
            using (var reader = new System.IO.BinaryReader(ms))
            {
                reader.ReadByte();
                var p = Packets.CompressedInputPacket<TInput>.Deserialize(reader);
                return (p.PlayerId, p.StartFrame, p.Inputs, p.PosX, p.PosY, p.StateHash);
            }
        }

        public static byte[] CreateInputPacket(int playerId, int startFrame, TInput[] inputs, int posX, int posY, int stateHash)
        {
            return Serialize(new InputPacket<TInput>
            {
                PlayerId = playerId,
                StartFrame = startFrame,
                Inputs = inputs,
                PosX = posX,
                PosY = posY,
                StateHash = stateHash
            });
        }

        // --- Readers ---
        
        // We can expose Read methods or just use manual deserialization where needed. 
        // For consistency with original codebase, let's expose generic readers.

        public static PacketType ReadType(byte[] data)
        {
            if (data == null || data.Length == 0) return PacketType.Input; // Default? Or Error
            return (PacketType)data[0];
        }

        public static int ReadJoinRequest(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                return JoinRequestPacket.Deserialize(reader).Version;
            }
        }

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

        public static (int connected, int total, int mask) ReadLobbyUpdate(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = LobbyUpdatePacket.Deserialize(reader);
                return (p.ConnectedCount, p.TotalPlayers, p.SlotMask);
            }
        }

        public static (int seed, int total) ReadStartGame(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = StartGamePacket.Deserialize(reader);
                return (p.Seed, p.TotalPlayers);
            }
        }

        public static (int pid, bool isReady) ReadLobbyReady(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = LobbyReadyPacket.Deserialize(reader);
                return (p.PlayerId, p.IsReady);
            }
        }

        public static string ReadDisconnect(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                return DisconnectPacket.Deserialize(reader).Reason;
            }
        }

        public static (string name, int cur, int max) ReadDiscoveryResponse(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = DiscoveryResponsePacket.Deserialize(reader);
                return (p.ServerName, p.CurrentPlayers, p.MaxPlayers);
            }
        }

        public static long ReadPing(byte[] data)
        {
             using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                return PingPacket.Deserialize(reader).Timestamp;
            }
        }

        public static long ReadPong(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                return PongPacket.Deserialize(reader).Timestamp;
            }
        }

        public static byte[] ReadStateSync(byte[] data)
        {
             using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                return StateSyncPacket.Deserialize(reader).Data;
            }
        }

        public static (int index, int total, byte[] data) ReadStateChunk(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                var p = StateChunkPacket.Deserialize(reader);
                return (p.Index, p.TotalChunks, p.Data);
            }
        }

        public static (int pid, int startFrame, TInput[] inputs, int posX, int posY, int hash) ReadInputPacket(byte[] data)
        {
             using (var ms = new MemoryStream(data))
             using (var reader = new BinaryReader(ms))
             {
                 reader.ReadByte(); 
                 var p = InputPacket<TInput>.Deserialize(reader);
                 return (p.PlayerId, p.StartFrame, p.Inputs, p.PosX, p.PosY, p.StateHash);
             }
        }
    }
}
