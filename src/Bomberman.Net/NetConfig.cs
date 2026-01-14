namespace Bomberman.Net
{
    /// <summary>
    /// Configuration for networking parameters.
    /// </summary>
    public class NetConfig
    {
        public int ServerPort { get; set; } = 5000;
        public int DiscoveryPort { get; set; } = 54321;
        
        // Protocol
        public int ProtocolVersion { get; set; } = 1;
        public int MaxPacketSize { get; set; } = 1200; // Under Ethernet MTU
        
        // State Sync
        public int ChunkSize { get; set; } = 1000;
        
        // Timeouts
        public int ConnectionTimeoutMs { get; set; } = 5000;
        public int HeartbeatIntervalMs { get; set; } = 1000;
        
        public static NetConfig Default => new NetConfig();
    }
}
