using System.IO;
using System.Text;
using NUnit.Framework;
using Bomberman.Core;
using Microsoft.Xna.Framework;

namespace Bomberman.Tests
{
    [TestFixture]
    public class InputStateTests
    {
        [Test]
        public void InputState_SerializeDeserialize_RoundTrips()
        {
            var original = new InputState { Movement = new Vector2(0.6f, -0.8f), PlaceBomb = true };

            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                original.Serialize(writer);

            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            var restored = InputState.Deserialize(reader);

            Assert.That(restored, Is.EqualTo(original), "Deserialized input must equal the original");
            Assert.That(restored.Equals(original), Is.True);
        }

        [Test]
        public void InputState_Equality_Works()
        {
            var a = new InputState { Movement = new Vector2(1, 0), PlaceBomb = false };
            var b = new InputState { Movement = new Vector2(1, 0), PlaceBomb = false };
            var c = new InputState { Movement = new Vector2(1, 0), PlaceBomb = true };
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
        }
    }
}
