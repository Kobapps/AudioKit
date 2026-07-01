using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class VariationSelectorTests
    {
        private static SelectionState NewState(int count)
        {
            var s = new SelectionState();
            s.EnsureCapacity(count);
            return s;
        }

        [Test]
        public void EmptyGroup_ReturnsMinusOne()
        {
            var s = NewState(0);
            Assert.AreEqual(-1, VariationSelector.Select(PlayMode.Random, 0, null, s, new XorShiftRandom()));
        }

        [Test]
        public void SingleVariation_AlwaysReturnsZero()
        {
            var s = NewState(1);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(0, VariationSelector.Select(PlayMode.RandomNoImmediateRepeat, 1, null, s, new XorShiftRandom()));
        }

        [Test]
        public void Random_StaysInRange()
        {
            var s = NewState(4);
            var rng = new XorShiftRandom(12345);
            for (int i = 0; i < 1000; i++)
            {
                int idx = VariationSelector.Select(PlayMode.Random, 4, null, s, rng);
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, 4);
            }
        }

        [Test]
        public void RandomNoImmediateRepeat_NeverRepeatsConsecutively()
        {
            var s = NewState(5);
            var rng = new XorShiftRandom(999);
            int prev = -1;
            for (int i = 0; i < 5000; i++)
            {
                int idx = VariationSelector.Select(PlayMode.RandomNoImmediateRepeat, 5, null, s, rng);
                Assert.AreNotEqual(prev, idx, "no-immediate-repeat produced a back-to-back duplicate");
                prev = idx;
            }
        }

        [Test]
        public void RandomNoImmediateRepeat_CoversAllIndices()
        {
            var s = NewState(4);
            var rng = new XorShiftRandom(7);
            var seen = new bool[4];
            for (int i = 0; i < 2000; i++)
                seen[VariationSelector.Select(PlayMode.RandomNoImmediateRepeat, 4, null, s, rng)] = true;
            foreach (var b in seen) Assert.IsTrue(b, "every index should eventually be chosen");
        }

        [Test]
        public void Sequential_CyclesDeterministicallyFromZero()
        {
            var s = NewState(3);
            var rng = new XorShiftRandom();
            int[] expected = { 0, 1, 2, 0, 1, 2, 0 };
            foreach (int e in expected)
                Assert.AreEqual(e, VariationSelector.Select(PlayMode.Sequential, 3, null, s, rng));
        }

        [Test]
        public void RoundRobin_CoversEveryVariationOncePerCycle()
        {
            var s = NewState(4);
            // FakeRandom seeds the initial phase to 0 so the cycle is predictable.
            var rng = new FakeRandom().WithInts(0);
            var counts = new int[4];
            for (int i = 0; i < 4; i++)
                counts[VariationSelector.Select(PlayMode.RoundRobin, 4, null, s, rng)]++;
            foreach (int c in counts)
                Assert.AreEqual(1, c, "each variation should play exactly once per round-robin cycle");
        }

        [Test]
        public void Weighted_RespectsZeroWeight_AndApproximatesProportions()
        {
            var s = NewState(3);
            var rng = new XorShiftRandom(2024);
            float[] weights = { 1f, 0f, 3f }; // index 1 never; index 2 ~3x index 0
            var counts = new int[3];
            const int N = 40000;
            for (int i = 0; i < N; i++)
                counts[VariationSelector.Select(PlayMode.Weighted, 3, weights, s, rng)]++;

            Assert.AreEqual(0, counts[1], "zero-weight variation must never be selected");

            float ratio = (float)counts[2] / counts[0];
            Assert.That(ratio, Is.EqualTo(3f).Within(0.25f), "weighted ratio should approximate 3:1");
        }

        [Test]
        public void Weighted_AllZeroWeights_FallsBackToUniform()
        {
            var s = NewState(3);
            var rng = new XorShiftRandom(5);
            float[] weights = { 0f, 0f, 0f };
            var counts = new int[3];
            for (int i = 0; i < 6000; i++)
                counts[VariationSelector.Select(PlayMode.Weighted, 3, weights, s, rng)]++;
            foreach (int c in counts)
                Assert.Greater(c, 0, "uniform fallback should hit every index");
        }

        [Test]
        public void Oldest_BehavesAsLeastRecentlyUsed()
        {
            var s = NewState(3);
            var rng = new XorShiftRandom();
            // First three picks must cover all indices (each becomes the newest, so the next
            // pick is forced to a different, older one).
            var seen = new bool[3];
            for (int i = 0; i < 3; i++)
                seen[VariationSelector.Select(PlayMode.Oldest, 3, null, s, rng)] = true;
            foreach (var b in seen) Assert.IsTrue(b, "Oldest should visit every variation before repeating");

            // Steady state: the index just played is never the immediate next pick.
            int prev = VariationSelector.Select(PlayMode.Oldest, 3, null, s, rng);
            for (int i = 0; i < 50; i++)
            {
                int idx = VariationSelector.Select(PlayMode.Oldest, 3, null, s, rng);
                Assert.AreNotEqual(prev, idx);
                prev = idx;
            }
        }
    }
}
