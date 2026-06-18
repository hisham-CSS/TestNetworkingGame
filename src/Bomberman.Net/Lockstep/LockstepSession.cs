using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Bomberman.Core;
using Bomberman.Net.Desync;

namespace Bomberman.Net.Lockstep
{
    /// <summary>The result of trying to advance one lockstep frame.</summary>
    public enum LockstepStep
    {
        /// <summary>Both players' inputs for the current frame were present; the simulation advanced.</summary>
        Stepped,
        /// <summary>A required input was missing; the simulation held this frame and must retry.</summary>
        Stalled
    }

    /// <summary>
    /// Drives a deterministic <see cref="GameSession"/> in lockstep with one remote peer.
    ///
    /// The rule: the simulation may only advance frame F once we hold BOTH players' inputs for F.
    /// Send inputs, not state; if anything is missing, STALL (hold the frame) rather than guess.
    ///
    /// Week 4 additions: after each confirmed frame we capture a snapshot (for its hash, and for resync),
    /// announce that hash to the peer via a Checksum packet, and compare incoming checksums to detect a
    /// desync. The host is authoritative: when it detects a divergence it pushes its snapshot of that
    /// frame back to the client, which restores it (a hard resync). Week 5 turns this into rollback.
    /// </summary>
    public sealed class LockstepSession
    {
        public const float FixedTimeStep = 1.0f / 60.0f;
        private const double FrameMs = 1000.0 / 60.0;

        private readonly GameSession _session;
        private readonly NetworkController<InputState> _net;

        public int LocalPlayerId { get; }
        public int RemotePlayerId { get; }
        public int InputDelay { get; private set; }

        /// <summary>Player 0 (the host) is authoritative: it issues resyncs.</summary>
        public bool IsAuthoritative => LocalPlayerId == 0;

        // Inputs indexed by the frame they APPLY to (not the frame they were captured on).
        private readonly Dictionary<int, InputState> _localInputs = new Dictionary<int, InputState>();
        private readonly Dictionary<int, InputState> _remoteInputs = new Dictionary<int, InputState>();

        // Week 4: one snapshot per confirmed frame (its hash drives desync detection; the full snapshot
        // is what we ship to resync a diverged peer).
        private readonly SnapshotStore _snapshots = new SnapshotStore(128);
        private readonly DesyncDetector _detector;

        public DesyncReport? LastDesync { get; private set; }
        public int ResyncCount { get; private set; }
        public event Action<DesyncReport>? OnDesyncDetected;
        public event Action<int>? OnResynced;

        private int _nextLocalFrame;
        private const int RedundantHistory = 4;

        public int CurrentFrame => _session.CurrentFrame;

        public LockstepSession(GameSession session, NetworkController<InputState> net,
                               int localPlayerId, int inputDelay)
        {
            _session = session;
            _net = net;
            LocalPlayerId = localPlayerId;
            RemotePlayerId = 1 - localPlayerId; // 2-player
            InputDelay = Math.Max(0, inputDelay);
            _nextLocalFrame = InputDelay;
            _detector = new DesyncDetector(_snapshots);

            for (int f = 0; f < InputDelay; f++)
            {
                _localInputs[f] = default;
                _remoteInputs[f] = default;
            }

            // Seed the buffer with the initial (frame 0) state so its hash is comparable.
            _snapshots.Store(_session.CaptureState());

            _net.OnInputReceived += HandleRemoteInput;
            _net.OnChecksumReceived += HandleRemoteChecksum;
        }

        /// <summary>Chooses an input delay (in frames) that covers the measured one-way latency.</summary>
        public static int CalculateInputDelay(int roundTripMs, int minDelay = 1, int maxDelay = 10)
        {
            double oneWayMs = roundTripMs / 2.0;
            int frames = (int)Math.Ceiling(oneWayMs / FrameMs);
            return Math.Clamp(frames, minDelay, maxDelay);
        }

        /// <summary>Capture this tick's local input: schedule it `InputDelay` frames ahead and send it
        /// (with a little history behind it) to the peer.</summary>
        public void SubmitLocalInput(InputState input)
        {
            int applyFrame = _nextLocalFrame;
            _localInputs[applyFrame] = input;

            int startFrame = Math.Max(0, applyFrame - (RedundantHistory - 1));
            int count = applyFrame - startFrame + 1;
            var history = new InputState[count];
            for (int i = 0; i < count; i++)
                history[i] = _localInputs.TryGetValue(startFrame + i, out var v) ? v : default;

            _net.SendInput(LocalPlayerId, startFrame, history, 0, 0, 0);
            _nextLocalFrame++;
        }

        public void HandleRemoteInput(int pid, int startFrame, InputState[] inputs, int posX, int posY, int hash)
        {
            if (pid == LocalPlayerId) return;
            for (int i = 0; i < inputs.Length; i++)
            {
                int frame = startFrame + i;
                if (!_remoteInputs.ContainsKey(frame))
                    _remoteInputs[frame] = inputs[i];
            }
        }

        /// <summary>Try to advance exactly one frame. On success, snapshot the new state, store it, and
        /// announce its hash so the peer can check for a desync.</summary>
        public LockstepStep TryAdvance()
        {
            int f = _session.CurrentFrame;
            if (!_localInputs.TryGetValue(f, out var local)) return LockstepStep.Stalled;
            if (!_remoteInputs.TryGetValue(f, out var remote)) return LockstepStep.Stalled;

            var inputs = new InputState[2];
            inputs[LocalPlayerId] = local;
            inputs[RemotePlayerId] = remote;

            _session.Step(inputs, FixedTimeStep);

            // Week 4: confirm the new frame's state and broadcast its fingerprint.
            var snap = _session.CaptureState();          // Frame = f + 1, Hash = state after frame f
            _snapshots.Store(snap);
            var (px, py) = LocalPlayerPos();
            _net.SendChecksum(snap.Frame, snap.Hash, px, py);

            _localInputs.Remove(f - 120);
            _remoteInputs.Remove(f - 120);
            return LockstepStep.Stepped;
        }

        /// <summary>A peer told us its hash for <paramref name="frame"/>. Compare; on a mismatch report
        /// it, and if we are the host, push our authoritative snapshot of that frame back to resync them.</summary>
        public void HandleRemoteChecksum(int frame, int remoteHash, int remotePosX, int remotePosY)
        {
            var report = _detector.Check(frame, remoteHash, remotePosX, remotePosY);
            if (report == null) return;

            LastDesync = report;
            OnDesyncDetected?.Invoke(report.Value);

            if (IsAuthoritative && _snapshots.TryGet(frame, out var authoritative))
                _net.BroadcastStateSync(authoritative.Serialize());   // host corrects the client
        }

        /// <summary>Apply a snapshot pushed by the host (resync). Restore the world to the authoritative
        /// state; buffered inputs from that frame on are replayed as lockstep resumes.</summary>
        public void ApplyResync(byte[] snapshotBytes)
        {
            var snap = GameStateSnapshot.Deserialize(snapshotBytes);
            _session.RestoreState(snap);     // CurrentFrame jumps to snap.Frame
            _snapshots.Clear();
            _snapshots.Store(snap);          // re-seed with the authoritative state
            ResyncCount++;
            OnResynced?.Invoke(snap.Frame);
        }

        /// <summary>Demo hook: nudge the local player's position by one unit so this peer's state (and
        /// hence its hash) diverges from the remote, proving desync detection and resync fire.</summary>
        public void ForceDesync()
        {
            var world = _session.Simulation.World;
            var players = world.Players.GetAll();
            var pents = world.Players.GetEntities();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId != (uint)LocalPlayerId) continue;
                var te = world.Transforms.GetEntities();
                for (int t = 0; t < te.Count; t++)
                {
                    if (!te[t].Equals(pents[i])) continue;
                    var tr = world.Transforms.Get(t);
                    tr.Position += new Vector2(1, 0);
                    world.Transforms.Set(t, tr);
                    return;
                }
            }
        }

        private (int x, int y) LocalPlayerPos()
        {
            var world = _session.Simulation.World;
            var players = world.Players.GetAll();
            var pents = world.Players.GetEntities();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId != (uint)LocalPlayerId) continue;
                var te = world.Transforms.GetEntities();
                var tr = world.Transforms.GetAll();
                for (int t = 0; t < te.Count; t++)
                    if (te[t].Equals(pents[i]))
                        return ((int)tr[t].Position.X, (int)tr[t].Position.Y);
            }
            return (0, 0);
        }

        public bool IsStalledWaitingForRemote =>
            _localInputs.ContainsKey(_session.CurrentFrame) && !_remoteInputs.ContainsKey(_session.CurrentFrame);
    }
}
