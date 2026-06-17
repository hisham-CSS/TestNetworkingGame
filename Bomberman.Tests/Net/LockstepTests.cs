using Bomberman.Core;
using Bomberman.Net;
using Bomberman.Net.Lockstep;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Bomberman.Tests.Net
{
    /// <summary>
    /// Headless tests for the lockstep rule and its delay/loss handling. No sockets: we drive the
    /// LockstepSession's input buffers directly to assert when it steps and when it stalls.
    /// </summary>
    [TestFixture]
    public class LockstepTests
    {
        private static (LockstepSession ls, GameSession s) Make(int localId, int delay)
        {
            var session = new GameSession(12345);
            var net = new NetworkController<InputState>(new UdpTransport(0)); // no peer; SendInput is a no-op
            var ls = new LockstepSession(session, net, localId, delay);
            return (ls, session);
        }

        [Test]
        public void PureStall_DoesNotAdvanceWithoutRemoteInput()
        {
            var (ls, s) = Make(localId: 0, delay: 0);
            ls.SubmitLocalInput(new InputState { Movement = new Vector2(1, 0) });

            // We have our own input for frame 0 but not the peer's -> must stall.
            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stalled));
            Assert.That(s.CurrentFrame, Is.EqualTo(0));
            Assert.That(ls.IsStalledWaitingForRemote, Is.True);
        }

        [Test]
        public void PureStall_AdvancesOnceBothInputsPresent()
        {
            var (ls, s) = Make(localId: 0, delay: 0);
            ls.SubmitLocalInput(new InputState { Movement = new Vector2(1, 0) });
            ls.HandleRemoteInput(pid: 1, startFrame: 0, inputs: new[] { new InputState() }, 0, 0, 0);

            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stepped));
            Assert.That(s.CurrentFrame, Is.EqualTo(1));
            Assert.That(ls.IsStalledWaitingForRemote, Is.False);
        }

        [Test]
        public void InputDelay_PrefillsFrames_SoMatchStartsWithoutStalling()
        {
            var (ls, s) = Make(localId: 0, delay: 2);
            // Frames 0 and 1 are neutral-prefilled for both players at construction.
            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stepped)); // frame 0
            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stepped)); // frame 1
            Assert.That(s.CurrentFrame, Is.EqualTo(2));
            // Frame 2 needs real input that hasn't been provided yet.
            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stalled));
        }

        [Test]
        public void RedundantHistory_FillsGapLeftByLostPacket()
        {
            var (ls, s) = Make(localId: 0, delay: 0);
            for (int f = 0; f < 3; f++) ls.SubmitLocalInput(new InputState());

            // Simulate losing the packets for frames 0 and 1, then receiving a single packet whose
            // history run covers frames 0..2. One packet heals the gap.
            var run = new[] { new InputState(), new InputState(), new InputState() };
            ls.HandleRemoteInput(pid: 1, startFrame: 0, inputs: run, 0, 0, 0);

            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stepped)); // 0
            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stepped)); // 1
            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stepped)); // 2
            Assert.That(s.CurrentFrame, Is.EqualTo(3));
        }

        [Test]
        public void DuplicateRemoteInput_IsIgnored_FirstValueWins()
        {
            var (ls, _) = Make(localId: 0, delay: 0);
            ls.SubmitLocalInput(new InputState());
            ls.HandleRemoteInput(1, 0, new[] { new InputState { PlaceBomb = true } }, 0, 0, 0);
            ls.HandleRemoteInput(1, 0, new[] { new InputState { PlaceBomb = false } }, 0, 0, 0); // dup, ignored
            Assert.That(ls.TryAdvance(), Is.EqualTo(LockstepStep.Stepped));
        }

        [Test]
        public void CalculateInputDelay_ScalesWithLatency_AndClamps()
        {
            // ~16.67ms per frame at 60Hz; delay covers one-way latency (rtt/2).
            Assert.That(LockstepSession.CalculateInputDelay(0), Is.EqualTo(1));    // clamped to min 1
            Assert.That(LockstepSession.CalculateInputDelay(50), Is.EqualTo(2));   // 25ms one-way -> 2 frames
            Assert.That(LockstepSession.CalculateInputDelay(100), Is.EqualTo(3));  // 50ms one-way -> 3 frames
            Assert.That(LockstepSession.CalculateInputDelay(10000), Is.EqualTo(10)); // clamped to max
        }
    }
}
