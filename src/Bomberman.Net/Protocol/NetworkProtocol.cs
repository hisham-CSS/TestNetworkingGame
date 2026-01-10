using System;
using System.IO;
using System.IO;
using Bomberman.Core;

namespace Bomberman.Net
{
    public enum PacketType : byte
    {
        Input = 0,
        JoinRequest = 1,
        Welcome = 2,
        StartGame = 3,
        LobbyUpdate = 4,
        DiscoveryRequest = 5,
        DiscoveryResponse = 6,
        Heartbeat = 7,
        Disconnect = 8,
        LobbyReady = 9
    }

    public static class NetworkProtocol
    {
        public const int ProtocolVersion = 1;

        public static byte[] CreateJoinRequest()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.JoinRequest);
                writer.Write(ProtocolVersion);
                return ms.ToArray();
            }
        }

        public static int ReadJoinRequest(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // type
                // Handle legacy or short packets safely
                if (ms.Position >= ms.Length) return 0; 
                return reader.ReadInt32();
            }
        }

        public static byte[] CreateWelcome(int assignedId, int seed, int totalPlayers)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.Welcome);
                writer.Write(assignedId);
                writer.Write(seed);
                writer.Write(totalPlayers);
                return ms.ToArray();
            }
        }

        public static (int assignedId, int seed, int totalPlayers) ReadWelcome(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // type
                int id = reader.ReadInt32();
                int seed = reader.ReadInt32();
                int total = reader.ReadInt32();
                return (id, seed, total);
            }
        }

        public static byte[] CreateLobbyUpdate(int connectedCount, int totalPlayers)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.LobbyUpdate);
                writer.Write(connectedCount);
                writer.Write(totalPlayers);
                return ms.ToArray();
            }
        }

        public static (int connectedCount, int totalPlayers) ReadLobbyUpdate(byte[] data)
        {
             using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); 
                int c = reader.ReadInt32();
                int t = reader.ReadInt32();
                return (c, t);
            }
        }

        public static byte[] CreateStartGame(int seed, int totalPlayers)
        {
             using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.StartGame);
                writer.Write(seed);
                writer.Write(totalPlayers);
                return ms.ToArray();
            }
        }

        public static (int seed, int totalPlayers) ReadStartGame(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); 
                int s = reader.ReadInt32();
                int t = reader.ReadInt32();
                return (s, t);
            }
        }

        public static byte[] CreateInputPacket(int playerId, int startFrame, InputState[] inputs, IntVector2 currentPos, int stateHash)
        {
             using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.Input);
                writer.Write(playerId);
                writer.Write(startFrame);
                writer.Write(inputs.Length);
                writer.Write(currentPos.X);
                writer.Write(currentPos.Y);
                writer.Write(stateHash); // New: State Hash
                for (int i = 0; i < inputs.Length; i++)
                {
                    writer.Write(inputs[i].Movement.X);
                    writer.Write(inputs[i].Movement.Y);
                    writer.Write(inputs[i].PlaceBomb);
                    // Explicit Bomb Target
                    writer.Write(inputs[i].BombTarget.X);
                    writer.Write(inputs[i].BombTarget.Y);
                }
                return ms.ToArray();
            }
        }

         public static (int playerId, int startFrame, InputState[] inputs, IntVector2 currentPos, int stateHash) ReadInputPacket(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // Skip Type
                int playerId = reader.ReadInt32();
                int startFrame = reader.ReadInt32();
                int count = reader.ReadInt32();
                
                // Hardening: Validate count against remaining data length
                long remainingBytes = ms.Length - ms.Position;
                // Minimum bytes per input: Movement(8) + PlaceBomb(1) + BombTarget(8) = 17 bytes
                // We also have X, Y, StateHash (12 bytes) before the loop.
                if (count < 0 || count > 60 || remainingBytes < count * 17) 
                {
                     // Return empty/safe default or throw specific error?
                     // For now, return empty to avoid crash. 
                     // Ideally we should log this.
                     return (playerId, startFrame, new InputState[0], new IntVector2(0,0), 0);
                }

                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                int stateHash = reader.ReadInt32(); // Read Hash
                IntVector2 currentPos = new IntVector2(x, y);
                
                InputState[] inputs = new InputState[count];
                for (int i = 0; i < count; i++)
                {
                     inputs[i].Movement.X = reader.ReadInt32();
                     inputs[i].Movement.Y = reader.ReadInt32();
                     inputs[i].PlaceBomb = reader.ReadBoolean();
                     inputs[i].BombTarget = new IntVector2(reader.ReadInt32(), reader.ReadInt32());
                }

                return (playerId, startFrame, inputs, currentPos, stateHash);
            }
        }

        public static byte[] CreateDiscoveryRequest()
        {
            return new byte[] { (byte)PacketType.DiscoveryRequest };
        }

        public static byte[] CreateDiscoveryResponse(string serverName, int currentPlayers, int maxPlayers)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.DiscoveryResponse);
                writer.Write(serverName);
                writer.Write(currentPlayers);
                writer.Write(maxPlayers);
                return ms.ToArray();
            }
        }

        public static (string serverName, int currentPlayers, int maxPlayers) ReadDiscoveryResponse(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); 
                string name = reader.ReadString();
                int cur = reader.ReadInt32();
                int max = reader.ReadInt32();
                return (name, cur, max);
            }
        }

        public static PacketType ReadType(byte[] data)
        {
             if (data == null || data.Length == 0) return PacketType.Input; 
             return (PacketType)data[0];
        }
        public static byte[] CreateHeartbeat()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.Heartbeat);
                return ms.ToArray();
            }
        }

        public static byte[] CreateDisconnect(string reason)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.Disconnect);
                writer.Write(reason);
                return ms.ToArray();
            }
        }

        public static string ReadDisconnect(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); 
                if (ms.Position >= ms.Length) return "Unknown";
                return reader.ReadString();
            }
        }
        public static byte[] CreateLobbyReady(int playerId, bool isReady)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.LobbyReady);
                writer.Write(playerId);
                writer.Write(isReady);
                return ms.ToArray();
            }
        }

        public static (int PlayerId, bool IsReady) ReadLobbyReady(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                int pid = reader.ReadInt32();
                bool ready = reader.ReadBoolean();
                return (pid, ready);
            }
        }
    }
}
