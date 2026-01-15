using System;

namespace Bomberman.Core.Logging
{
    public interface ILogger
    {
        /// <summary>Logs an informational message.</summary>
        void Info(string message);
        /// <summary>Logs a warning message.</summary>
        void Warning(string message);
        /// <summary>Logs an error message with optional exception details.</summary>
        void Error(string message, Exception? ex = null);
        /// <summary>Logs a debug message (only in DEBUG builds).</summary>
        void Debug(string message);
    }
}
