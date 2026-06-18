namespace Bomberman.Core
{
    /// <summary>
    /// Core-side owner of a single match: the deterministic Simulation, the input buffer, and (via the
    /// Simulation) the frame counter. The App layer talks to this, never the Simulation directly. It is
    /// the seam the threaded loop drives, the network feeds, and (Week 4) the snapshot/resync uses.
    /// </summary>
    public class GameSession
    {
        public Simulation Simulation { get; }
        public InputBuffer InputBuffer { get; } = new InputBuffer();

        /// <summary>The current frame, owned by the Simulation so a restore moves both together.</summary>
        public int CurrentFrame => Simulation.Frame;

        public GameSession(int seed, int numPlayers = 1) { Simulation = new Simulation(seed, numPlayers); }

        /// <summary>Record this frame's inputs, advance the simulation one fixed tick.</summary>
        public void Step(InputState[] inputs, float dt)
        {
            InputBuffer.Record(CurrentFrame, inputs);
            Simulation.Update(inputs, dt);
        }

        /// <summary>Capture a restorable snapshot of the world at the current frame (Week 4).</summary>
        public GameStateSnapshot CaptureState() => Simulation.CaptureState();

        /// <summary>Restore the world and frame counter from a snapshot (Week 4 resync, Week 5 rollback).</summary>
        public void RestoreState(GameStateSnapshot snap) => Simulation.RestoreState(snap);

        /// <summary>Build an immutable, render-ready copy of the world for the View thread.</summary>
        public RenderSnapshot CaptureRenderSnapshot() => RenderSnapshot.From(Simulation.World, CurrentFrame);
    }
}
