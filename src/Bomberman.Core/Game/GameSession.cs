namespace Bomberman.Core
{
    /// <summary>
    /// Core-side owner of a single match: the deterministic Simulation, the input buffer, and the
    /// frame counter. The App layer talks to this — it never pokes the Simulation directly. This is
    /// the seam the threaded loop drives and (from Week 3) the network feeds.
    /// </summary>
    public class GameSession
    {
        public Simulation Simulation { get; }
        public InputBuffer InputBuffer { get; } = new InputBuffer();
        public int CurrentFrame { get; private set; }

        public GameSession(int seed, int numPlayers = 1) { Simulation = new Simulation(seed, numPlayers); }

        /// <summary>Record this frame's inputs, advance the simulation one fixed tick.</summary>
        public void Step(InputState[] inputs, float dt)
        {
            InputBuffer.Record(CurrentFrame, inputs);
            Simulation.Update(inputs, dt);
            CurrentFrame++;
        }

        /// <summary>Build an immutable, render-ready copy of the world for the View thread.</summary>
        public RenderSnapshot CaptureRenderSnapshot() => RenderSnapshot.From(Simulation.World, CurrentFrame);
    }
}
