namespace Bomberman.Net.Packets
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
        LobbyReady = 9,
        StateSync = 10,
        StateChunk = 11,
        Ping = 12,
        Pong = 13
    }
}
