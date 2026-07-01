using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class PlaylistSequencerTests
    {
        [Test]
        public void Empty_ReturnsMinusOne()
        {
            var s = new PlaylistSequencer();
            Assert.AreEqual(-1, s.Next(0, PlaylistMode.Sorted, new XorShiftRandom()));
        }

        [Test]
        public void Single_AlwaysZero()
        {
            var s = new PlaylistSequencer();
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(0, s.Next(1, PlaylistMode.Random, new XorShiftRandom()));
        }

        [Test]
        public void Sorted_CyclesInOrder()
        {
            var s = new PlaylistSequencer();
            var rng = new XorShiftRandom();
            int[] expected = { 0, 1, 2, 3, 0, 1 };
            foreach (int e in expected)
                Assert.AreEqual(e, s.Next(4, PlaylistMode.Sorted, rng));
        }

        [Test]
        public void Random_StaysInRange()
        {
            var s = new PlaylistSequencer();
            var rng = new XorShiftRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                int idx = s.Next(5, PlaylistMode.Random, rng);
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, 5);
            }
        }

        [Test]
        public void RandomNoRepeat_NeverRepeatsConsecutively()
        {
            var s = new PlaylistSequencer();
            var rng = new XorShiftRandom(7);
            int prev = -1;
            for (int i = 0; i < 3000; i++)
            {
                int idx = s.Next(4, PlaylistMode.RandomNoRepeat, rng);
                Assert.AreNotEqual(prev, idx);
                prev = idx;
            }
        }

        [Test]
        public void Reset_RestartsSortedFromZero()
        {
            var s = new PlaylistSequencer();
            var rng = new XorShiftRandom();
            s.Next(3, PlaylistMode.Sorted, rng); // 0
            s.Next(3, PlaylistMode.Sorted, rng); // 1
            s.Reset();
            Assert.AreEqual(0, s.Next(3, PlaylistMode.Sorted, rng));
        }
    }
}
