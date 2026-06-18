using Microsoft.Xna.Framework;

namespace Bomberman.App.Rendering
{
    /// <summary>
    /// Shared UI palette so every screen matches the look students saw in Weeks 1-4:
    /// a dark navy background with cyan titles, amber selection, and green/red status.
    /// Restyle only - the state machine, rollback, and replay logic are unchanged.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Bg     = new Color(14, 23, 38);    // dark navy background
        public static readonly Color Title  = new Color(34, 211, 238);  // cyan headings
        public static readonly Color Accent = new Color(251, 191, 36);  // amber selection / highlight
        public static readonly Color Text   = new Color(230, 237, 247); // near-white body text
        public static readonly Color Muted  = new Color(120, 140, 170); // hints / secondary
        public static readonly Color Ok     = new Color(52, 211, 153);  // ready / success / go
        public static readonly Color Bad    = new Color(248, 113, 113); // not ready / error / rec
    }
}
