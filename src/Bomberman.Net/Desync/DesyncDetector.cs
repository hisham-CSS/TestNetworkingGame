using Bomberman.Core;

namespace Bomberman.Net.Desync
{
    /// <summary>Diagnostic record of a detected divergence between two peers at one frame.</summary>
    public readonly struct DesyncReport
    {
        public readonly int Frame;
        public readonly int LocalHash;
        public readonly int RemoteHash;
        public readonly int RemotePosX;
        public readonly int RemotePosY;

        public DesyncReport(int frame, int localHash, int remoteHash, int rx, int ry)
        { Frame = frame; LocalHash = localHash; RemoteHash = remoteHash; RemotePosX = rx; RemotePosY = ry; }

        public override string ToString()
            => $"DESYNC @ frame {Frame}: local=0x{LocalHash:X8} remote=0x{RemoteHash:X8} (remote pos {RemotePosX},{RemotePosY})";
    }

    /// <summary>
    /// Compares a remote peer's per-frame state hash against our own stored hash for that frame. A match
    /// means the simulations agree; a mismatch is a desync, reported with enough context (frame, both
    /// hashes, remote position) to start diagnosing. If the frame is no longer buffered (too old) or we
    /// have not simulated it yet, the comparison is skipped rather than guessed.
    /// </summary>
    public sealed class DesyncDetector
    {
        private readonly SnapshotStore _store;

        public int Comparisons { get; private set; }
        public int Mismatches { get; private set; }
        public int LastMatchedFrame { get; private set; } = -1;

        public DesyncDetector(SnapshotStore store) { _store = store; }

        public DesyncReport? Check(int frame, int remoteHash, int remotePosX, int remotePosY)
        {
            int? local = _store.HashAt(frame);
            if (local == null) return null;       // can't compare this frame

            Comparisons++;
            if (local.Value == remoteHash)
            {
                LastMatchedFrame = frame;
                return null;                       // in sync
            }

            Mismatches++;
            return new DesyncReport(frame, local.Value, remoteHash, remotePosX, remotePosY);
        }
    }
}
