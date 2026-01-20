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

        /// <summary>Maximum frames ahead of the last confirmed frame we can simulate.</summary>
        public int MaxPredictionFrames { get; set; } = 60 * 60 * 10; // "Unlimited"
        
        // --- Input ---

        /// <summary>Fixed frame delay for local inputs to account for network latency.</summary>
        public int InputDelayFrames { get; set; } = 2; 
        /// <summary>Number of redundant input frames to include in each packet.</summary>
        public int RedundancyFactor { get; set; } = 8; 
        
        public static RollbackConfig Default => new RollbackConfig();
    }
}
