using System.Threading;
using NUnit.Framework;
using Bomberman.Core;

namespace Bomberman.Tests
{
    [TestFixture]
    public class ThreadingTests
    {
        [Test]
        public void GameSession_Step_AdvancesFrame_AndRecordsInput()
        {
            var session = new GameSession(12345);
            Assert.That(session.CurrentFrame, Is.EqualTo(0));

            session.Step(new[] { new InputState() }, 1f / 60f);

            Assert.That(session.CurrentFrame, Is.EqualTo(1), "Step should advance the frame");
            Assert.That(session.InputBuffer.TryGet(0, out _), Is.True, "Step should record frame 0's input");
        }

        [Test]
        public void RenderSnapshot_Captures_DrawableState()
        {
            var session = new GameSession(12345);
            var snap = session.CaptureRenderSnapshot();
            Assert.That(snap.Players.Count, Is.EqualTo(1), "One player at spawn");
            Assert.That(snap.Tiles.Count, Is.GreaterThan(0), "Map tiles should be present");
        }

        [Test]
        public void SimulationLoop_OnWorkerThread_PublishesSnapshots()
        {
            var session = new GameSession(12345);
            var loop = new SimulationLoop(session);
            loop.Start();
            Thread.Sleep(150);              // ~9 fixed ticks at 60 Hz
            var snap = loop.LatestSnapshot;
            int frame = loop.CurrentFrame;
            loop.Stop();

            Assert.That(snap, Is.Not.Null, "Loop should publish a snapshot");
            Assert.That(frame, Is.GreaterThan(0), "Simulation should have advanced on its own thread");
        }
    }
}
