using System.Text;
using Bomberman.Core;
using NUnit.Framework;

namespace Bomberman.Tests.Core
{
    /// <summary>Hashing algorithms used for desync detection (Week 4).</summary>
    public class SerializationTests
    {
        [Test]
        public void Crc32_MatchesStandardCheckValue()
        {
            // The canonical CRC-32/IEEE check value for "123456789" is 0xCBF43926.
            uint crc = Crc32.Compute(Encoding.ASCII.GetBytes("123456789"));
            Assert.That(crc, Is.EqualTo(0xCBF43926u));
        }

        [Test]
        public void Crc32_DetectsASingleBitChange()
        {
            byte[] a = { 1, 2, 3, 4 };
            byte[] b = { 1, 2, 3, 5 };
            Assert.That(Crc32.Compute(a), Is.Not.EqualTo(Crc32.Compute(b)));
        }

        [Test]
        public void Jenkins_IsDeterministic_AcrossTwoIdenticalRuns()
        {
            var a = new GameSession(2024, 2);
            var b = new GameSession(2024, 2);
            for (int i = 0; i < 30; i++)
            {
                var inputs = new[] { new InputState(), new InputState() };
                a.Step(inputs, 1f / 60f);
                b.Step(inputs, 1f / 60f);
            }
            Assert.That(StateHasher.Hash(a.Simulation.World),
                        Is.EqualTo(StateHasher.Hash(b.Simulation.World)));
        }
    }
}
