using System.Collections.Generic;
using Kobapps.AudioKit.Core;

namespace Kobapps.AudioKit.Tests
{
    /// <summary>
    /// A fully programmable <see cref="IRandom"/> for deterministic unit tests. Queue the exact
    /// integer / float draws a test needs; falls back to 0 when a queue is empty.
    /// </summary>
    internal sealed class FakeRandom : IRandom
    {
        public readonly Queue<int> Ints = new Queue<int>();
        public readonly Queue<float> Floats = new Queue<float>();
        public uint Raw = 0u;

        public FakeRandom WithInts(params int[] values)
        {
            foreach (var v in values) Ints.Enqueue(v);
            return this;
        }

        public FakeRandom WithFloats(params float[] values)
        {
            foreach (var v in values) Floats.Enqueue(v);
            return this;
        }

        public uint NextUInt() => Raw;

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            int v = Ints.Count > 0 ? Ints.Dequeue() : 0;
            v %= maxExclusive;
            if (v < 0) v += maxExclusive;
            return v;
        }

        public float NextFloat01() => Floats.Count > 0 ? Floats.Dequeue() : 0f;

        public float NextRange(float min, float max) => min;
    }
}
