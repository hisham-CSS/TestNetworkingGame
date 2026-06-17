using System;
using System.Collections.Generic;
using Bomberman.Core;

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
    /// Two settings of <see cref="InputDelay"/> show the whole progression taught this week:
    ///   * InputDelay = 0  -> pure stall lockstep (the Week-3 prototype): every frame waits a full
    ///                        round-trip for the peer, so a slow link visibly hitches.
    ///   * InputDelay = d  -> delay-based lockstep: local input captured now is scheduled to APPLY at
    ///                        frame (now + d). Because it was sent d frames early, by the time the sim
    ///                        reaches that frame the remote input has usually arrived, hiding latency.
    ///
    /// This is the Bomberman-specific driver; Week 5 generalises it into the Chronos rollback library.
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

        // Inputs indexed by the frame they APPLY to (not the frame they were captured on).
        private readonly Dictionary<int, InputState> _localInputs = new Dictionary<int, InputState>();
        private readonly Dictionary<int, InputState> _remoteInputs = new Dictionary<int, InputState>();

        // The next frame number a freshly captured local input will be scheduled into.
        private int _nextLocalFrame;

        // How many past frames of local input to resend in each packet (packet-loss insurance).
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

            // The first `InputDelay` frames have no real input yet, so both peers deterministically
            // fill them with a neutral input. This lets the match start without an initial stall.
            for (int f = 0; f < InputDelay; f++)
            {
                _localInputs[f] = default;
                _remoteInputs[f] = default;
            }

            _net.OnInputReceived += HandleRemoteInput;
        }

        /// <summary>Chooses an input delay (in frames) that covers the measured one-way latency.
        /// Clamped to a sane range so a bad ping can't make the game unplayably laggy.</summary>
        public static int CalculateInputDelay(int roundTripMs, int minDelay = 1, int maxDelay = 10)
        {
            double oneWayMs = roundTripMs / 2.0;
            int frames = (int)Math.Ceiling(oneWayMs / FrameMs);
            return Math.Clamp(frames, minDelay, maxDelay);
        }

        /// <summary>Capture this tick's local input: schedule it `InputDelay` frames ahead and send it
        /// (with a little history behind it) to the peer.</summary>
        public void SubmitLocalInput(InputState input, int posX = 0, int posY = 0, int stateHash = 0)
        {
            int applyFrame = _nextLocalFrame;
            _localInputs[applyFrame] = input;

            // Build a short run [startFrame .. applyFrame] so one received packet can cover dropped ones.
            int startFrame = Math.Max(0, applyFrame - (RedundantHistory - 1));
            int count = applyFrame - startFrame + 1;
            var history = new InputState[count];
            for (int i = 0; i < count; i++)
                history[i] = _localInputs.TryGetValue(startFrame + i, out var v) ? v : default;

            _net.SendInput(LocalPlayerId, startFrame, history, posX, posY, stateHash);
            _nextLocalFrame++;
        }

        /// <summary>Store remote inputs. Each packet carries a run starting at <paramref name="startFrame"/>;
        /// we keep the first value seen per frame, so duplicates are ignored and a later packet can fill
        /// gaps left by an earlier lost one.</summary>
        public void HandleRemoteInput(int pid, int startFrame, InputState[] inputs, int posX, int posY, int hash)
        {
            if (pid == LocalPlayerId) return; // ignore echoes of our own input
            for (int i = 0; i < inputs.Length; i++)
            {
                int frame = startFrame + i;
                if (!_remoteInputs.ContainsKey(frame))
                    _remoteInputs[frame] = inputs[i];
            }
        }

        /// <summary>Try to advance exactly one frame. Steps the deterministic sim only if both players'
        /// inputs for the current frame are in hand; otherwise reports a stall and changes nothing.</summary>
        public LockstepStep TryAdvance()
        {
            int f = _session.CurrentFrame;
            if (!_localInputs.TryGetValue(f, out var local)) return LockstepStep.Stalled;
            if (!_remoteInputs.TryGetValue(f, out var remote)) return LockstepStep.Stalled;

            var inputs = new InputState[2];
            inputs[LocalPlayerId] = local;
            inputs[RemotePlayerId] = remote;

            _session.Step(inputs, FixedTimeStep);

            // Free memory we will never need again.
            _localInputs.Remove(f - 120);
            _remoteInputs.Remove(f - 120);
            return LockstepStep.Stepped;
        }

        /// <summary>True if we are currently blocked waiting on the remote input for this frame.</summary>
        public bool IsStalledWaitingForRemote =>
            _localInputs.ContainsKey(_session.CurrentFrame) && !_remoteInputs.ContainsKey(_session.CurrentFrame);
    }
}
