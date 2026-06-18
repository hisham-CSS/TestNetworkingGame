namespace Chronos.Rollback
{
    /// <summary>
    /// Configuration for the Rollback system.
    /// </summary>
    public class RollbackConfig
    {
        // --- Memory ---

        /// <summary>Maximum number of frames to keep in history for rewinding (default 5s @ 60fps).</summary>
        public int MaxSnapshotFrames { get; set; } = 60 * 5; 
        
        // --- Prediction ---

        /// <summary>
        /// Maximum frames we may predict ahead of the last confirmed remote frame before we STALL.
        /// This MUST stay well below <see cref="MaxSnapshotFrames"/>: if we predicted further ahead than
        /// the snapshot buffer can hold, a misprediction could need a snapshot that has already been
        /// evicted, the rollback would be skipped, and the client would desync permanently. Capping the
        /// window (GGPO uses 8) guarantees every rollback target is still buffered, so we stall briefly
        /// instead of diverging. Tunable for latency, but keep it far under MaxSnapshotFrames.
        /// </summary>
        public int MaxPredictionFrames { get; set; } = 8;
        
        // --- Input ---

        /// <summary>Fixed frame delay for local inputs to account for network latency.</summary>
        public int InputDelayFrames { get; set; } = 2; 
        /// <summary>Number of redundant input frames to include in each packet.</summary>
        public int RedundancyFactor { get; set; } = 8; 
        
        public static RollbackConfig Default => new RollbackConfig();
    }
}
