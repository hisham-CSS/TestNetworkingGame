namespace Bomberman.Net
{
    /// <summary>
    /// Configuration for networking parameters.
    /// </summary>
    public class NetConfig
    {
        // --- Ports ---
        /// <summary>UDP port for the host server.</summary>
        public int ServerPort { get; set; } = 5000;
        /// <summary>UDP port for LAN discovery broadcasts.</summary>
        public int DiscoveryPort { get; set; } = 54321;
        
        // --- Protocol ---
        /// <summary>Protocol version for compatibility checks.</summary>
        public int ProtocolVersion { get; set; } = 1;
        /// <summary>Maximum packet size in bytes (should be under Ethernet MTU ~1500).</summary>
        public int MaxPacketSize { get; set; } = 1200; 
        
        // --- State Sync ---
        /// <summary>Size of chunks when splitting large state snapshots.</summary>
        public int ChunkSize { get; set; } = 1000;
        
        // --- Timeouts ---
        /// <summary>Time in milliseconds before considering a connection lost.</summary>
        public int ConnectionTimeoutMs { get; set; } = 5000;
        /// <summary>Interval in milliseconds for sending heartbeat packets.</summary>
        public int HeartbeatIntervalMs { get; set; } = 1000;
        
        public static NetConfig Default => new NetConfig();
    }
}
