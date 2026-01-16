using System;
using Bomberman.Core.Logging;
using Moq;
using NUnit.Framework;

namespace Bomberman.Tests.Core.Logging
{
    [TestFixture]
    public class CompositeLoggerTests
    {
        [Test]
        public void Info_DelegatesToAllLoggers()
        {
            var logger1 = new Mock<ILogger>();
            var logger2 = new Mock<ILogger>();
            var composite = new CompositeLogger(logger1.Object, logger2.Object);

            composite.Info("test message");

            logger1.Verify(x => x.Info("test message"), Times.Once);
            logger2.Verify(x => x.Info("test message"), Times.Once);
        }

        [Test]
        public void Warning_DelegatesToAllLoggers()
        {
            var logger1 = new Mock<ILogger>();
            var logger2 = new Mock<ILogger>();
            var composite = new CompositeLogger(logger1.Object, logger2.Object);

            composite.Warning("warn message");

            logger1.Verify(x => x.Warning("warn message"), Times.Once);
            logger2.Verify(x => x.Warning("warn message"), Times.Once);
        }

        [Test]
        public void Error_DelegatesToAllLoggers()
        {
            var logger1 = new Mock<ILogger>();
            var logger2 = new Mock<ILogger>();
            var composite = new CompositeLogger(logger1.Object, logger2.Object);
            var ex = new Exception("bang");

            composite.Error("error message", ex);

            logger1.Verify(x => x.Error("error message", ex), Times.Once);
            logger2.Verify(x => x.Error("error message", ex), Times.Once);
        }

        [Test]
        public void Debug_DelegatesToAllLoggers()
        {
            var logger1 = new Mock<ILogger>();
            var logger2 = new Mock<ILogger>();
            var composite = new CompositeLogger(logger1.Object, logger2.Object);

            composite.Debug("debug message");

            logger1.Verify(x => x.Debug("debug message"), Times.Once);
            logger2.Verify(x => x.Debug("debug message"), Times.Once);
        }
    }
}
