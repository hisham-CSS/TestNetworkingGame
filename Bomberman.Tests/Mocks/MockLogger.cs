using System;
using Bomberman.Core.Logging;

namespace Bomberman.Tests.Mocks
{
    public class MockLogger : ILogger
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? ex = null) { }
        public void Debug(string message) { }
    }
}
