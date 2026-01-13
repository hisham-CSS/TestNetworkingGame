using System;
using System.Collections.Generic;

namespace Bomberman.Core.Logging
{
    public class CompositeLogger : ILogger
    {
        private readonly List<ILogger> _loggers;

        public CompositeLogger(params ILogger[] loggers)
        {
            _loggers = new List<ILogger>(loggers);
        }

        public void Info(string message) => _loggers.ForEach(l => l.Info(message));
        public void Warning(string message) => _loggers.ForEach(l => l.Warning(message));
        public void Error(string message, Exception? ex = null) => _loggers.ForEach(l => l.Error(message, ex));
        public void Debug(string message) => _loggers.ForEach(l => l.Debug(message));
    }
}
