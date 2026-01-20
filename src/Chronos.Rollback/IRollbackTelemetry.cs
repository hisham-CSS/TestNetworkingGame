namespace Chronos.Rollback
{
    /// <summary>
    /// Interface for logging telemetry and events from the Rollback System.
    /// </summary>
    public interface IRollbackTelemetry
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
        void RecordMisprediction(int frame, int playerId);
        void RecordRollback(int fromFrame, int toFrame);
    }

    /// <summary>
    /// Default no-op telemetry.
    /// </summary>
    public class NoOpTelemetry : IRollbackTelemetry
    {
        public void Log(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
        public void RecordMisprediction(int frame, int playerId) { }
        public void RecordRollback(int fromFrame, int toFrame) { }
    }
}
