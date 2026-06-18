using Bomberman.Core;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Bomberman.Tests.Core
{
    /// <summary>State snapshot capture/restore and binary serialization (Week 4).</summary>
    public class SnapshotTests
    {
        private static InputState Move(float x, float y) => new InputState { Movement = new Vector2(x, y) };

        [Test]
        public void Capture_Then_Restore_ReproducesStateExactly()
        {
            var session = new GameSession(123, 2);
            for (int i = 0; i < 20; i++) session.Step(new[] { Move(1, 0), Move(0, 1) }, 1f / 60f);

            var snap = session.CaptureState();
            int frameAtCapture = snap.Frame;
            int hashAtCapture = snap.Hash;

            // Diverge the live world, then restore.
            for (int i = 0; i < 10; i++) session.Step(new[] { Move(-1, 0), Move(0, -1) }, 1f / 60f);
            Assert.That(StateHasher.Hash(session.Simulation.World), Is.Not.EqualTo(hashAtCapture));

            session.RestoreState(snap);
            Assert.That(session.CurrentFrame, Is.EqualTo(frameAtCapture));
            Assert.That(StateHasher.Hash(session.Simulation.World), Is.EqualTo(hashAtCapture));
        }

        [Test]
        public void Serialize_Then_Deserialize_PreservesHash()
        {
            var session = new GameSession(999, 2);
            for (int i = 0; i < 15; i++) session.Step(new[] { Move(1, 0), Move(1, 0) }, 1f / 60f);

            var snap = session.CaptureState();
            byte[] bytes = snap.Serialize();
            var restored = GameStateSnapshot.Deserialize(bytes);

            Assert.That(restored.Frame, Is.EqualTo(snap.Frame));
            Assert.That(restored.Hash, Is.EqualTo(snap.Hash));
            // Recomputing from the rebuilt data must match (proves the bytes captured the full state).
            Assert.That(restored.ComputeHash(), Is.EqualTo(snap.Hash));
        }

        [Test]
        public void SnapshotStore_EvictsOldest_AndLooksUpHashByFrame()
        {
            var store = new SnapshotStore(4);
            var session = new GameSession(5, 2);
            int[] hashes = new int[6];
            for (int f = 0; f < 6; f++)
            {
                session.Step(new[] { Move(1, 0), Move(0, 0) }, 1f / 60f);
                var snap = session.CaptureState();
                hashes[snap.Frame - 1] = snap.Hash;
                store.Store(snap);
            }
            // Capacity 4: frames 1,2 evicted; 3..6 remain.
            Assert.That(store.HashAt(1), Is.Null);
            Assert.That(store.HashAt(2), Is.Null);
            Assert.That(store.HashAt(6), Is.EqualTo(hashes[5]));
            Assert.That(store.Count, Is.EqualTo(4));
        }
    }
}
