using System;

namespace Bomberman.Core.Logging
{
    public class ConsoleLogger : ILogger
    {
        public void Info(string message) => Write(ConsoleColor.White, "[INFO]", message);
        public void Warning(string message) => Write(ConsoleColor.Yellow, "[WARN]", message);
        public void Error(string message, Exception? ex = null)
        {
            Write(ConsoleColor.Red, "[ERROR]", message);
            if (ex != null) Console.WriteLine(ex);
        }
        public void Debug(string message)
        {
#if DEBUG
            Write(ConsoleColor.Gray, "[DEBUG]", message);
#endif
        }

        private void Write(ConsoleColor color, string label, string message)
        {
             var prev = Console.ForegroundColor;
             Console.ForegroundColor = color;
             Console.WriteLine($"{DateTime.Now:HH:mm:ss} {label} {message}");
             Console.ForegroundColor = prev;
        }
    }
}
