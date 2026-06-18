using Bomberman.Core;
using Bomberman.Net.Desync;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Bomberman.Tests.Net
{
    /// <summary>Desync detection over per-frame state hashes (Week 4).</summary>
    public class DesyncTests
    {
        private static InputState Move(float x, float y) => new InputState { Movement = new Vector2(x, y) };

        [Test]
        public void IdenticalSimulations_ProduceMatchingHashes()
        {
            var a = new GameSession(77, 2);
            var b = new GameSession(77, 2);
            for (int i = 0; i < 25; i++)
            {
                var inputs = new[] { Move(1, 0), Move(0, 1) };
                a.Step(inputs, 1f / 60f); b.Step(inputs, 1f / 60f);
            }
            Assert.That(a.CaptureState().Hash, Is.EqualTo(b.CaptureState().Hash));
        }

        [Test]
        public void Detector_FlagsMismatch_AndIgnoresMatchAndUnknownFrame()
        {
            var session = new GameSession(77, 2);
            for (int i = 0; i < 10; i++) session.Step(new[] { Move(1, 0), Move(0, 0) }, 1f / 60f);

            var store = new SnapshotStore();
            var snap = session.CaptureState();
            store.Store(snap);
            var detector = new DesyncDetector(store);

            // Frame not buffered -> skipped (null).
            Assert.That(detector.Check(99999, 123, 0, 0), Is.Null);
            // Matching hash -> in sync (null).
            Assert.That(detector.Check(snap.Frame, snap.Hash, 0, 0), Is.Null);
            // Different hash -> a report.
            var report = detector.Check(snap.Frame, snap.Hash ^ 0x1, 50, 60);
            Assert.That(report, Is.Not.Null);
            Assert.That(report!.Value.Frame, Is.EqualTo(snap.Frame));
            Assert.That(detector.Mismatches, Is.EqualTo(1));
        }

        [Test]
        public void RestoringHostSnapshot_ConvergesADivergedPeer()
        {
            // Two peers in sync; one diverges; restoring the other's authoritative snapshot re-aligns them.
            var host = new GameSession(321, 2);
            var client = new GameSession(321, 2);
            for (int i = 0; i < 12; i++)
            {
                var inputs = new[] { Move(1, 0), Move(0, 1) };
                host.Step(inputs, 1f / 60f); client.Step(inputs, 1f / 60f);
            }
            var authoritative = host.CaptureState();

            // Corrupt the client the way ForceDesync does: nudge the PLAYER's transform. (The hash
            // covers player/bomb positions, not tile positions, so we must move a player to diverge.)
            NudgePlayer0(client, new Vector2(3, 0));
            Assert.That(client.CaptureState().Hash, Is.Not.EqualTo(authoritative.Hash));

            // Resync: client restores the host's snapshot (shipped as bytes).
            client.RestoreState(GameStateSnapshot.Deserialize(authoritative.Serialize()));
            Assert.That(client.CaptureState().Hash, Is.EqualTo(authoritative.Hash));
        }
    
        private static void NudgePlayer0(GameSession session, Vector2 delta)
        {
            var world = session.Simulation.World;
            var pents = world.Players.GetEntities();
            var tents = world.Transforms.GetEntities();
            var tlist = world.Transforms.GetAll();
            var player0 = pents[0];
            for (int t = 0; t < tents.Count; t++)
                if (tents[t].Equals(player0))
                {
                    var tr = tlist[t];
                    tr.Position += delta;
                    world.Transforms.Set(t, tr);
                    return;
                }
        }
}
}
