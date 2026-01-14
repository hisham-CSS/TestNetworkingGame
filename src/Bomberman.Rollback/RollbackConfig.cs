namespace Bomberman.Rollback
{
    /// <summary>
    /// Configuration for the Rollback system.
    /// </summary>
    public class RollbackConfig
    {
        public int MaxSnapshotFrames { get; set; } = 60 * 5; // 5 Seconds
        public int MaxPredictionFrames { get; set; } = 60 * 60 * 10; // "Unlimited"
        
        public int InputDelayFrames { get; set; } = 2; // Default delay for smoother netplay?
        public int RedundancyFactor { get; set; } = 8; // Inputs to resend
        
        public static RollbackConfig Default => new RollbackConfig();
    }
}
