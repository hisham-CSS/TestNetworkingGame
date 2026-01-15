using System;
using Bomberman.Core.Logging;

namespace Bomberman.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of ILogger.
    /// Captures logs or ignores them for testing.
    /// </summary>
    public class MockLogger : ILogger
    {
        public System.Collections.Generic.List<string> Logs { get; } = new System.Collections.Generic.List<string>();

        public void Info(string message) => Logs.Add($"[INFO] {message}");
        public void Warning(string message) => Logs.Add($"[WARN] {message}");
        public void Error(string message, Exception? ex = null) => Logs.Add($"[ERROR] {message} {ex}");
        public void Debug(string message) => Logs.Add($"[DEBUG] {message}");
    }
}
