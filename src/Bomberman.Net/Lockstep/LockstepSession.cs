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
            // TODO (LA2 - Input delay): convert a round-trip time (ms) into frames of input delay.
            //  - One-way latency is roughly roundTripMs / 2.
            //  - Convert ms to frames by dividing by FrameMs (ms per frame), rounding UP (Math.Ceiling).
            //  - Clamp the result to [minDelay, maxDelay] so a bad ping cannot make the game unplayable.
            throw new System.NotImplementedException("LA2: implement CalculateInputDelay");
        }

        /// <summary>Capture this tick's local input: schedule it `InputDelay` frames ahead and send it
        /// (with a little history behind it) to the peer.</summary>
        public void SubmitLocalInput(InputState input, int posX = 0, int posY = 0, int stateHash = 0)
        {
            // TODO (LA2 - Input delay + loss): schedule and send this tick's input.
            //  1. The frame this input APPLIES to is _nextLocalFrame (which starts at InputDelay).
            //     Store it: _localInputs[applyFrame] = input;
            //  2. Build a short run of recent inputs for redundancy: frames
            //     [Max(0, applyFrame - (RedundantHistory - 1)) .. applyFrame]. Missing frames -> default.
            //  3. Send it: _net.SendInput(LocalPlayerId, startFrame, history, posX, posY, stateHash);
            //  4. Advance _nextLocalFrame by one.
            throw new System.NotImplementedException("LA2: implement SubmitLocalInput");
        }

        /// <summary>Store remote inputs. Each packet carries a run starting at <paramref name="startFrame"/>;
        /// we keep the first value seen per frame, so duplicates are ignored and a later packet can fill
        /// gaps left by an earlier lost one.</summary>
        public void HandleRemoteInput(int pid, int startFrame, InputState[] inputs, int posX, int posY, int hash)
        {
            // TODO (LA2 - Loss handling): store the remote inputs.
            //  - Ignore echoes of our own input (pid == LocalPlayerId).
            //  - The packet carries a run starting at startFrame: inputs[i] applies to frame startFrame + i.
            //  - Keep the FIRST value seen per frame (only set _remoteInputs[frame] if not already present).
            //    This ignores duplicates and lets a later packet fill a gap left by an earlier lost one.
            throw new System.NotImplementedException("LA2: implement HandleRemoteInput");
        }

        /// <summary>Try to advance exactly one frame. Steps the deterministic sim only if both players'
        /// inputs for the current frame are in hand; otherwise reports a stall and changes nothing.</summary>
        public LockstepStep TryAdvance()
        {
            // TODO (LA2 - Lockstep rule): advance exactly one frame, but only if BOTH inputs are ready.
            //  - Let f = _session.CurrentFrame.
            //  - If _localInputs has no entry for f, or _remoteInputs has no entry for f, return Stalled
            //    and change nothing (NEVER guess a missing input).
            //  - Otherwise build InputState[2] with the local input at LocalPlayerId and the remote input
            //    at RemotePlayerId, call _session.Step(inputs, FixedTimeStep), and return Stepped.
            //  - (Optional) drop very old buffered inputs to save memory.
            throw new System.NotImplementedException("LA2: implement TryAdvance");
        }

        /// <summary>True if we are currently blocked waiting on the remote input for this frame.</summary>
        public bool IsStalledWaitingForRemote =>
            _localInputs.ContainsKey(_session.CurrentFrame) && !_remoteInputs.ContainsKey(_session.CurrentFrame);
    }
}
