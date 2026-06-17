using System;
using System.Diagnostics;
using System.Threading;

namespace Bomberman.Core
{
    /// <summary>
    /// Runs the fixed-timestep simulation on its own worker thread (the PRODUCER) and publishes an
    /// immutable RenderSnapshot each frame for the render thread (the CONSUMER) to draw. This is the
    /// producer-consumer pattern decoupling logic from rendering, with a double-buffered handoff:
    ///   - input flows render-thread -> sim-thread via SubmitInput (lock-guarded)
    ///   - snapshots flow sim-thread -> render-thread via the _published reference (atomic swap)
    /// Because the simulation stays deterministic and isolated (Week 1), moving it to a thread is a
    /// handoff problem, not a rewrite.
    /// </summary>
    public class SimulationLoop
    {
        public const double FixedTimeStep = 1.0 / 60.0;

        private readonly GameSession _session;
        private Thread? _thread;
        private volatile bool _running;

        private readonly object _inputLock = new object();
        private InputState _latestInput;

        private RenderSnapshot? _published;   // double-buffered handoff (latest published frame)

        public SimulationLoop(GameSession session) { _session = session; }

        public int CurrentFrame => _session.CurrentFrame;

        /// <summary>Render thread -> sim thread: publish the newest input for the next tick.</summary>
        public void SubmitInput(InputState input)
        {
            lock (_inputLock) { _latestInput = input; }
        }

        /// <summary>Render thread &lt;- sim thread: the latest published, immutable snapshot (or null before the first frame).</summary>
        public RenderSnapshot? LatestSnapshot => Volatile.Read(ref _published);

        public void Start()
        {
            if (_running) return;
            _running = true;
            Volatile.Write(ref _published, _session.CaptureRenderSnapshot()); // frame 0
            _thread = new Thread(Run) { IsBackground = true, Name = "Simulation" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            _thread?.Join();
        }

        private void Run()
        {
            var sw = Stopwatch.StartNew();
            double last = sw.Elapsed.TotalSeconds;
            double accumulator = 0;

            while (_running)
            {
                double now = sw.Elapsed.TotalSeconds;
                accumulator += now - last;
                last = now;

                while (accumulator >= FixedTimeStep)
                {
                    InputState input;
                    lock (_inputLock) { input = _latestInput; }      // consume latest input
                    _session.Step(new[] { input }, (float)FixedTimeStep);
                    accumulator -= FixedTimeStep;
                }

                // Publish a fresh immutable snapshot. Reference assignment is atomic, so the render
                // thread always sees a fully-built frame, never a half-written one (no tearing).
                Volatile.Write(ref _published, _session.CaptureRenderSnapshot());
                Thread.Sleep(1);
            }
        }
    }
}
