namespace Bomberman.Net.Packets
{
    /// <summary>
    /// Identifiers for every packet type in the protocol. The value is written as the FIRST byte of
    /// every packet so the receiver can dispatch on it before reading the rest of the payload.
    /// </summary>
    public enum PacketType : byte
    {
        Input = 0,             // Input history from a client, or broadcast from the host
        JoinRequest = 1,       // Client asking to join
        Welcome = 2,           // Host accepting: assigns id + shared seed
        StartGame = 3,         // Host signalling the match begins
        LobbyUpdate = 4,       // Host broadcasting lobby slot state
        DiscoveryRequest = 5,  // LAN discovery broadcast
        DiscoveryResponse = 6, // Server answering a discovery probe
        Heartbeat = 7,         // Keep-alive
        Disconnect = 8,        // Explicit disconnection notice
        LobbyReady = 9,        // A player's ready status
        StateSync = 10,        // Full state snapshot (used from Week 4-5)
        StateChunk = 11,       // One fragment of a chunked snapshot
        Ping = 12,             // Latency probe (carries a timestamp)
        Pong = 13              // Reply to a Ping (echoes the timestamp)
    }
}
