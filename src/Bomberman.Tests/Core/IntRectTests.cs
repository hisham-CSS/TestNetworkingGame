using NUnit.Framework;
using Bomberman.Core;

namespace Bomberman.Tests.Core
{
    [TestFixture]
    public class IntRectTests
    {
        [Test]
        public void Intersects_ShouldReturnTrue_WhenRectanglesOverlap()
        {
            var r1 = new IntRect(0, 0, 10, 10);
            var r2 = new IntRect(5, 5, 10, 10);
            Assert.That(r1.Intersects(r2), Is.True);
            Assert.That(r2.Intersects(r1), Is.True);
        }

        [Test]
        public void Intersects_ShouldReturnFalse_WhenRectanglesDoNotOverlap()
        {
            var r1 = new IntRect(0, 0, 10, 10);
            var r2 = new IntRect(11, 0, 10, 10); // To the right
            Assert.That(r1.Intersects(r2), Is.False);
            
            var r3 = new IntRect(0, 11, 10, 10); // Below
            Assert.That(r1.Intersects(r3), Is.False);
        }

        [Test]
        public void Intersects_ShouldReturnFalse_WhenTouchingEdges()
        {
            var r1 = new IntRect(0, 0, 10, 10);
            var r2 = new IntRect(10, 0, 10, 10); 
            Assert.That(r1.Intersects(r2), Is.False);
        }

        [Test]
        public void Contains_ShouldReturnTrue_WhenPointInside()
        {
            var r = new IntRect(0, 0, 10, 10);
            Assert.That(r.Contains(new IntVector2(5, 5)), Is.True);
            Assert.That(r.Contains(new IntVector2(0, 0)), Is.True); 
        }

        [Test]
        public void Contains_ShouldReturnFalse_WhenPointOutside()
        {
            var r = new IntRect(0, 0, 10, 10);
            Assert.That(r.Contains(new IntVector2(-1, 5)), Is.False);
            Assert.That(r.Contains(new IntVector2(10, 10)), Is.False);
            Assert.That(r.Contains(new IntVector2(10, 5)), Is.False);
        }

        [Test]
        public void Equals_ShouldReturnTrue_ForSameValues()
        {
            var r1 = new IntRect(1, 2, 3, 4);
            var r2 = new IntRect(1, 2, 3, 4);
            Assert.That(r1, Is.EqualTo(r2));
            Assert.That(r1 == r2, Is.True);
        }

        [Test]
        public void ToString_ShouldFormatCorrectly()
        {
            var r = new IntRect(1, 2, 3, 4);
            Assert.That(r.ToString(), Is.EqualTo("{X:1 Y:2 Width:3 Height:4}"));
        }
    }
}
