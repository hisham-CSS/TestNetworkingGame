using NUnit.Framework;
using Bomberman.App.States;
using Microsoft.Xna.Framework;

namespace Bomberman.Tests
{
    [TestFixture]
    public class GameOverStateTests
    {
        // Since we can't easily test Draw() output without a graphics device, 
        // we will test the LOGIC by inspecting public properties or by extracting the logic.
        // However, GameOverState logic is mostly in the constructor setting flags and Draw() using them.
        // We can verify that the state accepts the flags correctly.

        // Actually, the user asked to "unit test every behaviour".
        // The best way to test the UI logic without rendering is to extract the String Resolution logic.
        // But since we can't easily refactor the whole UI right now, let's add a test that verifies
        // the state holds the correct flags which drive the UI.
        
        [Test]
        public void GameOverState_InitializedCorrectly_ForIncompleteReplay()
        {
            var state = new GameOverState(null, null, null, -1, true, false);
            
            // Need to expose these for testing or use Reflection?
            // Or better: Let's extract the "GetTitle()" logic to a helper if we want to test it strictly.
            // For now, let's use reflection to verify internal state matches expectations.
            
            var isReplay = (bool)state.GetType().GetField("_isReplayView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(state);
            var isComplete = (bool)state.GetType().GetField("_isGameCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(state);
            
            Assert.That(isReplay, Is.True);
            Assert.That(isComplete, Is.False);
        }

        [Test]
        public void GameOverState_InitializedCorrectly_ForCompleteGame()
        {
            var state = new GameOverState(null, null, null, 1, false, true);
             
            var isReplay = (bool)state.GetType().GetField("_isReplayView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(state);
            var isComplete = (bool)state.GetType().GetField("_isGameCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(state);
            var winner = (int)state.GetType().GetField("_winnerId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(state);

            Assert.That(isReplay, Is.False);
            Assert.That(isComplete, Is.True);
            Assert.That(winner, Is.EqualTo(1));
        }

        // To truly test the string logic as requested:
        [Test]
        public void GameOverState_GetTitleString_ReturnsCorrectMessage()
        {
            // We can make a public static helper in GameOverState or just test the logic replication.
            // Let's refactor GameOverState to have a public helper for this to make it testable?
            
            Assert.That(GameOverState.GetTitle(true, false, -1), Is.EqualTo("REPLAY ENDED (INCOMPLETE)"));
            Assert.That(GameOverState.GetTitle(true, true, -1), Is.EqualTo("DRAW GAME!")); // Replay Complete mimics Game Over
            Assert.That(GameOverState.GetTitle(false, true, 0), Is.EqualTo("PLAYER 1 WINS!"));
            Assert.That(GameOverState.GetTitle(false, true, -1), Is.EqualTo("DRAW GAME!"));
        }
    }
}
