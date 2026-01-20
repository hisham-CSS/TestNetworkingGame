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
        /// <summary>Creates a packet to request joining a game session.</summary>
        public static byte[] CreateJoinRequest() => Serialize(new JoinRequestPacket { Version = ProtocolVersion });
        
        /// <summary>Creates a welcome packet for a new client.</summary>
        /// <param name="assignedId">The player ID assigned to the client.</param>
        /// <param name="seed">The random seed for the game session.</param>
        /// <param name="totalPlayers">The total number of players expected.</param>
        public static byte[] CreateWelcome(int assignedId, int seed, int totalPlayers) 
            => Serialize(new WelcomePacket { AssignedId = assignedId, Seed = seed, TotalPlayers = totalPlayers });
            
        /// <summary>Creates a keep-alive heartbeat packet.</summary>
        public static byte[] CreateHeartbeat() => Serialize(new HeartbeatPacket());
        
        /// <summary>Creates a disconnect packet with a reason.</summary>
        public static byte[] CreateDisconnect(string reason) => Serialize(new DisconnectPacket { Reason = reason });
        
        /// <summary>Creates a packet to signal the start of the game.</summary>
        public static byte[] CreateStartGame(int seed, int totalPlayers)
            => Serialize(new StartGamePacket { Seed = seed, TotalPlayers = totalPlayers });

        // --- Lobby ---
        /// <summary>Creates a packet with the current lobby status.</summary>
        public static byte[] CreateLobbyUpdate(int connectedCount, int totalPlayers, int slotMask)
            => Serialize(new LobbyUpdatePacket { ConnectedCount = connectedCount, TotalPlayers = totalPlayers, SlotMask = slotMask });
            
        /// <summary>Creates a packet signaling a player's ready status.</summary>
        public static byte[] CreateLobbyReady(int playerId, bool isReady)
            => Serialize(new LobbyReadyPacket { PlayerId = playerId, IsReady = isReady });
            
        /// <summary>Creates a packet to broadcast discovery requests to find local servers.</summary>
        public static byte[] CreateDiscoveryRequest() => Serialize(new DiscoveryRequestPacket());
        
        /// <summary>Creates a response to a discovery request.</summary>
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
        /// <summary>
        /// Creates a packet containing input data.
        /// </summary>
        /// <param name="playerId">The ID of the player sending inputs.</param>
        /// <param name="startFrame">The frame number for the first input in the history.</param>
        /// <param name="inputs">The array of input states (current + history).</param>
        /// <param name="posX">Debug validation X position.</param>
        /// <param name="posY">Debug validation Y position.</param>
        /// <param name="stateHash">Hash of the simulation state for desync detection.</param>
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
