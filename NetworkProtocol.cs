using System;
using System.IO;

namespace Bomberman
{
    public enum PacketType : byte
    {
        Input = 0,
        JoinRequest = 1,
        Welcome = 2,
        StartGame = 3,
        LobbyUpdate = 4,
        DiscoveryRequest = 5,
        DiscoveryResponse = 6
    }

    public static class NetworkProtocol
    {
        public static byte[] CreateJoinRequest()
        {
            return new byte[] { (byte)PacketType.JoinRequest };
        }

        public static byte[] CreateWelcome(int playerId, int seed, int playerCount)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.Welcome);
                writer.Write(playerId);
                writer.Write(seed);
                writer.Write(playerCount);
                return ms.ToArray();
            }
        }

        public static (int playerId, int seed, int playerCount) ReadWelcome(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // Skip Type
                int pid = reader.ReadInt32();
                int seed = reader.ReadInt32();
                int count = reader.ReadInt32();
                return (pid, seed, count);
            }
        }



        public static byte[] CreateLobbyUpdate(int currentCount, int totalRequired)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.LobbyUpdate);
                writer.Write(currentCount);
                writer.Write(totalRequired);
                return ms.ToArray();
            }
        }

        public static (int currentCount, int totalRequired) ReadLobbyUpdate(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // Skip
                int current = reader.ReadInt32();
                int total = reader.ReadInt32();
                return (current, total);
            }
        }

        public static byte[] CreateStartGame(int seed, int playerCount)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.StartGame);
                writer.Write(seed);
                writer.Write(playerCount);
                return ms.ToArray();
            }
        }

        public static (int seed, int playerCount) ReadStartGame(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // Skip
                int seed = reader.ReadInt32();
                int playerCount = reader.ReadInt32();
                return (seed, playerCount);
            }
        }

        public static byte[] CreateInputPacket(int playerId, int frameId, InputState input)
        {
             using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)PacketType.Input);
                writer.Write(playerId);
                writer.Write(frameId);
                writer.Write(input.Movement.X);
                writer.Write(input.Movement.Y);
                writer.Write(input.PlaceBomb);
                return ms.ToArray();
            }
        }

         public static (int playerId, int frameId, InputState input) ReadInputPacket(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // Skip Type
                int playerId = reader.ReadInt32();
                int frameId = reader.ReadInt32();
                InputState input = new InputState();
                input.Movement.X = reader.ReadSingle();
                input.Movement.Y = reader.ReadSingle();
                input.PlaceBomb = reader.ReadBoolean();
                return (playerId, frameId, input);
            }
        }

        public static PacketType ReadType(byte[] data)
        {
             if (data == null || data.Length == 0) return PacketType.Input; // Fallback? Or Error?
             return (PacketType)data[0];
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
                writer.Write(serverName); // String length prefixed automatically
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
                reader.ReadByte(); // Skip Type
                string name = reader.ReadString();
                int current = reader.ReadInt32();
                int max = reader.ReadInt32();
                return (name, current, max);
            }
        }
    }
}
