using System;
using System.IO;

namespace Bomberman.Core.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _path;
        private readonly object _lock = new object();

        public FileLogger(string path)
        {
            _path = path;
            // Ensure directory exists
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        public void Info(string message) => Log("INFO", message);
        public void Warning(string message) => Log("WARN", message);
        public void Error(string message, Exception? ex = null)
        {
            Log("ERROR", message);
            if (ex != null) Log("ERROR", ex.ToString());
        }
        public void Debug(string message)
        {
#if DEBUG
            Log("DEBUG", message);
#endif
        }

        private void Log(string level, string message)
        {
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
                }
                catch { /* Best effort */ }
            }
        }
    }
}
